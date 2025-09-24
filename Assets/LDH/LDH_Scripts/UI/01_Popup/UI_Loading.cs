using System;
using LDH_MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace LDH_UI
{
    public class UI_Loading : UI_Popup
    {
        [Header("Loading UI")]
        [SerializeField] private TMP_Text title;               // 큰 제목
        [SerializeField] private TMP_Text smallDescription;    // 서브 설명(작은 글씨)

        [Header("Common Visuals (Prefab handles colors/background)")]
        [SerializeField] private Image progressCircle;         // 원형 진행률 (fillAmount만 사용)
        [SerializeField] private Transform unimoAnchor;        // 유니모 애니메이션 위치

        [Header("Color Panels")]
        [SerializeField] private Image titlePanel;            // 제목 배경 패널
        [SerializeField] private Image backgroundPanel;       // 배경 패널
        [SerializeField] private Image descriptionPanel;      // 설명창 패널

        private GameObject _spawnedUnimo;
        
        // event
        public Action<Scene> onSceneLoaded;
        

        #region Unity Life Cycle / Scene Loaded

        protected override void Init()
        {
            base.Init();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void Clear()
        {
            base.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            onSceneLoaded?.Invoke(scene);
        }


        #endregion
       

        /// <summary>
        /// 테마 적용 (텍스트/유니모)
        /// </summary>
        public void ApplyTheme(UI_LoadingTheme theme)
        {
            if (!theme) return;

            SetTitlePanelColor(theme.titlePanelColor);
            SetBackgroundPanelColor(theme.backgroundPanelColor);
            SetDescriptionPanelColor(theme.descriptionPanelColor);

            SpawnRandomUnimo(theme.unimoPrefabs);

            if (theme.useGoogleSheetsText && GoogleSheetsLoadingManager.Instance != null)
            {
                // 구글시트에서 해당 카테고리 텍스트 가져오기
                string category = theme.GetActiveCategory();
                var textData = GoogleSheetsLoadingManager.Instance.GetRandomLoadingData(category);
                SetTitle(textData.gameTitle);
                SetSmallDescription(textData.smallDescription);
                Debug.Log($"구글시트 텍스트 적용: {theme.themeType} → '{category}' 카테고리");
            }
            else
            {
                // 테마의 고정 텍스트 사용
                SetTitle(theme.gameTitle);
                SetSmallDescription(theme.smallDescription);
                Debug.Log($"테마 고정 텍스트 적용: {theme.themeType}");
            }
        }

        /// <summary>
        /// 진행률 반영 (0~1)
        /// </summary>
        public void SetProgress(float t01)
        {
            if (progressCircle) progressCircle.fillAmount = Mathf.Clamp01(t01);
        }

        #region 텍스트 설정
        public void SetTitle(string text)
        {
            if (title) title.text = text ?? string.Empty;
        }
       
        public void SetSmallDescription(string text)
        {
            if (smallDescription) smallDescription.text = text ?? string.Empty;
        }
        #endregion


        #region 색상 설정
        public void SetTitlePanelColor(Color color)
        {
            if (titlePanel) titlePanel.color = color;
        }

        public void SetBackgroundPanelColor(Color color)
        {
            if (backgroundPanel) backgroundPanel.color = color;
        }

        public void SetDescriptionPanelColor(Color color)
        {
            if (descriptionPanel) descriptionPanel.color = color;
        }

        public void SetAllPanelColors(Color titleColor, Color backgroundColorColor, Color descriptionColor)
        {
            SetTitlePanelColor(titleColor);
            SetBackgroundPanelColor(backgroundColorColor);
            SetDescriptionPanelColor(descriptionColor);
        }

        #endregion

        #region 유니모 설정
        /// <summary>
        /// 특정 유니모 프리팹을 강제로 스폰(디버그/특수 케이스용)
        /// </summary>
        public void SpawnUnimo(GameObject prefab)
        {
            ClearSpawnedUnimo();
            if (!prefab || !unimoAnchor) return;

            _spawnedUnimo = Instantiate(prefab, unimoAnchor);
            var t = _spawnedUnimo.transform;
            t.localPosition = new Vector3(0, 0, 0);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one * 4;
        }

        /// <summary>
        /// 유니모 프리팹 풀에서 랜덤 스폰
        /// </summary>
        public void SpawnRandomUnimo(GameObject[] pool)
        {
            if (pool == null || pool.Length == 0)
            {
                ClearSpawnedUnimo();
                return;
            }

            var prefab = pool[Random.Range(0, pool.Length)];
            SpawnUnimo(prefab);
        }


        private void ClearSpawnedUnimo()
        {
            if (_spawnedUnimo) Destroy(_spawnedUnimo);
            _spawnedUnimo = null;
        }
        #endregion
    }
}