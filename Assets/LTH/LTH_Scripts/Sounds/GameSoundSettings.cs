using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameSoundSettings", menuName = "Audio/Game Sound Settings")]
public class GameSoundSettings : ScriptableObject
{
    [Header("Game Information")]  // 사운드를 사용할 게임 정보
    public string gameName;
    public GameType gameType;

    [Header("Sound Collection")]
    public SoundCollection soundCollection;  // 사운드 데이터베이스

    [Header("Default Settings")]
    public string defaultBGM = "";
    [Range(0f, 1f)]
    public float bgmVolumeMultiplier = 1f;
    [Range(0f, 1f)]
    public float sfxVolumeMultiplier = 1f;

    [Header("Memory Management")]  // 메모리 관리 설정
    public bool autoUnloadOnSceneChange = true;
    public int maxCachedClips = 15;
}