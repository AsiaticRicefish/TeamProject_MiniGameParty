using System.Collections;
using System.Collections.Generic;
using Firebase.Auth;
using UnityEngine;
using System;

// 사운드 기본 데이터 구조

[Serializable]
public class SoundData
{
    public string soundName;
    public AudioClip audioClip;
    [Range(0f, 1f)]
    public float defaultVolume = 1f;
    public bool isLoop = false;
}