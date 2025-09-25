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
    public static event Action OnRewardedLoaded;             // 준비 완료
    public static event Action<string> OnRewardedLoadFailed; // 로드 실패 (메시지)

    public static bool IsRewardedReady =>
#if ADMOB_ENABLED
        _rewardedAd != null && _rewardedAd.CanShowAd();
#else
        false;
#endif

#if ADMOB_ENABLED
    private static RewardedAd _rewardedAd;
    private static bool _initialized;
    private static bool _isLoading = false;
#endif

    /// <summary>
    /// 광고가 나와야 하는 씬 진입 시 1회 호출
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
        if (_isLoading)
        {
            // 이미 로딩 중이면 완료될 때까지 대기
            while (_isLoading)
            {
                await Task.Delay(100);
            }
            return IsRewardedReady;
        }

        if (IsRewardedReady) return true; // 이미 준비됨

        _isLoading = true;

        var tcs = new TaskCompletionSource<bool>();

        var adUnitId = AdMobConfig.GetRewardedId();
        if (string.IsNullOrEmpty(adUnitId) || adUnitId == "unused")
        {
            string msg = "[AdMob] Rewarded adUnitId missing (check admob_config.json)";
            Debug.LogError(msg);
            OnRewardedLoadFailed?.Invoke(msg);
            _isLoading = false;
            tcs.SetResult(false);
            return await tcs.Task;
        }

        var request = new AdRequest();

        RewardedAd.Load(adUnitId, request, (ad, error) =>
        {
            _isLoading = false;
            if (error != null)
            {
                string em = error.ToString();
                Debug.LogError($"[AdMob] Rewarded load failed: {em}");
                OnRewardedLoadFailed?.Invoke(em);
                tcs.SetResult(false);
                return;
            }

            // 기존 광고 객체 정리
            if (_rewardedAd != null) _rewardedAd.Destroy();

            _rewardedAd = ad;

            _rewardedAd.OnAdFullScreenContentFailed += OnAdFailed;

            Debug.Log("[AdMob] Rewarded loaded successfully");
            OnRewardedLoaded?.Invoke();
            tcs.SetResult(true);
        });

        return await tcs.Task;
    }

    #region Event Handlers

    // 광고 실패 이벤트 핸들러
    private static void OnAdFailed(AdError error)
    {
        Debug.LogError($"[AdMob] Fullscreen failed: {error}");
        _rewardedAd = null; // 현재 광고 무효화
    }
    #endregion

    /// <summary>
    /// 광고 표시. 성공적으로 표시되어 닫히면 true
    /// 끝까지 시청 시 onRewarded 콜백이 호출됨 (여기서 2배 지급 등 처리)
    /// </summary>
    public static async Task<bool> ShowRewardedAsync(Action<Reward> onRewarded)
    {
        if (!IsRewardedReady)
        {
            Debug.LogWarning("[AdMob] Rewarded not ready, try preload...");
            bool loaded = await LoadRewardedAsync();
            if (!loaded || !IsRewardedReady) return false;
        }

        var tcs = new TaskCompletionSource<bool>();

        try
        {
            Debug.Log("[AdMob] Attempting to show rewarded ad...");

            // Show 호출 전 마지막 체크
            if (!_rewardedAd.CanShowAd())
            {
                Debug.LogWarning("[AdMob] CanShowAd returned false");
                return false;
            }

            // 별도의 닫힘 핸들러 생성 (중복 방지)
            Action closedHandler = null;
            closedHandler = () =>
            {
                Debug.Log("[AdMob] Show completed, ad closed");

                // 핸들러 제거
                if (_rewardedAd != null)
                {
                    _rewardedAd.OnAdFullScreenContentClosed -= closedHandler;
                }

                // 광고 무효화
                _rewardedAd = null;

                tcs.SetResult(true);
            };

            // Show 전용 닫힘 핸들러 등록
            _rewardedAd.OnAdFullScreenContentClosed += closedHandler;

            _rewardedAd.Show(reward =>
            {
                Debug.Log($"[AdMob] Reward received: {reward.Type}, Amount: {reward.Amount}");
                onRewarded?.Invoke(reward);
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdMob] Show failed with exception: {ex.Message}");
            tcs.SetResult(false);
            return false;
        }

        bool result = await tcs.Task;
        Debug.Log($"[AdMob] ShowRewardedAsync completed with result: {result}");
        return result;
    }

#endif
}