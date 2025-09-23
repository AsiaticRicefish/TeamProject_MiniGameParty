using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LDH_UI;
using UnityEngine;
using UnityEngine.UI;

public class UI_Popup_GameSetting : UI_Popup
{
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button closeBtn;

    private bool _wiring;

    private void Awake()
    {
        // 닫기
        if (closeBtn)
        {
            SoundManager.Instance?.PlaySFX("Click");
            closeBtn.onClick.AddListener(() => RequestClose());
        }

        // 슬라이더 변경 → SoundManager에 반영
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

    private void OnEnable()
    {
        // 다음 프레임에 동기화 (초기화 순서/정렬 이슈 회피)
        SyncNextFrame().Forget();
    }

    private async UniTaskVoid SyncNextFrame()
    {
        await UniTask.NextFrame(); // 1프레임 대기
        SyncFromManagerToUI();
    }

    private void SyncFromManagerToUI()
    {
        _wiring = true;

        var sm = SoundManager.Instance;
        if (sm != null)
        {
            float bgm = Mathf.Clamp01(sm.bgmSoundVolume);
            float sfx = Mathf.Clamp01(sm.sfxSoundVolume);

            if (bgmSlider) bgmSlider.value = bgm;
            if (sfxSlider) sfxSlider.value = sfx;
        }

        _wiring = false;
    }

    private void OnBgmSliderChanged(float v)
    {
        if (_wiring) return;
        SoundManager.Instance?.SetBGMSoundVolume(v);
    }

    private void OnSfxSliderChanged(float v)
    {
        if (_wiring) return;
        SoundManager.Instance?.SetSFXSoundVolume(v);
    }
}