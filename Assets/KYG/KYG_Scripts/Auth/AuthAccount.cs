using UnityEngine;
using Firebase.Auth;
using Photon.Pun;
using System.Threading.Tasks;

/// <summary>
/// [공용 유틸]
/// - 마지막 로그인 정보(게스트/구글, uid, 닉네임)를 PlayerPrefs로 보관/조회
/// - 로그아웃/연동해제 시 공통 정리 로직
/// </summary>

public static class AuthAccount
{
    // PlayerPrefs 키들(철자 바꾸면 기존 저장값이 안 불립니다!)
    private const string KEY_PROVIDER = "auth_last_provider"; // "guest" or "gpgs"
    private const string KEY_UID      = "auth_last_uid";
    private const string KEY_NICK     = "auth_last_nickname";

    public static void Remember(string provider, string uid, string nickname)
    {
        // 1) 마지막 로그인 방법/UID/닉네임을 로컬(기기) 저장
        PlayerPrefs.SetString(KEY_PROVIDER, provider ?? "");
        PlayerPrefs.SetString(KEY_UID, uid ?? "");
        PlayerPrefs.SetString(KEY_NICK, nickname ?? "");
        PlayerPrefs.Save(); // 즉시 저장
    }

    public static bool TryGet(out string provider, out string uid, out string nickname)
    {
        provider = PlayerPrefs.GetString(KEY_PROVIDER, "");
        uid      = PlayerPrefs.GetString(KEY_UID, "");
        nickname = PlayerPrefs.GetString(KEY_NICK, "");
        // 최소 조건: provider 존재
        return !string.IsNullOrEmpty(provider);
    }

    public static void ClearLocal()
    {
        PlayerPrefs.DeleteKey(KEY_PROVIDER);
        PlayerPrefs.DeleteKey(KEY_UID);
        PlayerPrefs.DeleteKey(KEY_NICK);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 앱의 "로그아웃" 버튼에서 호출하면 됩니다.
    /// - Photon 연결 해제
    /// - Firebase SignOut
    /// - GPGS도 함께 SignOut (로그인 옵션 전환 대비)
    /// - 로컬 저장 데이터 삭제
    /// - 필요 시 닉네임 예약 해제(게스트)
    /// </summary>
    public static async System.Threading.Tasks.Task SignOutAllAsync(bool releaseGuestNickname = false)
    {
        // ✅ [가장 먼저] 다음 씬 자동 로그인/접속 1회 차단
        AuthAutoSuppressor.MarkOnce();

        // (선택) 세션 감시 중지 등 현재 연결 정리
        var enf = UnityEngine.Object.FindObjectOfType<SessionEnforcer>(true);
        if (enf != null) { try { enf.StopAll(); } catch {} }

        // Photon 끊기
        try
        {
            if (Photon.Pun.PhotonNetwork.IsConnected || Photon.Pun.PhotonNetwork.IsConnectedAndReady)
                Photon.Pun.PhotonNetwork.Disconnect();
        } catch {}

        // Firebase SignOut
        try { Firebase.Auth.FirebaseAuth.DefaultInstance?.SignOut(); } catch {}

        // (Android) GPGS SignOut
#if UNITY_ANDROID && !UNITY_EDITOR
    // try { GooglePlayGames.PlayGamesPlatform.Instance?.SignOut(); } catch {}
#endif

        // 로컬 자동로그인 정보/닉네임 캐시 등 지우기
        ClearLocal();

        // (옵션) 게스트 닉네임 반환
        if (releaseGuestNickname)
        {
            try
            {
                var u = Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser;
                if (u != null && u.IsAnonymous && !string.IsNullOrEmpty(u.DisplayName))
                    await NicknameRegistry.ReleaseIfOwnerAsync(u.UserId, u.DisplayName);
            } catch {}
        }
    }
}

