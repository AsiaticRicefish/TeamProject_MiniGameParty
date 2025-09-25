using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    [DisallowMultipleComponent]
    public class UI_SfxBinder : MonoBehaviour
    {
        [Header("Common")]
        [SerializeField] private bool playOnlyWhenInteractable = true;

        [Header("Button SFX")] 
        [SerializeField] private SFX_UI buttonClickSfx = SFX_UI.SFX_BtnClick;
        [Header("Toggle SFX")] 
        [SerializeField] private SFX_UI toggleOnSfx = SFX_UI.SFX_BtnClick;
        [SerializeField] private SFX_UI toggleOffSfx = SFX_UI.SFX_BtnClick;
        [SerializeField] private bool playToggleOnSfx = true;
        [SerializeField] private bool playToggleOffSfx = false;
        
        
        private Button _button;
        private Toggle _toggle;

        void Awake()
        {
            _button = GetComponent<Button>();
            _toggle = GetComponent<Toggle>();
        }

        
        void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(OnButtonClick);

            if (_toggle != null)
                _toggle.onValueChanged.AddListener(OnToggleChanged);
        }
        
        void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClick);

            if (_toggle != null)
                _toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
        
        private void OnButtonClick()
        {
            if (!CanPlay()) return;
                SoundManager.Instance.PlaySFX_UI(buttonClickSfx);
        }

        private void OnToggleChanged(bool isOn)
        {
            if (!CanPlay()) return;
            var sfx = isOn ? toggleOnSfx : toggleOffSfx;
            bool canPlay = isOn ? playToggleOnSfx : playToggleOffSfx;
            if(canPlay)
                SoundManager.Instance.PlaySFX_UI(sfx);
        }
        
        private bool CanPlay()
        {
            if (SoundManager.Instance == null) return false;
            if (!playOnlyWhenInteractable) return true;

            // interactable 체크 (Button/Toggle 둘 중 있는 것)
            if (_button != null) return _button.interactable && gameObject.activeInHierarchy;
            if (_toggle != null) return _toggle.interactable && gameObject.activeInHierarchy;
            return gameObject.activeInHierarchy;
        }
        
        
    }
}