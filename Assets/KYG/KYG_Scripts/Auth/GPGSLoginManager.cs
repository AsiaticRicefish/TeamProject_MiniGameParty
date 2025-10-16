using System;
using System.Reflection;
using Firebase.Auth;
using Firebase.Extensions;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using LDH_Game;
using Managers; // SignInStatus
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using LDH_Util;

namespace KYG.Auth
{
    /// <summary>
    /// GPGS → Firebase Auth → Photon(uid 주입) (v2.1.0 호환)
    /// - 1순위: RequestServerSideAccess 로 ServerAuthCode 획득
    /// - 2순위: GetIdToken 리플렉션 폴백
    /// - Photon.AuthValues 는 완전수식으로 안전 지정
    /// </summary>
    public class GPGSLoginManager : MonoBehaviourPunCallbacks
    {
        [Header("Photon")] [SerializeField] private string defaultRegion = "asia";

        private FirebaseAuth _auth;
        private FirebaseUser _user;


        // GPGS event <- button 구독
        // button click -> gpgs login 호출 -> event invoke -> 버튼 구독


        public event Action<bool> OnGPGSLogin;
        public bool Processing { get; private set; } = false;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _auth = FirebaseAuth.DefaultInstance;

            // v2.x에선 Activate만으로 충분
            try { PlayGamesPlatform.Activate(); }
            catch
            {
                /* no-op */
            }

            // Photon 단계별 로그
            /*gameObject.AddComponent<Photon.Pun.UtilityScripts.PhotonStatsGui>().enabled = false;
            gameObject.AddComponent<Photon.Pun.UtilityScripts.ConnectAndJoinRandom>();
            gameObject.AddComponent<Photon.Realtime.SupportLogger>();*/
        }

        /// <summary>UI 버튼에서 호출</summary>
        public void LoginWithGPGS()
        {
            //중복 처리 방지
            if (Processing) return;
            SetProcessing(true);
            
            PreflightLog(); // 사전 로그 출력 (Firebase/GPGS/Photon 상태 확인)    

#if UNITY_ANDROID && !UNITY_EDITOR
            // 1) GPGS 디버그 로그 켜기(개발중에만)
            PlayGamesPlatform.DebugLogEnabled = true;

            // 2) 이전 세션 꼬임 방지: SignOut은 버전 의존 → 안전 호출
            try
            {
                var platform = PlayGamesPlatform.Instance;
                if (platform != null)
                {
                    var mi = typeof(PlayGamesPlatform).GetMethod("SignOut",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null) mi.Invoke(platform, null);
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[GPGS] SignOut 생략 또는 실패(무시): {e.Message}");
            }


            // 3) 인증 시작
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
                {
                    Manager.UI.EnqueueToast(Define_LDH.ToastType.Error, "GPGS Authenticate 실패");
                    Debug.LogError($"[GPGS] Authenticate 실패: {status}");

                    //processing 플래그 초기화
                    SetProcessing(false);

                    return;
                }

                // 4) 임시 표시 이름 (Firebase 로그인 후 DisplayName으로 덮어씀)
                string displayName = Social.localUser.userName;
                if (string.IsNullOrWhiteSpace(displayName)) displayName = "Player";

                // 5) 서버 인증코드(권장) → Firebase 크리덴셜 생성
                try
                {
                    PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                    {
                        if (!string.IsNullOrEmpty(code))
                        {
                            Debug.Log("[GPGS] ServerAuthCode OK");
                            //var cred = GooglePlayGames.BasicApi.PlayGamesServerAuthCode.GetServerAuthCodeCredential(code);
                            var cred = Firebase.Auth.PlayGamesAuthProvider.GetCredential(code);

                            SignInFirebase(cred, displayName);
                        }
                        else
                        {
                            Debug.LogWarning("[GPGS] ServerAuthCode 비어있음 → (선택) IdToken 폴백 로직으로");
                            TryIdTokenFallback(displayName); // 구현해두신 폴백 함수 사용
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GPGS] RequestServerSideAccess 예외: {e.Message} → IdToken 폴백");
                    TryIdTokenFallback(displayName);
                }
            });
#else
            Debug.LogWarning("[GPGS] Android 기기에서 테스트하세요. (에디터 미지원)");
            SetProcessing(false);
#endif
        }

        /// <summary>GetIdToken 공개 API가 없는 환경을 위한 리플렉션 폴백</summary>
        private void TryIdTokenFallback(string displayName)
        {
            string idToken = null;
            try
            {
                // Social.Active 또는 Instance 양쪽 다 시도
                var active = (Social.Active as PlayGamesPlatform) ?? (PlayGamesPlatform.Instance as PlayGamesPlatform);
                if (active != null)
                {
                    var mi = active.GetType().GetMethod("GetIdToken", BindingFlags.Public | BindingFlags.Instance);
                    if (mi != null) idToken = mi.Invoke(active, null) as string;
                }
            }
            catch
            {
                /* ignore */
            }

            if (!string.IsNullOrEmpty(idToken))
            {
                Debug.Log("[GPGS] IdToken OK (fallback)");
                var cred = GoogleAuthProvider.GetCredential(idToken, null);
                SignInFirebase(cred, displayName);
            }
            else
            {
                Manager.UI.EnqueueToast(Define_LDH.ToastType.Error, "GPGS ServerAuthCode/IdToken 실패");
                SetProcessing(false);

                Debug.LogError("[GPGS] ServerAuthCode/IdToken 모두 획득 실패. 콘솔/키/리졸버 설정 확인 필요.");
            }
        }

        // --- Firebase 로그인 & Photon 주입 ---

        private void SignInFirebase(Credential credential, string displayName)
        {
            _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    Manager.UI.EnqueueToast(Define_LDH.ToastType.Error, "Firebase SignIn 실패");
                    SetProcessing(false);
                    Debug.LogError($"[GPGS] Firebase SignIn 실패: {t.Exception}");
                    return;
                }

                // FirebaseUser 또는 AuthResult.User 모두 대응
                FirebaseUser fbUser = null;
                try { fbUser = t.GetType().GetProperty("Result")?.GetValue(t) as FirebaseUser; }
                catch { }

                if (fbUser == null)
                {
                    try
                    {
                        var resultObj = t.GetType().GetProperty("Result")?.GetValue(t);
                        fbUser = resultObj?.GetType().GetProperty("User")?.GetValue(resultObj) as FirebaseUser;
                    }
                    catch { }
                }

                if (fbUser == null)
                {
                    Manager.UI.EnqueueToast(Define_LDH.ToastType.Error, "FirebaseUser 획득 실패");
                    SetProcessing(false);
                    Debug.LogError("[GPGS] FirebaseUser 획득 실패(패키지 버전 확인).");
                    return;
                }

                _user = fbUser;

                _ = SessionEnforcer.Instance?.StartForUidAsync(_user.UserId);

                if (!string.IsNullOrEmpty(displayName))
                {
                    var profile = new UserProfile { DisplayName = displayName };
                    _user.UpdateUserProfileAsync(profile)
                        .ContinueWithOnMainThread(_ => ApplyPhotonAndConnect(_user.UserId, displayName));
                }
                else
                {
                    ApplyPhotonAndConnect(_user.UserId, _user.DisplayName);
                }
            });
        }

        private void ApplyPhotonAndConnect(string uid, string nickname)
        {
            PhotonNetwork.AutomaticallySyncScene = true;
            if (!string.IsNullOrEmpty(defaultRegion))
                PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = defaultRegion;

            PhotonNetwork.NickName = string.IsNullOrEmpty(nickname) ? "Player" : nickname;

            // AuthValues (Google UID 기반)
            var authValues = new Photon.Realtime.AuthenticationValues(uid);
            PhotonNetwork.AuthValues = authValues;

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable { { "uid", uid } });

            // Debug 로그 추가
            Debug.Log($"[GPGS] Firebase UID={uid}, Nickname={PhotonNetwork.NickName}");
            Debug.Log($"[GPGS] Photon.AuthValues.UserId={PhotonNetwork.AuthValues?.UserId}");

            Manager.UI.EnqueueToast(Define_LDH.ToastType.Check, "GPGS 로그인 성공");
            //game 리소스 다운 / 초기화 및 파이어베이스 데이터 로드 진행 후 서버로 연결하기 위해 game boot strap을 생성한다.
            Util_LDH.ConsoleLog(this, "------------Game Start Bootstrap을 만듭니다. -----------");
            GameObject gameBootstrap = new GameObject("GameStartBootstrap", typeof(GameStartBootstrap));

            // if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();
            // else if (!PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby) // 방어로직 추가
            //     PhotonNetwork.JoinLobby();
        }

        private async Task ApplyPhotonAndConnectAsync(string uid, string nickname)
        {
            var enf = FindObjectOfType<SessionEnforcer>(true);
            if (enf != null)
            {
                var ok = await enf.StartForUidAsync(uid);
                if (!ok)
                {
                    /* UI 폴백 */
                    return;
                }
            }

            AuthAccount.Remember("gpgs", uid, nickname);

            PhotonNetwork.NickName = nickname;
            PhotonNetwork.AuthValues = new Photon.Realtime.AuthenticationValues(uid);
            // new GameObject("Game Bootstrap", typeof(GameStartBootstrap));
        }

        public override void OnConnectedToMaster()
        {
            // 중복로비 진입으로 주석처리합니다.
            // if (!PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby) // 방어로직 추가
            //     PhotonNetwork.JoinLobby();
        }

        private void PreflightLog()
        {
            var app = Firebase.FirebaseApp.DefaultInstance;
            var opts = app?.Options;
            Debug.Log(
                $"[GPGS] Preflight: Firebase ProjectId={opts?.ProjectId}, AppId={opts?.AppId}, ApiKey={(opts?.ApiKey?.Substring(0, 6) ?? "null")}...");

            // GPGS 플랫폼 활성화 여부
            Debug.Log($"[GPGS] PlayGamesPlatform.Active? {(GooglePlayGames.PlayGamesPlatform.Instance != null)}");

            // Photon 지역/설정
            Debug.Log(
                $"[GPGS] Photon.FixedRegion={Photon.Pun.PhotonNetwork.PhotonServerSettings?.AppSettings?.FixedRegion}");
        }


        private void SetProcessing(bool value)
        {
            if (Processing == value) return;
            Processing = value;
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                OnGPGSLogin?.Invoke(Processing);
            });
        }

    }
}