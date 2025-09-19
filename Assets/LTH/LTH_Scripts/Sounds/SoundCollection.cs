using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Sound", menuName = "Audio/Sound Collection")]
public class SoundCollection : MonoBehaviour
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
}
