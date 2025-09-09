using UnityEngine;
using UnityEngine.UI;
using RhythmGame;

public class SoundSettingUI : MonoBehaviour
{
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        bgmSlider.value = SoundManager.Instance.bgmSoundVolume;
        sfxSlider.value = SoundManager.Instance.sfxSoundVolume;
    }

    private void OnBGMVolumeChanged(float value)
    {
        SoundManager.Instance.SetBGMSoundVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        SoundManager.Instance.SetSFXSoundVolume(value);
    }

}