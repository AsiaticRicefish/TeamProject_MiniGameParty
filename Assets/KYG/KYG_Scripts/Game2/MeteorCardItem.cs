using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YG
{
    public class MeteorCardItem : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text valueText;

        [Header("Sprites")]
        [SerializeField] private Sprite frontSprite;
        [SerializeField] private Sprite backSprite;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;                  // 기본
        [SerializeField] private Color dimColor = new Color(1,1,1,0.35f);         // 남이 골라서 막힌 카드
        [SerializeField] private Color mineColor = new Color(1f,0.95f,0.6f);      // 내가 고른 카드(강조)
        [SerializeField] private Color takenColor = new Color(0.9f,0.95f,1f);     // 남이 고른 카드(강조)

        public int Index { get; private set; }
        public int Value { get; private set; }

        private System.Action<int> _onClick;

        public void Init(int index, System.Action<int> onClick, int value)
        {
            Index = index; Value = value; _onClick = onClick;

            if (!button) button = GetComponent<Button>();                // 자동 연결
            if (!frame)  frame  = GetComponent<Image>();
            if (!valueText) valueText = GetComponentInChildren<TMP_Text>(true);

            if (button)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke(Index));
                button.interactable = true;
            }

            // 초기 '뒷면'
            SetBackface();
            frame.color = normalColor;
        }

        /// <summary>카드를 뒷면(?) 상태로 만든다(선택 전/선택 직후에도 숫자 공개 금지).</summary>
        public void SetBackface()
        {
            if (frame) frame.sprite = backSprite;
            if (valueText) valueText.text = "";
        }

        /// <summary>선택된 카드에 색상만 입혀서 표시(숫자는 공개 안 함).</summary>
        public void MarkPicked(bool mine)
        {
            if (button) button.interactable = false;
            SetBackface(); // 숫자 감춤 유지
            frame.color = mine ? mineColor : takenColor;
        }

        /// <summary>다른 플레이어가 골라서 더 이상 선택 불가 → 흐리게만 표시(숫자 공개 안 함).</summary>
        public void DimUnavailable()
        {
            if (button) button.interactable = false;
            SetBackface();                 // 여전히 뒷면 유지
            frame.color = dimColor;
        }

        /// <summary>일괄 공개 타이밍에 앞면/숫자를 보여준다.</summary>
        public void FlipReveal()
        {
            if (frame) frame.sprite = frontSprite;
            if (valueText) valueText.text = Value.ToString();
            if (button) button.interactable = false;
            frame.color = Color.white; // 공개 시 컬러 초기화(원하면 유지해도 됨)
        }
    }
}
