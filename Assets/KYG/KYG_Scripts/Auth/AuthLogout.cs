using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Firebase.Auth;
using GooglePlayGames;
using UnityEngine.SocialPlatforms;

/// <summary>
/// "한 번 클릭"으로 모든 로그인/네트워크 상태를 안전하게 끊는 유틸.
/// - 자동접속 억제 플래그 → 진행중인 로그인/부트스트랩 취소 → Photon 완전 끊기 대기
/// - 닉네임 예약 해제(게스트) → GPGS SignOut → Firebase SignOut → 로컬 기억값 삭제
/// - 마지막으로 로그인 화면으로 이동(프로젝트에 맞게 바꾸세요)
/// </summary>
public static class AuthLogout
{
    // 씬/다른 스크립트의 재접속 로직이 개입하지 못하게 하는 전역 가드
    public static bool IsLoggingOut { get; private set; }

    public static async Task LogoutNowAsync(bool releaseGuestNickname = true, string loginSceneName = "Login Scene")
    {
        if (IsLoggingOut) return;
        IsLoggingOut = true;

        try
        {
            // 0) 다음 실행(또는 다음 씬)에서 "자동 로그인/자동 접속"을 1회 막음
            //    ※ 이 메서드는 프로젝트에 이미 있습니다(이전 안내 참고).
            AuthAutoSuppressor.MarkOnce(); // 다음 씬에서 재접속 시도 금지 (1회)  :contentReference[oaicite:5]{index=5}

            // 1) 현재 Firebase 사용자/닉네임 확보 (게스트 예약 해제에 사용)
            var auth = FirebaseAuth.DefaultInstance;
            var user = auth?.CurrentUser;
            string uid  = user?.UserId ?? "";
            string nick = user?.DisplayName ?? "";

            // 2) 진행 중인 게스트 로그인/닉네임 예약 과정이 있으면 "취소" (UI/예약/Photon 연결 정리)
            //    GuestLoginUI의 취소 버튼이 내부적으로 아래 메서드를 호출합니다.
            var glm = UnityEngine.Object.FindObjectOfType<KYG.Auth.GuestLoginManager>();
            if (glm != null)
            {
                try { glm.CancelPendingLogin(); } catch { /* no-op */ }
            }
            // ↑ 내부에서 닉네임 예약 해제 시도 + PhotonNetwork.Disconnect 호출 + UI 원복까지 처리합니다.  :contentReference[oaicite:6]{index=6} :contentReference[oaicite:7]{index=7}

            // 3) GameBootstrap 같이 "접속을 다시 시도"하는 오브젝트를 모두 제거해서 재부팅 차단
            foreach (var gb in UnityEngine.Object.FindObjectsOfType<GameBootstrap>()) // 타입은 프로젝트 클래스명 유지
                UnityEngine.Object.Destroy(gb.gameObject);
            // (GPGSLoginManager/GuestLoginManager가 GameBootstrap을 생성해 접속을 이어갑니다. 이것부터 끊어줘야 재연결 레이스가 안 납니다.) :contentReference[oaicite:8]{index=8} :contentReference[oaicite:9]{index=9}

            // 4) Photon 완전 끊기 (연결 중/룸 중/로비 중 모두 포함)
            try { PhotonNetwork.Disconnect(); } catch { /* ignore */ }
            // 상태가 Disconnected가 될 때까지 잠깐 대기
            var t0 = DateTime.UtcNow;
            while (PhotonNetwork.IsConnected || PhotonNetwork.IsConnectedAndReady)
            {
                await Task.Delay(50);
                if ((DateTime.UtcNow - t0).TotalSeconds > 5) break; // 안전 타임아웃
            }

            // 5) (게스트만) 닉네임 전역 예약 해제
            //    - 지금 내가 주인(uid)이면만 삭제되도록 안전 트랜잭션 처리되어 있습니다.
            if (releaseGuestNickname && !string.IsNullOrEmpty(uid) && user != null && user.IsAnonymous && !string.IsNullOrEmpty(nick))
            {
                try { await NicknameRegistry.ReleaseIfOwnerAsync(uid, nick); } catch { /* ignore */ }
                // 닉네임 키는 OnDisconnect 로도 제거되지만, 수동으로 한 번 더 보장합니다.  :contentReference[oaicite:10]{index=10}
            }

            // 6) GPGS 세션이 있으면 먼저 SignOut (안드로이드 기기에서만 실제 효과)
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var pg = PlayGamesPlatform.Instance as PlayGamesPlatform;
                if (pg != null) pg.SignOut();
            }
            catch { /* ignore */ }
#endif
            // (GPGS 인증 흐름은 프로젝트에 이미 존재 - 로그인은 GPGSLoginManager 참고)  :contentReference[oaicite:11]{index=11} :contentReference[oaicite:12]{index=12}

            // 7) Firebase 계정에서 로그아웃 (익명/구글 공통)
            try { auth?.SignOut(); } catch { /* ignore */ }

            // 8) 로컬 “마지막 로그인 정보/자동로그인 기억값” 삭제
            try { AuthAccount.ClearLocal(); } catch { /* ignore */ }  // Remember로 저장했던 provider/uid/nickname 정리  :contentReference[oaicite:13]{index=13}

            // 9) (선택) PlayerPrefs 강제 저장
            try { PlayerPrefs.Save(); } catch { /* ignore */ }

            // 10) 로그인 씬으로 이동 (프로젝트 씬 이름에 맞게 변경)
            if (!string.IsNullOrEmpty(loginSceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(loginSceneName);
        }
        finally
        {
            // 씬 전환 직후 자동접속 로직이 있더라도 suppressor가 1회 막아줍니다.
            IsLoggingOut = false;
        }
    }
}
