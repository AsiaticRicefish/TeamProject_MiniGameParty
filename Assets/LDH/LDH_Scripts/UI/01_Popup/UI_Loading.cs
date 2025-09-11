using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Loading : UI_Popup
    {
        [Header("Loading UI")]
        [SerializeField] private TMP_Text title;          // 게임 이름
        [SerializeField] private TMP_Text description;    // 게임 설명
        [SerializeField] private Image backgroundImg;     // 배경 이미지
        [SerializeField] private Image progressCircle;      // 진행률 바
        [SerializeField] private Transform unimoAnchor;   // 유니모 애니메이션 위치

        private GameObject _spawnedUnimo;

        /// <summary>
        /// Theme 데이터 적용
        /// </summary>
        public void ApplyTheme(UI_LoadingTheme theme)
        {
            if (!theme) return;

            if (title) title.text = theme.gameTitle;
            if (description) description.text = theme.gameDescription;
            if (backgroundImg) backgroundImg.sprite = theme.background;
            if (progressCircle) progressCircle.color = theme.progressColor;

            // 유니모 랜덤 연출
            if (_spawnedUnimo) Destroy(_spawnedUnimo);
            if (theme.unimoPrefabs != null && theme.unimoPrefabs.Length > 0 && unimoAnchor)
            {
                var prefab = theme.unimoPrefabs[Random.Range(0, theme.unimoPrefabs.Length)];
                _spawnedUnimo = Instantiate(prefab, unimoAnchor);
                _spawnedUnimo.transform.localPosition = Vector3.zero;
                _spawnedUnimo.transform.localRotation = Quaternion.identity;
                _spawnedUnimo.transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 진행률 반영
        /// </summary>
        public void SetProgress(float t01)
        {
            if (progressCircle) progressCircle.fillAmount = Mathf.Clamp01(t01);
        }
    }
}