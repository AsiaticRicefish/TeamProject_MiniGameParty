using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using LDH_UI;
using UnityEngine;
using UnityEngine.Networking;

public class GoogleSheetsLoadingManager : CombinedSingleton<GoogleSheetsLoadingManager>
{
    [Header("구글 시트 설정")]
    [SerializeField] private string sheetURL = ""; // CSV 공개 링크
    [SerializeField] private float refreshInterval = 300f; // 5분마다 갱신
    [SerializeField] private bool loadOnStart = true; // 게임 시작시 자동 로딩

    private Dictionary<string, List<LoadingTextData>> categorizedData = new Dictionary<string, List<LoadingTextData>>();
    private List<LoadingTextData> allLoadingData = new List<LoadingTextData>();
    private bool isDataReady = false;
    private bool isLoading = false;

    protected override void OnAwake()
    {
        if (loadOnStart)
        {
            StartCoroutine(LoadDataFromSheet());
        }
    }

    void Start()
    {
        // 주기적으로 데이터 갱신
        if (refreshInterval > 0)
        {
            InvokeRepeating(nameof(RefreshData), refreshInterval, refreshInterval);
        }
    }

    private void RefreshData()
    {
        StartCoroutine(LoadDataFromSheet());
    }

    private IEnumerator LoadDataFromSheet()
    {
        if (isLoading) yield break; // 중복 로딩 방지

        isLoading = true;
        Debug.Log("구글시트에서 로딩 데이터 가져오는 중...");

        using (UnityWebRequest request = UnityWebRequest.Get(sheetURL))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ParseCSVData(request.downloadHandler.text);
                isDataReady = true;
                Debug.Log($"로딩 데이터 갱신 완료: {allLoadingData.Count}개");
            }
            else
            {
                Debug.LogError($"시트 로딩 실패: {request.error}");
                // 실패시 기본 데이터 사용
                if (allLoadingData.Count == 0)
                {
                    LoadDefaultData();
                }
            }
        }

        isLoading = false;
    }

    private void ParseCSVData(string csvData)
    {
        allLoadingData.Clear();
        categorizedData.Clear();
        string[] lines = csvData.Split('\n');

        // 첫 번째 줄은 헤더이므로 스킵 (제목, 내용, 게임) 또는 (gameTitle, smallDescription, category)
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i].Trim())) continue;

            string[] values = lines[i].Split(',');
            if (values.Length >= 2)
            {
                string category = values.Length >= 3 ? values[2].Trim().Trim('"') : "default";

                var data = new LoadingTextData(
                    values[0].Trim().Trim('"'), // 제목 (gameTitle)
                    values[1].Trim().Trim('"'), // 내용 (smallDescription)
                    category                    // 지정 (category)
                );

                allLoadingData.Add(data);

                // 카테고리별로 분류
                if (!categorizedData.ContainsKey(category))
                {
                    categorizedData[category] = new List<LoadingTextData>();
                }
                categorizedData[category].Add(data);
            }
        }

        Debug.Log($"로딩 데이터 파싱 완료: 총 {allLoadingData.Count}개, 카테고리 {categorizedData.Count}개");
    }

    private void LoadDefaultData()
    {
        var defaultData = new List<LoadingTextData>
        {
            new LoadingTextData("로딩 중...", "잠시만 기다려주세요", "Default"),
            new LoadingTextData("곧 시작해요", "준비 완료까지 조금만 더!", "Default"),
            new LoadingTextData("젠가스타", "젠가 블럭을 타이밍에 맞워서 빠르게 점수를 쌓아보세요!", "Jenga"),
            new LoadingTextData("날아라 유니모", "타이밍에 맞춰 유니모를 날려주세요!", "Shooting"),
            new LoadingTextData("세어라 별똥별", "떨어지는 운석을 조심하면서, 별똥별을 세어보세요", "Meteor"),
            new LoadingTextData("스타 이스케이프", "노트를 최대한 정확하게 맞춰서 1등을 노려보도록 하세요!", "Rhythm"),
        };

        allLoadingData = defaultData;
        categorizedData.Clear();

        foreach (var data in defaultData)
        {
            if (!categorizedData.ContainsKey(data.category))
            {
                categorizedData[data.category] = new List<LoadingTextData>();
            }
            categorizedData[data.category].Add(data);
        }

        isDataReady = true;
    }

    public LoadingTextData GetRandomLoadingData(string category = "Default")
    {
        if (!isDataReady)
        {
            Debug.LogWarning($"데이터가 준비되지 않음. 기본값 반환: {category}");
            return new LoadingTextData("로딩 중...", "잠시만 기다려주세요", category);
        }

        // 해당 카테고리 데이터가 있으면 그것에서 선택
        if (categorizedData.ContainsKey(category) && categorizedData[category].Count > 0)
        {
            var categoryList = categorizedData[category];
            int randomIndex = Random.Range(0, categoryList.Count);
            Debug.Log($"카테고리 '{category}' 데이터 {categoryList.Count}개 중 {randomIndex}번째 선택");
            return categoryList[randomIndex];
        }

        // 카테고리가 없으면 경고 후 전체에서 선택
        Debug.LogWarning($"카테고리 '{category}' 없음! 사용 가능한 카테고리: [{string.Join(", ", categorizedData.Keys)}]");

        if (allLoadingData.Count > 0)
        {
            int randomIndex = Random.Range(0, allLoadingData.Count);
            return allLoadingData[randomIndex];
        }

        // 완전히 데이터가 없으면 기본값
        return new LoadingTextData("로딩 중...", "잠시만 기다려주세요", category);
    }

    public List<string> GetAvailableCategories()
    {
        return new List<string>(categorizedData.Keys);
    }

    public bool IsDataReady => isDataReady;
}