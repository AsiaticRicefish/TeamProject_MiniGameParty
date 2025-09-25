using UnityEngine;

[CreateAssetMenu(menuName = "UI/Loading Theme", fileName = "NewLoadingTheme")]
public class UI_LoadingTheme : ScriptableObject
{
    [Header("테마 식별")]
    public GameThemeType themeType;              
    public string themeName;                     // 표시용 이름

    [Header("기본 텍스트 (구글시트 미사용시)")]
    public string gameTitle;                     // 큰 제목
    [TextArea] public string smallDescription;   // 서브 설명

    [Header("배경 색")]
    public Color titlePanelColor = Color.black;         // 제목 색 (기본값: 검정색)
    public Color backgroundPanelColor = Color.white;    // 배경 색 (기본값: 흰색)
    public Color descriptionPanelColor = Color.white;   // 설명창 색 (기본값: 흰색)

    [Header("유니모 연출")]
    public GameObject[] unimoPrefabs;            // 랜덤 뽑기용 유니모 프리팹 리스트

    [Header("구글시트 연동 설정")]
    public bool useGoogleSheetsText = true;      // 구글시트 텍스트 사용 여부
    public string sheetCategory;                 // 구글시트에서 가져올 카테고리


    public string GetCategoryFromEnum()
    {
        return themeType switch
        {
            GameThemeType.Shooting => "Shooting",
            GameThemeType.Meteor => "Meteor",
            GameThemeType.Jenga => "Jenga",
            GameThemeType.Rhythm => "Rhythm",
            GameThemeType.Default => "Default",
            _ => "Default"
        }; 
    }

         // 실제 사용할 카테고리 반환 (수동 입력이 있으면 우선 사용)
    public string GetActiveCategory()
    {
        return string.IsNullOrEmpty(sheetCategory) ? GetCategoryFromEnum() : sheetCategory;
    }
}