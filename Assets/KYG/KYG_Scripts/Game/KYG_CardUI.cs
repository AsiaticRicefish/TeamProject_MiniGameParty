using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace KYG
{
    /// <summary>
    /// 카드 1장 UI. 클릭 시 CardManager에 선택 요청.
    /// 선택 상태/비활성 상태가 화면에 확실히 보이도록
    /// 보이는 이미지들(faceImage, cardBackGround)을 모두 틴트합니다.
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Refs")]
        [SerializeField] private Image cardBackGround;   // 카드 배경(없으면 비워도 됨)
        [SerializeField] private Image faceImage;        // 실제로 보이는 정사각형 이미지(흰색)
        [SerializeField] private TMP_Text numberText;    // 숫자 표기용(앞면일 때만 표시)

        [Header("Sprites")]
        [SerializeField] private Sprite backSprite;      // 뒷면
        [SerializeField] private Sprite faceSpriteBase;  // 앞면 기본

        [Header("Colors")]
        [SerializeField] private Color normalColor   = Color.white;                     // 기본
        [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.35f);    // 선택 불가(남이 집은 카드)
        [SerializeField] private Color ownerColor    = new Color(0.8f, 1f, 0.8f, 1f);   // 내가 집은 카드(연녹)
        [SerializeField] private Color selectedColor = new Color(0.8f, 0.8f, 0.8f, 1f); // 선택됨 표시(회색)

        [Header("Flip")]
        [SerializeField] private float flipDuration = 0.2f;

        private RectTransform rt;
        private bool isFlipping;
        private bool isFace; // 앞면 상태인지
        private int _index;  // 0..N-1
        private int _value;  // 1..N
        private System.Action<int> _onClick;
        
        private bool _interactable = true;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            SetFace(false);
            ApplyTint(normalColor);
            SetRaycast(true);                 // 초기엔 클릭 가능
        }

        /// <summary>카드 초기화</summary>
        public void Setup(int index, int value, System.Action<int> onClicked)
        {
            _index   = index;
            _value   = value;
            _onClick = onClicked;

            SetFace(false);
            UpdateNumberText(_value, false);
            ApplyTint(normalColor);
            _interactable = true;             // 초기화
            SetRaycast(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactable) return;
            if (isFlipping || isFace) return;
            Debug.Log($"[CardUI] clicked index={_index}"); // ← 임시
            _onClick?.Invoke(_index);
        }

        /// <summary>
        /// 상호작용 가능/불가 + 내 카드 강조
        /// interactable: true  -> 아직 아무도 안 집은 카드
        /// interactable: false -> 누군가가 집은 카드
        /// </summary>
        // 선택 가능/불가 + 내 카드 강조
        public void SetInteractable(bool interactable, bool isOwner = false)
        {
            _interactable = interactable;     // 상태 보관
            SetRaycast(interactable);         // 레이캐스트 꺼서 실제로 못 누르게

            if (interactable)
                ApplyTint(isOwner ? ownerColor : normalColor);
            else
                ApplyTint(isOwner ? ownerColor : disabledColor);
        }

        /// <summary>선택됨을 즉시 표시(뒤집히기 전 단계)</summary>
        // 선택 즉시 표시
        public void SetSelected(bool isOwner)
        {
            _interactable = false;            // 선택된 순간부터 클릭 금지
            SetRaycast(false);
            ApplyTint(isOwner ? ownerColor : selectedColor);
        }
        
        private void SetRaycast(bool on)      // 이미지에 레이캐스트 토글
        {
            if (cardBackGround) cardBackGround.raycastTarget = on;
            if (faceImage)      faceImage.raycastTarget      = on;
        }

        /// <summary>카드 공개 애니메이션</summary>
        public void RevealFace()
        {
            if (isFace || isFlipping) return;
            StartCoroutine(CoFlipToFace());
        }

        private IEnumerator CoFlipToFace()
        {
            isFlipping = true;
            Quaternion start = rt.localRotation;
            Quaternion mid   = start * Quaternion.Euler(0, 90, 0);
            Quaternion end   = start * Quaternion.Euler(0, 180, 0);
            float half = flipDuration * 0.5f;
            float t = 0;

            while (t < half)
            {
                t += Time.deltaTime;
                rt.localRotation = Quaternion.Slerp(start, mid, t / half);
                yield return null;
            }

            SetFace(true);
            UpdateNumberText(_value, true);

            t = 0;
            while (t < half)
            {
                t += Time.deltaTime;
                rt.localRotation = Quaternion.Slerp(mid, end, t / half);
                yield return null;
            }

            rt.localRotation = Quaternion.identity;
            isFlipping = false;
        }

        private void SetFace(bool showFace)
        {
            isFace = showFace;

            if (faceImage != null)
                faceImage.sprite = showFace ? faceSpriteBase : backSprite;

            if (numberText != null)
                numberText.gameObject.SetActive(showFace);
        }

        private void UpdateNumberText(int number, bool show)
        {
            if (numberText == null) return;
            numberText.text = show ? number.ToString() : string.Empty;
        }

        /// <summary>
        /// 실제로 보이는 모든 Image에 동일한 틴트를 적용.
        /// (Prefab마다 어떤 이미지가 앞/뒤인지 달라도 안전)
        /// </summary>
        private void ApplyTint(Color c)
        {
            if (cardBackGround != null) cardBackGround.color = c;
            if (faceImage      != null) faceImage.color      = c;
        }
    }
}
