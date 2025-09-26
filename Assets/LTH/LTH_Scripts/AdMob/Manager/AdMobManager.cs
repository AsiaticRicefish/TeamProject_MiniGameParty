#if UNITY_ANDROID || UNITY_IOS
#define ADMOB_ENABLED
#endif

using System;
using System.Threading.Tasks;
using UnityEngine;
using DesignPattern;

#if ADMOB_ENABLED
using GoogleMobileAds;
using GoogleMobileAds.Api;
#endif

public class AdMobManager : CombinedSingleton<AdMobManager>
{
    public event Action OnRewardedLoaded;                 // 준비 완료
    public event Action<string> OnRewardedLoadFailed;     // 로드 실패 (메시지)

    public bool IsRewardedReady =>
#if ADMOB_ENABLED
        _rewardedAd != null && _rewardedAd.CanShowAd();
#else
        false;
#endif

#if ADMOB_ENABLED
    private RewardedAd _rewardedAd;
    private bool _isInitialized;
    private bool _isLoading = false;
#endif

    protected override void OnAwake()
    {
        isPersistent = true;
    }

    private async void Start()
    {
        // 항상 자동 초기화
        await InitializeAsync();
    }

    /// <summary>
    /// AdMob SDK 초기화 및 첫 광고 로드
    /// </summary>
    public async Task InitializeAsync()
    {
#if ADMOB_ENABLED
        if (_isInitialized) return;

        Debug.Log("[AdMob] Initializing SDK...");

        // SDK 초기화
        var tcs = new TaskCompletionSource<bool>();
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[AdMob] SDK Initialized");
            tcs.SetResult(true);
        });
        await tcs.Task;

        _isInitialized = true;

         await LoadRewardedAsync();
#else
        Debug.Log("[AdMob] Mock initialization (Editor)");
        await Task.CompletedTask;
#endif
    }

#if ADMOB_ENABLED
    /// <summary>
    /// 리워드 광고 로드
    /// </summary>
    public async Task<bool> LoadRewardedAsync()
    {
        if (_isLoading)
        {
            Debug.Log("[AdMob] Already loading, waiting...");
            // 타임아웃 추가로 무한 대기 방지
            int waitCount = 0;

            while (_isLoading && waitCount < 100) // 10초 타임아웃
            {
                await Task.Delay(100);
                waitCount++;
            }

            if (_isLoading)
            {
                Debug.LogWarning("[AdMob] Load timeout, resetting...");
                _isLoading = false;
                _rewardedAd = null;
            }

            return IsRewardedReady;
        }

        if (IsRewardedReady)
        {
            Debug.Log("[AdMob] Ad already ready");
            return true;
        }

        _isLoading = true;
        Debug.Log("[AdMob] Starting to load rewarded ad...");

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
            if (_rewardedAd != null)
            {
                Debug.Log("[AdMob] Destroying previous ad");
                _rewardedAd.Destroy();
            }

            _rewardedAd = ad;

            // 실패 이벤트만 연결
            _rewardedAd.OnAdFullScreenContentFailed += OnAdFailed;

            Debug.Log("[AdMob] Rewarded loaded successfully");
            OnRewardedLoaded?.Invoke();
            tcs.SetResult(true);
        });

        return await tcs.Task;
    }

    private void OnAdFailed(AdError error)
    {
        Debug.LogError($"[AdMob] Fullscreen failed: {error}");
        _rewardedAd = null;
    }

    /// <summary>
    /// 리워드 광고 표시
    /// </summary>
    public async Task<bool> ShowRewardedAsync(Action<Reward> onRewarded)
    {
        if (!IsRewardedReady)
        {
            Debug.LogWarning("[AdMob] Rewarded not ready, try preload...");
            bool loaded = await LoadRewardedAsync();
            if (!loaded || !IsRewardedReady)
            {
                Debug.LogError("[AdMob] Failed to load ad for show");
                return false;
            }
        }

        var tcs = new TaskCompletionSource<bool>();

        try
        {
            Debug.Log("[AdMob] Attempting to show rewarded ad...");

            if (!_rewardedAd.CanShowAd())
            {
                Debug.LogWarning("[AdMob] CanShowAd returned false");
                return false;
            }

            // 일회용 닫힘 핸들러
            Action closedHandler = null;
            closedHandler = () =>
            {
                Debug.Log("[AdMob] Ad closed, cleaning up...");

                if (_rewardedAd != null)
                {
                    _rewardedAd.OnAdFullScreenContentClosed -= closedHandler;
                }

                _rewardedAd = null;
                tcs.SetResult(true);
            };

            _rewardedAd.OnAdFullScreenContentClosed += closedHandler;
            _rewardedAd.Show(reward =>
            {
                Debug.Log($"[AdMob] Reward received: {reward.Type}, Amount: {reward.Amount}");
                onRewarded?.Invoke(reward);
            });

            Debug.Log("[AdMob] Show() called successfully");
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

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            Debug.Log("[AdMob] App resumed - checking ad status");
        }
    }

#endif

    // 디버깅용 메서드들
    [ContextMenu("Force Load Ad")]
    public void ForceLoadAd()
    {
        _ = LoadRewardedAsync();
    }

    [ContextMenu("Reset AdMob State")]
    public void ResetState()
    {
#if ADMOB_ENABLED
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
        _isLoading = false;
#endif
        Debug.Log("[AdMob] State reset");
    }

}