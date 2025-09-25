using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class AdUnitIds
{
    public string rewarded;
}

[Serializable]
public class AdMobConfigData
{
    public AdUnitIds android;
    public AdUnitIds ios;
}

public static class AdMobConfig
{
    private static AdMobConfigData _config;

    public static void Load()
    {
        if (_config != null) return;

        TextAsset json = Resources.Load<TextAsset>("admob_config");
        if (json == null)
        {
            Debug.LogError("[AdMobConfig] admob_config.json 파일이 존재하지 않습니다!");
            return;
        }

        _config = JsonUtility.FromJson<AdMobConfigData>(json.text);
    }

    public static string GetRewardedId()
    {
        Load();
#if UNITY_ANDROID
        return _config.android.rewarded;
#elif UNITY_IOS
        return _config.ios.rewarded;
#else
        return "unused";
#endif
    }
}