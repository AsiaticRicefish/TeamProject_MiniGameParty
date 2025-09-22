using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_ClosetItemButton : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image background; // 버튼 베이스 색
        [SerializeField] private Image lockImage;
        [SerializeField] private TMP_Text label;

        [Header("Color")] [SerializeField] private Color ownedColor = Color.white;
        [SerializeField] private Color lockColor = new Color(1f, 1f, 1f, 0.5f);


        private string _id;
        private Action<string, bool> _onToggle; // 소유한 아이템이라면 발생하는 이벤트
        private Action<string, bool> _onClicked; // 소유한 아이템이 아니라면 발생하는 이벤트
        private bool _owned;
        private bool _isBound; // 중복 바인딩 방지 플래그


        public Toggle GetToggle() => toggle;
        public string Id => _id;
        public bool IsOwned() => _owned;

        private void Awake()
        {
            if (!toggle) toggle = GetComponent<Toggle>();
        }

        public void Init()
        {
            if (iconImage) iconImage.sprite = null;
            if (label) label.text = "...";
            if (toggle) toggle.interactable = false;

            _id = null;
            _onToggle = null;
            _isBound = false;
        }

        public void Bind(string id, string displayName, Sprite icon, Action<string, bool> onToggle)
        {
            if (_isBound)
            {
                Debug.LogWarning($"[{name}] 이미 Bind가 호출된 상태인데 또 호출됨");
                return;
            }

            _id = id;
            _onToggle = onToggle;

            if (label) label.SetText(displayName ?? id);
            if (iconImage) iconImage.sprite = icon;
            if (toggle)
            {
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(OnValueChanged);
                toggle.interactable = true;
            }

            _isBound = true;
        }


        private void OnValueChanged(bool isOn)
        {
            _onToggle?.Invoke(_id, isOn);
        }

        public void SetOwned(bool owned)
        {
            _owned = owned;
            if (background) background.color = owned ? ownedColor : lockColor;
            if (iconImage) iconImage.color = owned ? ownedColor : lockColor;
            if (lockImage) lockImage.gameObject.SetActive(!owned);
        }
    }
}