using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "SoundCollection", menuName = "Audio/Sound Collection")]
public class SoundCollection : ScriptableObject
{
    [Header("BGM")]
    public SoundData[] bgmSounds;

    [Header("SFX")]
    public SoundData[] sfxSounds;

    public SoundData GetBGM(string soundName)
    {
        return Array.Find(bgmSounds, sound => sound.soundName == soundName);
    }

    public SoundData GetSFX(string soundName)
    {
        return Array.Find(sfxSounds, sound => sound.soundName == soundName);
    }

    // 프리로드할 사운드들 반환
    public SoundData[] GetPreloadSounds()
    {
        var preloadList = new List<SoundData>();

        foreach (var bgm in bgmSounds)
            if (bgm.preloadOnStart) preloadList.Add(bgm);

        foreach (var sfx in sfxSounds)
            if (sfx.preloadOnStart) preloadList.Add(sfx);

        return preloadList.ToArray();
    }
}
