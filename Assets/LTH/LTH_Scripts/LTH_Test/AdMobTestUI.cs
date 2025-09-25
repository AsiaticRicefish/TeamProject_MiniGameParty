using GoogleMobileAds;
using GoogleMobileAds.Api;
using TMPro;
using UnityEngine;

public class AdMobTestUI : MonoBehaviour
{
    private RewardedAd _rewardedAd;
    private int _rewardCount = 0;

    [SerializeField] private TMP_Text rewardText; // 보상 표시용 텍스트

    void Start()
    {
        rewardText.text = $"Reward: {_rewardCount}";
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[AdMobTest] SDK Initialized");
        });
    }

    public void LoadRewarded()
    {
        var adRequest = new AdRequest();

        RewardedAd.Load("ca-app-pub-3940256099942544/5224354917", adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null)
                {
                    Debug.LogError("[AdMobTest] Failed to load: " + error);
                    return;
                }

                Debug.Log("[AdMobTest] Rewarded loaded!");
                _rewardedAd = ad;

                _rewardedAd.OnAdFullScreenContentClosed += () =>
                {
                    Debug.Log("[AdMobTest] Closed → reload");
                    _rewardedAd = null;
                };
            });
    }

    public void ShowRewarded()
    {
        if (_rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _rewardedAd.Show(reward =>
            {
                // 광고 끝까지 봤을 때 → 보상 카운트 증가
                _rewardCount += (int)reward.Amount;
                rewardText.text = $"Reward: {_rewardCount}";

                Debug.Log($"[AdMobTest] Reward: {reward.Type} x {reward.Amount}");
            });
        }
        else
        {
            Debug.LogWarning("[AdMobTest] No rewarded ad loaded yet!");
        }
    }
}
