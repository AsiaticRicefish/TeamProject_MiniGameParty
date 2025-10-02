using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;
using Photon.Pun;
using KYG.Auth;
using LDH_Game;
using LDH_Util;
using Photon.Pun.Demo.Procedural;
using UnityEngine.EventSystems;

namespace KYG
{
    public class AuthBootstrapper : MonoBehaviour
    {
        [Header("Firebase Realtime DB URL (닉네임/세션에서 사용)")] [SerializeField]
        private string databaseUrl =
            "https://<your-project-id>.firebaseio.com"; // ★ 콘솔 URL로 교체

        [Header("자동 로그인 제어")] [Tooltip("개발용: true면 앱 시작 시 무조건 로그인 UI 노출")] [SerializeField]
        private bool forceShowLoginUI = false;

        [Tooltip("최근 세션 킥이 있었으면 자동 로그인 시도 대신 UI로 폴백하는 쿨다운(초)")] [SerializeField]
        private int kickCooldownSeconds = 10;

        [Header("기본 닉네임(표시명이 비었을 때)")] [SerializeField]
        private string defaultNick = "Player";

        [Header("Verbose Logs")] [SerializeField]
        private bool verbose = true;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            // (선택) GPGS Activate는 GPGSLoginManager에서 이미 하고 있다면 생략 가능
            try { GooglePlayGames.PlayGamesPlatform.Activate(); }
            catch
            {
                /* ignore */
            }

            // (중요) 전역 네트워크 보강 유틸들을 씬에 없으면 자동 생성
            EnsureGlobalNetGlueObjects();
        }

        private async void Start()
        {
            // 로그아웃 직후 1회: 어떤 자동 로그인도 금지하고 UI만 띄움
            if (AuthAutoSuppressor.Consume())
            {
                Debug.Log("[AuthBootstrapper] auto-login suppressed → 대기 상태");
                return;
            }

            if (!await FirebaseInitGate.EnsureReadyAsync())
            {
                Debug.LogError("Firebase not ready");
                return;
            }

            // 1) Firebase 준비
            var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dep != DependencyStatus.Available)
            {
                Debug.LogError($"[AuthBootstrapper] Firebase deps: {dep}");
                return;
            }

            // 2) Realtime DB URL 전달 (닉네임/세션 유틸들이 의존)
            if (string.IsNullOrWhiteSpace(databaseUrl) || !databaseUrl.StartsWith("https://"))
            {
                Debug.LogError("[AuthBootstrapper] Realtime DB URL을 올바르게 설정하세요.");
                return;
            }
            // 닉네임 레지스트리 사용 중이라면 여기에 ConfigureDatabase(...) 호출 (프로젝트에 따라)
            // NicknameRegistry.ConfigureDatabase(databaseUrl);

            // 3) 개발자 강제 UI 옵션
            if (forceShowLoginUI)
            {
                return;
            }

            // 4) 최근 '세션 킥'이 있었으면 자동 로그인 대신 UI로 폴백 (핑퐁 방지)
            if (kickCooldownSeconds > 0 && SessionEnforcer.WasKickedRecently(kickCooldownSeconds * 1000))
            {
                if (verbose) Debug.Log("[AuthBootstrapper] 최근 세션 킥 감지 → 자동 로그인 보류, UI 표시");
                return;
            }

            var auth = FirebaseAuth.DefaultInstance;
            var cur = auth.CurrentUser;

            // 5) 기존 Firebase 세션이 있으면 그것부터 재사용 (가장 빠르고 안전)
            if (cur != null)
            {
                await ContinueWithExistingUserAsync(cur);
                return;
            }

            // 6) Firebase 세션이 없으면 → 마지막 로그인 수단(guest/gpgs)으로 무팝업 자동 로그인 시도
            if (AuthAccount.TryGet(out var provider, out var lastUid, out var lastNick))
            {
                if (verbose) Debug.Log($"[AuthBootstrapper] Try auto-login: last={provider}");

                if (provider == "guest")
                {
                    // 게스트 자동 로그인: 닉네임이 있으면 재사용
                    await TryAutoGuestAsync(lastNick);
                    return;
                }
#if UNITY_ANDROID && !UNITY_EDITOR
            else if (provider == "gpgs")
            {
                await TryAutoGpgsAsync();
                return;
            }
#endif
            }

            // 7) 완전 첫 실행 또는 정보 불충분 → UI
        }

        // ------------------ 내부 구현 ------------------

        /// <summary>
        /// Firebase 세션 유지 중: 세션 소유권(단일 세션) 확보 → Photon/Nickname 반영
        /// </summary>
        private async Task ContinueWithExistingUserAsync(FirebaseUser user)
        {
            string nick = string.IsNullOrEmpty(user.DisplayName) ? defaultNick : user.DisplayName;

            // ★ 단일 세션 시작(동시 접속 차단): 내 기기가 /sessions/{uid}를 소유하도록
            var enf = FindObjectOfType<SessionEnforcer>(true);
            if (enf != null)
            {
                var ok = await enf.StartForUidAsync(user.UserId);
                if (!ok)
                {
                    Debug.LogWarning("[AuthBootstrapper] SessionEnforcer 실패 → UI로 폴백");
                    // ShowLoginUI();
                    return;
                }
            }

            // 로컬 저장 갱신(다음 실행 자동 로그인용)
            AuthAccount.Remember(user.IsAnonymous ? "guest" : "gpgs", user.UserId, nick);

            // Photon 쪽에 UID/Nick 반영 후 게임 부트
            ApplyPhotonAndStart(user.UserId, nick);
        }

        /// <summary>
        /// 게스트 무팝업 자동 로그인(닉네임 재사용). 실패 시 UI로 폴백.
        /// </summary>
        private async Task TryAutoGuestAsync(string nickname)
        {
            try
            {
                var auth = FirebaseAuth.DefaultInstance;
                var res = await auth.SignInAnonymouslyAsync();
                var user = res.User;
                string nn = string.IsNullOrEmpty(nickname) ? defaultNick : nickname;

                // (선택) DisplayName 업데이트로 Photon Nick 동기화 쉽게
                await user.UpdateUserProfileAsync(new UserProfile { DisplayName = nn });

                // ★ 단일 세션 시작
                var enf = FindObjectOfType<SessionEnforcer>(true);
                if (enf != null)
                {
                    var ok = await enf.StartForUidAsync(user.UserId);
                    if (!ok)
                    {
                        ShowLoginUI();
                        return;
                    }
                }

                // 로컬 저장
                AuthAccount.Remember("guest", user.UserId, nn);

                ApplyPhotonAndStart(user.UserId, nn);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthBootstrapper] AutoGuest 실패: {e.Message}");
                ShowLoginUI();
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// GPGS 무팝업 자동 인증 → Firebase 연동 → 단일 세션 시작
    /// </summary>
    private Task TryAutoGpgsAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        GooglePlayGames.PlayGamesPlatform.Instance.Authenticate(status =>
        {
            if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
            {
                ShowLoginUI(); tcs.TrySetResult(false); return;
            }

            // 서버사이드 코드 우선, 실패 시 ID Token 폴백
            try
            {
                GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                {
                    if (!string.IsNullOrEmpty(code))
                    {
                        var cred = Firebase.Auth.PlayGamesAuthProvider.GetCredential(code);
                        FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(cred).ContinueWith(async t =>
                        {
                            if (t.IsFaulted || t.IsCanceled) { ShowLoginUI(); tcs.TrySetResult(false); return; }
                            var fbUser = FirebaseAuth.DefaultInstance.CurrentUser;
                            string nick = Social.localUser?.userName ?? fbUser?.DisplayName ?? defaultNick;

                            // ★ 단일 세션 시작
                            var enf = FindObjectOfType<SessionEnforcer>(true);
                            if (enf != null)
                            {
                                var ok = await enf.StartForUidAsync(fbUser.UserId);
                                if (!ok) { ShowLoginUI(); tcs.TrySetResult(false); return; }
                            }

                            AuthAccount.Remember("gpgs", fbUser.UserId, nick);
                            ApplyPhotonAndStart(fbUser.UserId, nick);
                            tcs.TrySetResult(true);
                        });
                    }
                    else
                    {
                        // ID Token 폴백
                        TryIdTokenFallback(tcs);
                    }
                });
            }
            catch { TryIdTokenFallback(tcs); }
        });

        return tcs.Task;
    }

    private void TryIdTokenFallback(TaskCompletionSource<bool> tcs)
    {
        try
        {
            var active =
 (Social.Active as GooglePlayGames.PlayGamesPlatform) ?? GooglePlayGames.PlayGamesPlatform.Instance;
            var mi =
 active?.GetType().GetMethod("GetIdToken", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            string idToken = mi != null ? (mi.Invoke(active, null) as string) : null;

            if (!string.IsNullOrEmpty(idToken))
            {
                var cred = Firebase.Auth.GoogleAuthProvider.GetCredential(idToken, null);
                FirebaseAuth.DefaultInstance.SignInWithCredentialAsync(cred).ContinueWith(async t =>
                {
                    if (t.IsFaulted || t.IsCanceled) { ShowLoginUI(); tcs.TrySetResult(false); return; }
                    var fbUser = FirebaseAuth.DefaultInstance.CurrentUser;
                    string nick = Social.localUser?.userName ?? fbUser?.DisplayName ?? defaultNick;

                    // ★ 단일 세션 시작
                    var enf = FindObjectOfType<SessionEnforcer>(true);
                    if (enf != null)
                    {
                        var ok = await enf.StartForUidAsync(fbUser.UserId);
                        if (!ok) { ShowLoginUI(); tcs.TrySetResult(false); return; }
                    }

                    AuthAccount.Remember("gpgs", fbUser.UserId, nick);
                    ApplyPhotonAndStart(fbUser.UserId, nick);
                    tcs.TrySetResult(true);
                });
            }
            else { ShowLoginUI(); tcs.TrySetResult(false); }
        }
        catch { ShowLoginUI(); tcs.TrySetResult(false); }
    }
#endif

        /// <summary>
        /// Photon 인증값/닉네임 설정 → 네트워크/게임 부트 이어주기
        /// </summary>
        private void ApplyPhotonAndStart(string uid, string nickname)
        {
            PhotonNetwork.NickName = string.IsNullOrEmpty(nickname) ? defaultNick : nickname;

            // Photon UserId는 AuthenticationValues에 주입(서버에서 식별용)
            PhotonNetwork.AuthValues = new Photon.Realtime.AuthenticationValues(uid);

            // (선택) 바로 연결/로비 진입이 GameBootstrap/NetworkManager에 숨어 있다면 해당 진입점을 생성
            Util_LDH.ConsoleLog(this, "------------Game Start Bootstrap을 만듭니다. -----------");
            new GameObject("GameStartBootstrap", typeof(GameStartBootstrap));

            if (verbose) Debug.Log($"[AuthBootstrapper] Ready → UID={uid}, Nick={nickname}");
        }

        /// <summary>로그인 UI(게스트/GPGS 선택창)를 띄웁니다.</summary>
        private void ShowLoginUI()
        {
            var ui = FindObjectOfType<KYG.GuestLoginUI>(true);
            if (ui) ui.ShowLoginChoice();
            else Debug.LogWarning("[AuthBootstrapper] GuestLoginUI를 찾지 못했습니다.");
        }

        /// <summary>
        /// 전역 네트워크 보강 오브젝트들 자동 보장:
        /// - UidPersistenceGuard (LocalPlayer uid 누락 자동 보정)
        /// - PlayerDirectory + PlayerDirectoryBinder (UID ↔ Player 매핑)
        /// - SessionEnforcer, SessionKickGuard (단일 세션 + 킥 오버레이)
        /// </summary>
        private void EnsureGlobalNetGlueObjects()
        {
            // 1) UID 보정 가드
            if (FindObjectOfType<UidPersistenceGuard>(true) == null)
            {
                var go = new GameObject("UidPersistenceGuard", typeof(UidPersistenceGuard));
                DontDestroyOnLoad(go);
            }

            // 2) PlayerDirectory + Binder
            if (FindObjectOfType<KYG.Net.PlayerDirectory>(true) == null)
            {
                var dir = new GameObject("PlayerDirectory", typeof(KYG.Net.PlayerDirectory));
                DontDestroyOnLoad(dir);
            }

            if (FindObjectOfType<PlayerDirectoryBinder>(true) == null)
            {
                var bd = new GameObject("PlayerDirectoryBinder", typeof(PlayerDirectoryBinder));
                DontDestroyOnLoad(bd);
            }

            // 3) SessionEnforcer
            if (FindObjectOfType<SessionEnforcer>(true) == null)
            {
                var se = new GameObject("SessionEnforcer", typeof(SessionEnforcer));
                DontDestroyOnLoad(se);
                // 에디터에서 SessionEnforcer.databaseUrl 필드에 URL 지정해두면 좋아요.
            }

            // 4) SessionKickGuard (오버레이 프리팹은 인스펙터에서 할당)
            if (FindObjectOfType<SessionKickGuard>(true) == null)
            {
                var kg = new GameObject("SessionKickGuard", typeof(SessionKickGuard));
                DontDestroyOnLoad(kg);
                // 인스펙터에서 overlayPrefab 슬롯에 SessionKickOverlay 프리팹을 넣으세요.
            }
        }
    }
}