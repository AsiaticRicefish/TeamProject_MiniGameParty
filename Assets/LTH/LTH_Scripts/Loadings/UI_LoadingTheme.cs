using UnityEngine;

[CreateAssetMenu(menuName = "UI/Loading Theme", fileName = "NewLoadingTheme")]
public class UI_LoadingTheme : ScriptableObject
{
    [Header("텍스트")]
    public string gameTitle;                     // 큰 제목
    public string bigDescription;                // 메인 설명 (큰 글씨)
    [TextArea] public string smallDescription;   // 서브 설명 (작은 글씨)

    [Header("배경 색")]
    public Color titlePanelColor = Color.black;         // 제목 색 (기본값: 검정색)
    public Color backgroundPanelColor = Color.white;    // 배경 색 (기본값: 흰색)
    public Color descriptionPanelColor = Color.white;   // 설명창 색 (기본값: 흰색)

    [Header("유니모 연출")]
    public GameObject[] unimoPrefabs;            // 랜덤 뽑기용 유니모 프리팹 리스트
}