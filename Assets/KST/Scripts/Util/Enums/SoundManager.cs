using DesignPattern;
using UnityEngine;

namespace RhythmGame //추후 다른 스크립트에서도 사용할 경우 해당 네임스페이스 제거 요망
{

    public class SoundManager : CombinedSingleton<SoundManager>
    {
        // ScriptableObject에서 사운드 관리해도 됨.
        [SerializeField] AudioClip[] _bgmList;
        [SerializeField] AudioClip[] _gameSfxList;
        [SerializeField] AudioClip[] _uiSfxList;


        //AudioSource 참조
        [SerializeField] AudioSource _bgmAudioSource;
        [SerializeField] AudioSource _sfxAudioSource;

        //음량
        public float bgmSoundVolume;
        public float sfxSoundVolume;


        #region BGM
        public void PlayBGM(Bgms bgms)
        {
            //현재 플레이 중인 BGM과 플레이 하려는 BGM과 동일한 경우 return
            if (_bgmAudioSource.isPlaying && _bgmAudioSource.clip == _bgmList[(int)bgms])
                return;

            _bgmAudioSource.clip = _bgmList[(int)bgms];
            _bgmAudioSource.loop = true;
            _bgmAudioSource.Play();
        }

        // BGM 정지
        public void StopBGM()
        {
            _bgmAudioSource.Stop();
        }

        #endregion

        #region SFX

        /// <summary>
        /// UI 관련 SFX 사운드 실행 시 호출
        /// </summary>
        /// <param name="sfx">UI SFX 사운드</param>
        public void PlaySFX_UI(SFX_UI sfx)
        {
            _sfxAudioSource.PlayOneShot(_uiSfxList[(int)sfx]);
        }

        public void PlaySFX_GAME(SfX_Game sfx)
        {
            _sfxAudioSource.PlayOneShot(_gameSfxList[(int)sfx]);
        }

        // SFX 정지
        public void StopSFX()
        {
            _sfxAudioSource.Stop();
        }

        #endregion

        #region Volume Control
        public void SetBGMSoundVolume(float volume)
        {
            bgmSoundVolume = volume;
            _bgmAudioSource.volume = bgmSoundVolume;
        }

        public void SetSFXSoundVolume(float volume)
        {
            sfxSoundVolume = volume;
            _sfxAudioSource.volume = volume;
        }

        #endregion

    }
}