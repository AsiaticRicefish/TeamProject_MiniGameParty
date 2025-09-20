using System.Collections;
using System.Collections.Generic;
using Firebase.Auth;
using UnityEngine;
using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// 사운드 기본 데이터 구조

[Serializable]
public class SoundData
{
    public string soundName;
    public AssetReference audioClip;        // Addressable Asset으로 사운드 로드
    
    [Range(0f, 1f)]
    public float defaultVolume = 1f;        // 기본 볼륨 설정

    public bool isLoop = false;
    public bool preloadOnStart = false;     // 시작 시 미리 로드
    public bool keepInMemory = false;       // 메모리에 계속 유지
}