#if UNITY_ANDROID || UNITY_IOS
#define ADMOB_ENABLED
#endif

using System;
using System.Threading.Tasks;
using UnityEngine;
#if ADMOB_ENABLED
using GoogleMobileAds;
using GoogleMobileAds.Api;
#endif

public static class AdMobService
{
    public static event Action OnRewardedLoaded;                 // 준비 완료
    public static event Action<string> OnRewardedLoadFailed;     // 로드 실패 (메시지)
    public static bool IsRewardedReady =>
#if ADMOB_ENABLED
        _rewardedAd != null && _rewardedAd.CanShowAd();
#else
        false;
#endif

#if ADMOB_ENABLED
    private static RewardedAd _rewardedAd;
    private static bool _initialized;
#endif

    /// <summary>
    /// 광고가 나와야 하는 씬 진입 시 1회 호출 (메인맵에서 사용할 예정)
    /// </summary>
    public static async Task InitializeAsync()
    {
#if ADMOB_ENABLED
        if (_initialized) return;

        // SDK 초기화
        var tcs = new TaskCompletionSource<bool>();
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[AdMob] SDK Initialized");
            tcs.SetResult(true);
        });
        await tcs.Task;
        _initialized = true;

        // 첫 광고 미리 로드
        await LoadRewardedAsync();
#else
        await Task.CompletedTask;
#endif
    }

#if ADMOB_ENABLED
    /// <summary>
    /// 리워드 광고 로드 (결과창 열리기 전에 로드를 해야 광고가 나온다)
    /// </summary>
    public static async Task<bool> LoadRewardedAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var adRequest = new AdRequest();

        RewardedAd.Load("ca-app-pub-3940256099942544/5224354917", adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"[AdMob] 보상 로드 실패 : {error}");
                    tcs.SetResult(false);
                    return;
                }
                Debug.Log("[AdMob] 보상 로드 성공");
                _rewardedAd = ad;

                // 닫히면 다음 기회 대비 재로드
                _rewardedAd.OnAdFullScreenContentClosed += () =>
                {
                    Debug.Log("[AdMob] Rewarded closed → reload");
                    _ = LoadRewardedAsync();
                };
                _rewardedAd.OnAdFullScreenContentFailed += (AdError e) =>
                {
                    Debug.LogWarning("[AdMob] Fullscreen open failed: " + e);
                };

                Debug.Log("[AdMob] 보상 로드 성공");
                OnRewardedLoaded?.Invoke();
                tcs.SetResult(true);
            });

        return await tcs.Task;
    }

    /// <summary>
    /// 광고 표시 성공적으로 표시되어 닫히면 true
    /// 끝까지 시청 시 onRewarded 콜백이 호출됨 (여기서 2배 지급 등 처리)
    /// </summary>
    public static async Task<bool> ShowRewardedAsync(Action<Reward> onRewarded)
    {
        if (_rewardedAd == null || !_rewardedAd.CanShowAd())
        {
            bool loaded = await LoadRewardedAsync();
            if (!loaded) return false;
        }

        var tcs = new TaskCompletionSource<bool>();

        _rewardedAd.Show(reward =>
        {
            // 유저가 끝까지 시청 → 보상 콜백
            onRewarded?.Invoke(reward);
        });

        // 화면이 닫히면 호출
        _rewardedAd.OnAdFullScreenContentClosed += () => tcs.SetResult(true);

        await tcs.Task;
        return true;
    }
#endif

}