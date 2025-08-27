using UnityEngine;
using UnityEngine.Video;

namespace LDH_MainGame
{
    [CreateAssetMenu(fileName="MiniGameInfo", menuName="Game/MiniGame Info")]
    public class MiniGameInfo : ScriptableObject
    {
        [Header("ID & Scene")]
        public string id;                    // RoomProps에 기록되는 키
        public string sceneName;             // Additive 로드 씬 이름

        [Header("Display")]
        public string gameName;
        [TextArea] public string description;
        public Sprite sprite;

        [Header("Tutorial (옵션)")]
        public VideoClip tutorialVideo;      // 영상형 튜토리얼
        public GameObject tutorialPanelPrefab; // 패널형 튜토리얼(UI 프리팹)
        public string practiceSceneName;     // 연습 모드가 따로 있으면
        
        
    }
}