using LDH_MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Loading : UI_Popup
    {
        [Header("Loading UI")]
        [SerializeField] private TMP_Text title;               // 큰 제목
        [SerializeField] private TMP_Text bigDescription;      // 메인 설명(큰 글씨)
        [SerializeField] private TMP_Text smallDescription;    // 서브 설명(작은 글씨)

        [Header("Common Visuals (Prefab handles colors/background)")]
        [SerializeField] private Image progressCircle;         // 원형 진행률 (fillAmount만 사용)
        [SerializeField] private Transform unimoAnchor;        // 유니모 애니메이션 위치

        [Header("Color Panels")]
        [SerializeField] private Image titlePanel;            // 제목 배경 패널
        [SerializeField] private Image backgroundPanel;       // 배경 패널
        [SerializeField] private Image descriptionPanel;      // 설명창 패널

        private GameObject _spawnedUnimo;

        /// <summary>
        /// 테마 적용 (텍스트/유니모)
        /// </summary>
        public void ApplyTheme(UI_LoadingTheme theme)
        {
            if (!theme) return;

            SetTitle(theme.gameTitle);
            SetBigDescription(theme.bigDescription);
            SetSmallDescription(theme.smallDescription);

            SetTitlePanelColor(theme.titlePanelColor);
            SetBackgroundPanelColor(theme.backgroundPanelColor);
            SetDescriptionPanelColor(theme.descriptionPanelColor);

            SpawnRandomUnimo(theme.unimoPrefabs);
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
        public void SetBigDescription(string text)
        {
            if (bigDescription) bigDescription.text = text ?? string.Empty;
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
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
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