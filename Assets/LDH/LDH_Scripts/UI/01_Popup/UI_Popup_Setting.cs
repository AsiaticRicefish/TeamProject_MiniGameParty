using System;
using Data;
using Managers;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_Setting : UI_Popup
    {
        [SerializeField] private Button closeButton;

        [Header("User Info")] 
        [SerializeField]
        private TextMeshProUGUI linkAccount;
        [SerializeField]
        private TextMeshProUGUI uid;
        [SerializeField]
        private TextMeshProUGUI nickname;

        [Header("Sound")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        private bool _wiring; // 슬라이더 값 주입 시 역발화 방지

        protected override void Init()
        {
            base.Init();
            Subscribe();

            SyncFromManagerToUI();
        }

        protected override void Clear()
        {
            base.Clear();
            Unsubscribe();
        }

        private void OnEnable()
        {
            //계정 정보 반영하기
#if TEST_WITHOUT_LOGIN
            uid.text = PhotonNetwork.LocalPlayer.UserId;
#else
            uid.text = Manager.Data.UID.Trim();
#endif
            
            nickname.text = PhotonNetwork.LocalPlayer.NickName.Trim();
            
            // 팝업 열릴 때마다 최신값으로 동기화
            SyncFromManagerToUI();

        }


        private void Subscribe()
        {
            if (closeButton) closeButton.onClick.AddListener(RequestClose);

            if (bgmSlider)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.wholeNumbers = false;
                bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            }
            if (sfxSlider)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.wholeNumbers = false;
                sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }
        }

        private void Unsubscribe()
        {
            if (closeButton) closeButton.onClick.RemoveAllListeners();
            if (bgmSlider) bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
            if (sfxSlider) sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
        }

        #region 사운드
        private void SyncFromManagerToUI()
        {
            var sm = SoundManager.Instance;
            if (sm == null) return;

            _wiring = true;
            if (bgmSlider) bgmSlider.SetValueWithoutNotify(Mathf.Clamp01(sm.bgmSoundVolume));
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(sm.sfxSoundVolume));
            _wiring = false;
        }

        private void OnBgmSliderChanged(float v)
        {
            if (_wiring) return;
            SoundManager.Instance?.SetBGMSoundVolume(v); // → PlayerPrefs 저장 + Mixer 반영
        }

        private void OnSfxSliderChanged(float v)
        {
            if (_wiring) return;
            SoundManager.Instance?.SetSFXSoundVolume(v); // → PlayerPrefs 저장 + Mixer 반영
        }
        #endregion
    }
}