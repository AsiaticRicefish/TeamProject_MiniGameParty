using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds;
using GoogleMobileAds.Api;

public class AdMobInitializer : MonoBehaviour
{
    private RewardedAd _rewardedAd;

    private void Start()
    {
        // SDK 초기화
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[AdMob] SDK Initialized");
        });

        // 리워드 광고 로드
        LoadRewarded();
    }

    private void LoadRewarded()
    {
        var adRequest = new AdRequest(); // 광고 요청

        RewardedAd.Load("ca-app-pub-3940256099942544/5224354917", adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"[AdMob] Rewarded load failed: {error}");
                    return;
                }
                Debug.Log("[AdMob] Rewarded loaded successfully");
                _rewardedAd = ad;

                // 닫히면 다시 로드하도록 이벤트 등록
                _rewardedAd.OnAdFullScreenContentClosed += () =>
                {
                    Debug.Log("[AdMob] Rewarded closed → reload");
                    LoadRewarded();
                };
            });
    }

    public void ShowRewarded()
    {
        if (_rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _rewardedAd.Show(reward =>
            {
                Debug.Log($"[AdMob] User rewarded: {reward.Type} x {reward.Amount}");
            });
        }
        else
        {
            Debug.LogWarning("[AdMob] Rewarded not ready yet");
        }
    }
}
