using System;
using System.Reflection;
using Firebase.Auth;
using Firebase.Extensions;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using Managers; // SignInStatus
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SocialPlatforms;

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
        [Header("Photon")]
        [SerializeField] private string defaultRegion = "asia";

        private FirebaseAuth _auth;
        private FirebaseUser _user;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _auth = FirebaseAuth.DefaultInstance;

            // v2.x에선 Activate만으로 충분
            try { PlayGamesPlatform.Activate(); } catch { /* no-op */ }
            
            // Photon 단계별 로그
            /*gameObject.AddComponent<Photon.Pun.UtilityScripts.PhotonStatsGui>().enabled = false; 
            gameObject.AddComponent<Photon.Pun.UtilityScripts.ConnectAndJoinRandom>(); 
            gameObject.AddComponent<Photon.Realtime.SupportLogger>();*/ 
        }

        /// <summary>UI 버튼에서 호출</summary>
        public void LoginWithGPGS()
        {
            PreflightLog();
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != SignInStatus.Success)
                {
                    Debug.LogError($"[GPGS] Authenticate 실패: {status}");
                    return;
                }

                string displayName = Social.localUser?.userName ?? "Player";

                // 1) v2.1.0 정석: 서버 인증코드 먼저
                try
                {
                    PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                    {
                        if (!string.IsNullOrEmpty(code))
                        {
                            Debug.Log("[GPGS] ServerAuthCode OK");
                            Debug.Log($"[GPGS] ServerAuthCode length={code.Length}");
                            var cred = PlayGamesAuthProvider.GetCredential(code);
                            SignInFirebase(cred, displayName);
                        }
                        else
                        {
                            TryIdTokenFallback(displayName);
                            Debug.LogWarning("[GPGS] ServerAuthCode EMPTY → Try IdToken fallback");
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
            catch { /* ignore */ }

            if (!string.IsNullOrEmpty(idToken))
            {
                Debug.Log("[GPGS] IdToken OK (fallback)");
                var cred = GoogleAuthProvider.GetCredential(idToken, null);
                SignInFirebase(cred, displayName);
            }
            else
            {
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
                    Debug.LogError($"[GPGS] Firebase SignIn 실패: {t.Exception}");
                    return;
                }

                // FirebaseUser 또는 AuthResult.User 모두 대응
                FirebaseUser fbUser = null;
                try { fbUser = t.GetType().GetProperty("Result")?.GetValue(t) as FirebaseUser; } catch { }
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
                    Debug.LogError("[GPGS] FirebaseUser 획득 실패(패키지 버전 확인).");
                    return;
                }

                _user = fbUser;

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
            
            
            if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();
            else if (!PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby) // 방어로직 추가
                PhotonNetwork.JoinLobby();
        }

        public override void OnConnectedToMaster()
        {
            if (!PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby) // 방어로직 추가
                PhotonNetwork.JoinLobby();
        }
        
        private void PreflightLog()
        {
            var app = Firebase.FirebaseApp.DefaultInstance;
            var opts = app?.Options;
            Debug.Log($"[GPGS] Preflight: Firebase ProjectId={opts?.ProjectId}, AppId={opts?.AppId}, ApiKey={(opts?.ApiKey?.Substring(0,6) ?? "null")}...");

            // GPGS 플랫폼 활성화 여부
            Debug.Log($"[GPGS] PlayGamesPlatform.Active? {(GooglePlayGames.PlayGamesPlatform.Instance != null)}");

            // Photon 지역/설정
            Debug.Log($"[GPGS] Photon.FixedRegion={Photon.Pun.PhotonNetwork.PhotonServerSettings?.AppSettings?.FixedRegion}");
        }
    }
}
