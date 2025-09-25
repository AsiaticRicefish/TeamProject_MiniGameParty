using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LDH_UI
{
    [DisallowMultipleComponent]
    public class UI_SfxBinder : MonoBehaviour, IPointerDownHandler
    {
        [Header("Common")]
        [SerializeField] private bool playOnlyWhenInteractable = true;

        [Header("Button SFX")] 
        [SerializeField] private SFX_UI buttonClickSfx = SFX_UI.SFX_Btn1;
        [Header("Toggle SFX")] 
        [SerializeField] private SFX_UI toggleOnSfx = SFX_UI.SFX_Btn1;
        [SerializeField] private SFX_UI toggleOffSfx = SFX_UI.SFX_Btn1;
        [SerializeField] private bool playToggleOnSfx = true;
        [SerializeField] private bool playToggleOffSfx = false;
        
        
        private Button _button;
        private Toggle _toggle;
        private bool _couldPlayAtDown;


        void Awake()
        {
            _button = GetComponent<Button>();
            _toggle = GetComponent<Toggle>();
        }

        
        void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(OnButtonClick);

            if (_toggle != null)
                _toggle.onValueChanged.AddListener(OnToggleChanged);
        }
        
        void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClick);

            if (_toggle != null)
                _toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            _couldPlayAtDown = CanPlay();
            Debug.Log(_couldPlayAtDown);
        }
        
        private void OnButtonClick()
        {
            if (!_couldPlayAtDown) return;
            SoundManager.Instance.PlaySFX_UI(buttonClickSfx);

            _couldPlayAtDown = false;
        }

        private void OnToggleChanged(bool isOn)
        {
            
            if (!_couldPlayAtDown) return;
            var sfx = isOn ? toggleOnSfx : toggleOffSfx;
            bool canPlay = isOn ? playToggleOnSfx : playToggleOffSfx;
            if(canPlay)
                SoundManager.Instance.PlaySFX_UI(sfx);
            _couldPlayAtDown = false;
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
        
        void DumpInactiveChain()
        {
            var t = transform;
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                    Debug.LogWarning($"inactive: {t.name} (activeSelf=false)");
                t = t.parent;
            }
        }
    }
}