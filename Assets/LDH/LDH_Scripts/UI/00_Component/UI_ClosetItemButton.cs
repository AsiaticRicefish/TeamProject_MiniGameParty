
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_ClosetItemButton : MonoBehaviour
    {
        [SerializeField] private Toggle _toggle;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text label;
        
        private string _id;
        private Action<string, bool> _onToggle; 
        private bool _isBound; // 중복 바인딩 방지 플래그
        
        public Toggle GetToggle() => _toggle;
        public string Id => _id;
        
        private void Awake()
        {
            if(!_toggle) _toggle = GetComponent<Toggle>();
        }
        
        public void Init()
        {
            if (iconImage) iconImage.sprite = null;
            if (label) label.text = "...";
            if (_toggle) _toggle.interactable = false;
            
            _id = null;
            _onToggle = null;
            _isBound = false;
        }
        
        public void Bind(string id, string displayName, Sprite icon, Action<string,bool> onToggle)
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
            if (_toggle)
            {
                _toggle.onValueChanged.RemoveAllListeners();
                _toggle.onValueChanged.AddListener(OnValueChanged);
                _toggle.interactable = true;
            }
            _isBound = true;
        }


        private void OnValueChanged(bool isOn)
        {
            _onToggle?.Invoke(_id, isOn);
        }
    }
}