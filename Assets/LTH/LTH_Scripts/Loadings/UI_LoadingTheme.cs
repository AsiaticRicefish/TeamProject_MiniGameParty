using UnityEngine;

[CreateAssetMenu(menuName = "UI/Loading Theme", fileName = "NewLoadingTheme")]
public class UI_LoadingTheme : ScriptableObject
{
    [Header("기본 정보")]
    public string gameTitle;                    // 미니게임 이름
    [TextArea] public string gameDescription;   // 미니게임 설명

    [Header("비주얼")]
    public Sprite background;                   // 배경 이미지
    public Color progressColor = Color.white;   // 로딩 원 색상

    [Header("유니모 연출")]
    public GameObject[] unimoPrefabs;   // 랜덤으로 뽑힐 유니모 프리팹 리스트
}
