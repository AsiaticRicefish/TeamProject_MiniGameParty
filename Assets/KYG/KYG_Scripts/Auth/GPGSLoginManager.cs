using System;
using System.Reflection;
using Firebase.Auth;
using Firebase.Extensions;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using GooglePlayGames.BasicApi; // SignInStatus 사용

namespace KYG.Auth
{
    /// <summary>
    /// GPGS 로그인 → Firebase 연동 → Photon 접속/UID 주입
    /// - GPGS 서버 인증코드가 없으면 IdToken으로 GoogleAuthProvider로 폴백
    /// - Firebase 반환형(FirebaseUser/AuthResult) 차이를 안전 처리
    /// - 기존 게스트 로그인 파이프라인과 동일하게 uid/nickname 주입
    /// </summary>
    public class GPGSLoginManager : MonoBehaviourPunCallbacks
    {
        [Header("Photon")]
        [SerializeField] private string defaultRegion = "asia";

        private FirebaseAuth _auth;
        private FirebaseUser _user;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            // Firebase 핸들: 기존 전역/게스트 초기화를 재활용
            _auth = FirebaseAuth.DefaultInstance; // BackendManager 통해 공유됨 :contentReference[oaicite:2]{index=2}

            // 일부 버전에서 Activate만 있어도 충분 (InitializeInstance 없음 대응)
            try { PlayGamesPlatform.Activate(); } catch { /* noop */ }
        }

        /// <summary>UI 버튼에서 호출</summary>
        public void LoginWithGPGS()
        {
            // 현재 프로젝트 GPGS 버전은 SignInStatus 콜백을 사용합니다.
            PlayGamesPlatform.Instance.Authenticate((SignInStatus status) =>
            {
                if (status != SignInStatus.Success)
                {
                    Debug.LogError($"[GPGS] Authenticate 실패: {status}");
                    return;
                }

                string displayName = Social.localUser?.userName ?? "Player";

                // 1) ServerAuthCode 우선 시도 (없으면 null)
                string serverAuthCode = TryGetServerAuthCode();
                if (!string.IsNullOrEmpty(serverAuthCode))
                {
                    var credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);
                    SignInFirebase(credential, displayName);
                    return;
                }

                // 2) 폴백: IdToken으로 GoogleAuthProvider 사용
                string idToken = TryGetIdToken();
                if (!string.IsNullOrEmpty(idToken))
                {
                    var credential = GoogleAuthProvider.GetCredential(idToken, null);
                    SignInFirebase(credential, displayName);
                    return;
                }

                Debug.LogError("[GPGS] ServerAuthCode/IdToken 모두 획득 실패(콘솔 설정/패키지 확인).");
            });
        }

        // --- GPGS 토큰 획득 유틸 ---

        private string TryGetServerAuthCode()
        {
            try
            {
                // 일부 버전: PlayGamesPlatform.Instance.GetServerAuthCode() 존재
                var inst = PlayGamesPlatform.Instance;
                var mi = inst.GetType().GetMethod("GetServerAuthCode", BindingFlags.Public | BindingFlags.Instance);
                if (mi != null)
                {
                    var code = mi.Invoke(inst, null) as string;
                    if (!string.IsNullOrEmpty(code))
                    {
                        Debug.Log("[GPGS] ServerAuthCode OK");
                        return code;
                    }
                }
            }
            catch { /* ignore */ }
            return null;
        }

        private string TryGetIdToken()
        {
            try
            {
                // 일부 버전: ((PlayGamesPlatform)Social.Active).GetIdToken()
                var active = Social.Active as PlayGamesPlatform;
                if (active != null)
                {
                    var mi = active.GetType().GetMethod("GetIdToken", BindingFlags.Public | BindingFlags.Instance);
                    if (mi != null)
                    {
                        var token = mi.Invoke(active, null) as string;
                        if (!string.IsNullOrEmpty(token))
                        {
                            Debug.Log("[GPGS] IdToken OK (fallback)");
                            return token;
                        }
                    }
                }
            }
            catch { /* ignore */ }
            return null;
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

                // 반환형: FirebaseUser 또는 AuthResult 모두 처리
                FirebaseUser fbUser = null;
                try
                {
                    // FirebaseUser 직접 반환 버전
                    fbUser = t.GetType().GetProperty("Result")?.GetValue(t) as FirebaseUser;
                }
                catch { /* ignore */ }

                if (fbUser == null)
                {
                    try
                    {
                        // AuthResult 반환 버전: .User 취득
                        var resultObj = t.GetType().GetProperty("Result")?.GetValue(t);
                        fbUser = resultObj?.GetType().GetProperty("User")?.GetValue(resultObj) as FirebaseUser;
                    }
                    catch { /* ignore */ }
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
                    _user.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(_ => ApplyPhotonAndConnect(_user.UserId, displayName));
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

            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "uid", uid } });
            Debug.Log($"[GPGS] Photon uid set: {uid}, nick: {PhotonNetwork.NickName}");

            if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();
            else if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
        }

        public override void OnConnectedToMaster()
        {
            // 기존 게스트 파이프라인과 동일하게 uid 보정/디렉토리 연동과 함께 동작합니다.
            PhotonNetwork.JoinLobby(); // 로비 진입 후 UidPersistenceGuard/PlayerDirectory 동작 :contentReference[oaicite:3]{index=3} :contentReference[oaicite:4]{index=4} :contentReference[oaicite:5]{index=5} :contentReference[oaicite:6]{index=6}
        }
    }
}
