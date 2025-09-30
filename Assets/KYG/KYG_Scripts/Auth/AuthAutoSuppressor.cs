using UnityEngine;

/// <summary>
/// [중요] 다음 씬에서 자동 로그인/자동 접속(부트스트랩)을 "정확히 1회" 막는 스위치.
/// - 로그아웃 직후 MarkOnce() 호출
/// - 로그인/타이틀 씬의 부트스트랩들은 Consume()을 가장 먼저 호출해 true면 즉시 중단
/// - PlayerPrefs에도 기록하여 씬 전환/로드간에도 유효
/// </summary>
public static class AuthAutoSuppressor
{
    private const string PREF_KEY_ONCE = "KYG.Auth.AutoSuppress.Once";
    private static bool _onceInMemory; // 씬 안에서 빠른 체크용

    /// <summary>다음 씬에서 자동로그인/자동접속을 1회 차단한다.</summary>
    public static void MarkOnce()
    {
        _onceInMemory = true;
        PlayerPrefs.SetInt(PREF_KEY_ONCE, 1);
        PlayerPrefs.Save();
        Debug.Log("[AuthAutoSuppressor] MarkOnce");
    }

    /// <summary>
    /// 차단 플래그가 켜져 있는지 즉시 확인(소비하지 않음).
    /// - 부트스트랩 초기화 전에 가볍게 참조할 때 사용
    /// </summary>
    public static bool IsSuppressed()
    {
        return _onceInMemory || PlayerPrefs.GetInt(PREF_KEY_ONCE, 0) == 1;
    }

    /// <summary>
    /// 차단 플래그를 "소비"한다. true면 이번 프레임에 자동로그인을 하지 말고 곧바로 리턴.
    /// - 부트스트랩 시작(보통 Awake/Start 첫 줄)에서 반드시 호출
    /// </summary>
    public static bool Consume()
    {
        bool blocked = IsSuppressed();
        if (blocked)
        {
            _onceInMemory = false;
            PlayerPrefs.DeleteKey(PREF_KEY_ONCE);
            Debug.Log("[AuthAutoSuppressor] Consumed (auto-login suppressed)");
        }
        return blocked;
    }

    /// <summary>디버그/예외 복구용. (보통 필요 없음)</summary>
    public static void Clear()
    {
        _onceInMemory = false;
        PlayerPrefs.DeleteKey(PREF_KEY_ONCE);
    }
}