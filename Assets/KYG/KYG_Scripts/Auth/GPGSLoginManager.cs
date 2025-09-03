using System;
using Firebase.Auth;
using Firebase.Extensions;
using GooglePlayGames;
using GooglePlayGames.BasicApi; // SignInStatus
using Photon.Pun;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace KYG.Auth
{
    /// <summary>
    /// GPGS → Firebase Auth → Photon 접속/UID 주입 (v2.1.0 대응)
    /// - 1순위: RequestServerSideAccess로 ServerAuthCode 획득 후 PlayGamesAuthProvider 사용
    /// - 2순위: IdToken 폴백(GoogleAuthProvider)
    /// - FirebaseUser/AuthResult 반환형 차이 안전 처리
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

            // Firebase 핸들
            _auth = FirebaseAuth.DefaultInstance;

            // GPGS 활성화 (v2.x는 이 한 줄이면 충분)
            try { PlayGamesPlatform.Activate(); } catch { /* no-op */ }
        }

        /// <summary>UI 버튼에서 호출</summary>
        public void LoginWithGPGS()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != SignInStatus.Success)
                {
                    Debug.LogError($"[GPGS] Authenticate 실패: {status}");
                    return;
                }

                string displayName = Social.localUser?.userName ?? "Player";

                // v2.1.0 정석: 서버 인증코드 먼저 요청
                try
                {
                    PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                    {
                        if (!string.IsNullOrEmpty(code))
                        {
                            Debug.Log("[GPGS] ServerAuthCode OK (v2.1.0)");
                            var cred = PlayGamesAuthProvider.GetCredential(code);
                            SignInFirebase(cred, displayName);
                        }
                        else
                        {
                            // 폴백: IdToken
                            TryIdToken(displayName);
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GPGS] RequestServerSideAccess 예외: {e.Message} → IdToken 폴백");
                    TryIdToken(displayName);
                }
            });
#else
            Debug.LogWarning("[GPGS] Android 기기에서 테스트하세요. (에디터 미지원)");
#endif
        }

        /// <summary>서버 인증코드 실패 시 IdToken으로 Firebase 로그인</summary>
        private void TryIdToken(string displayName)
        {
            string idToken = null;
            try
            {
                var active = Social.Active as PlayGamesPlatform;
                if (active != null)
                    idToken = active.GetIdToken(); // v2.x에서도 제공
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

                // FirebaseUser 혹은 AuthResult.User 대응
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

                // 디스플레이명 업데이트 후 Photon 주입
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
            PhotonNetwork.AuthValues = new AuthenticationValues(uid);

            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "uid", uid } });
            Debug.Log($"[GPGS] Photon uid set: {uid}, nick: {PhotonNetwork.NickName}");

            if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();
            else if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }

        public override void OnConnectedToMaster()
        {
            PhotonNetwork.JoinLobby(); // 로비 진입 후 UidPersistenceGuard/PlayerDirectory가 보강
        }
    }
}
