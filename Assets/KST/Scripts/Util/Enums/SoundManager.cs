using DesignPattern;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

    public class SoundManager : CombinedSingleton<SoundManager>
    {
        [Header("Legacy Audio Arrays (기존 방식)")]
        [SerializeField] AudioClip[] _bgmList;
        [SerializeField] AudioClip[] _rhythmList;
        [SerializeField] AudioClip[] _gameSfxList;
        [SerializeField] AudioClip[] _uiSfxList;

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup bgmMixerGroup;
        [SerializeField] private AudioMixerGroup sfxMixerGroup;

        [Header("Audio Sources")]
        [SerializeField] AudioSource _bgmAudioSource;
        [SerializeField] AudioSource _sfxAudioSource;

        [Header("Addressable System")]
        [SerializeField] private GameSoundSettings[] allGameSettings;
        [SerializeField] private SceneSoundMappingConfig sceneMappingConfig;
        [SerializeField] private GameType currentGameType = GameType.Title;

        // 현재 로드된 설정
        private GameSoundSettings _currentSettings;

        #region Addressable 캐시 (내부 처리용)
        private Dictionary<string, AudioClip> _loadedClips = new Dictionary<string, AudioClip>();
        private Dictionary<string, AsyncOperationHandle<AudioClip>> _loadingHandles = new Dictionary<string, AsyncOperationHandle<AudioClip>>();
        private Dictionary<string, Task<AudioClip>> _loadingTasks = new Dictionary<string, Task<AudioClip>>();
        #endregion

        #region 메모리 관리
        private Dictionary<string, float> _clipLastUsedTime = new Dictionary<string, float>();
        private List<string> _keepInMemoryClips = new List<string>();
        #endregion

        // 볼륨
        public float bgmSoundVolume { get; private set; } = 1f;
        public float sfxSoundVolume { get; private set; } = 1f;


        protected override void Awake()
        {
            base.Awake();
            InitializeAudioSources();
            LoadVolumeSettings();

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // 타이틀 사운드로 초기화
            LoadGameSounds(GameType.Title);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            UnloadAllAudioClips();
        }

        private void InitializeAudioSources()
        {
            if (_bgmAudioSource == null)
            {
                GameObject bgmObj = new GameObject("BGM AudioSource");
                bgmObj.transform.SetParent(transform);
                _bgmAudioSource = bgmObj.AddComponent<AudioSource>();
            }

            if (_sfxAudioSource == null)
            {
                GameObject sfxObj = new GameObject("SFX AudioSource");
                sfxObj.transform.SetParent(transform);
                _sfxAudioSource = sfxObj.AddComponent<AudioSource>();
            }

            // Audio Mixer 연결
            if (bgmMixerGroup) _bgmAudioSource.outputAudioMixerGroup = bgmMixerGroup;
            if (sfxMixerGroup) _sfxAudioSource.outputAudioMixerGroup = sfxMixerGroup;
        }

        #region BGM
        public void PlayBGM(Bgms bgms)
        {
            if (_bgmList == null || (int)bgms >= _bgmList.Length) return;

            //현재 플레이 중인 BGM과 플레이 하려는 BGM과 동일한 경우 return
            if (_bgmAudioSource.isPlaying && _bgmAudioSource.clip == _bgmList[(int)bgms])
                return;

            _bgmAudioSource.clip = _bgmList[(int)bgms];
            _bgmAudioSource.loop = true;
            _bgmAudioSource.Play();
        }

        public void PlayBGM(Bgm_RhythmGame bgms)
        {
            if (_rhythmList == null || (int)bgms >= _rhythmList.Length) return;

            //현재 플레이 중인 BGM과 플레이 하려는 BGM과 동일한 경우 return
            if (_bgmAudioSource.isPlaying && _bgmAudioSource.clip == _rhythmList[(int)bgms])
                return;

            _bgmAudioSource.clip = _rhythmList[(int)bgms];
            _bgmAudioSource.loop = true;
            _bgmAudioSource.Play();
        }

        #endregion

        #region SFX

        /// <summary>
        /// UI 관련 SFX 사운드 실행 시 호출
        /// </summary>
        /// <param name="sfx">UI SFX 사운드</param>
        public void PlaySFX_UI(SFX_UI sfx)
        {
            if (_uiSfxList == null || (int)sfx >= _uiSfxList.Length) return;
            _sfxAudioSource.PlayOneShot(_uiSfxList[(int)sfx]);
        }

        public void PlaySFX_GAME(SfX_Game sfx)
        {
            if (_gameSfxList == null || (int)sfx >= _gameSfxList.Length) return;
            _sfxAudioSource.PlayOneShot(_gameSfxList[(int)sfx]);
        }

        #endregion

        #region 리듬게임 관련 로직
        public Bgm_RhythmGame RandomSelectBGM()
        {
            if (_rhythmList == null || _rhythmList.Length == 0) return 0;

            int index = Random.Range(0,_rhythmList.Length);
            return (Bgm_RhythmGame)index;
        }
        #endregion


        #region Addressable으로 사운드 로딩
        /// <summary>
        /// 게임 사운드 로딩
        /// </summary>
        public void LoadGameSounds(GameType gameType)
        {
            LoadGameSoundsAsync(gameType);
        }

        /// <summary>
        /// BGM 재생
        /// </summary>
        public void PlayBGM(string soundName)
        {
            PlayBGMAsync(soundName);
        }

        /// <summary>
        /// SFX 재생
        /// </summary>
        public void PlaySFX(string soundName)
        {
            PlaySFXAsync(soundName);
        }

        public void StopBGM() => _bgmAudioSource.Stop();
        public void PauseBGM() => _bgmAudioSource.Pause();
        public void ResumeBGM() => _bgmAudioSource.UnPause();
        public void StopSFX() => _sfxAudioSource.Stop();


    /// <summary>
    /// 모든 사운드를 정지하고 정리 (씬 전환 시 사용)
    /// </summary>
    public void StopAllSounds()
    {
        StopBGM();
        StopSFX();
    }

    #endregion


    #region Volume Control
    public void SetBGMSoundVolume(float volume)
        {
            bgmSoundVolume = Mathf.Clamp01(volume);

            if (audioMixer)
            {
                float dbVolume;

                if (volume > 0f)
                {
                    // 선형 볼륨을 데시벨로 변환
                    dbVolume = Mathf.Log10(volume) * 20f;
                }
                else
                {
                    // 볼륨이 0 이하일 때는 음소거 (-80dB)
                    dbVolume = -80f;
                }

                audioMixer.SetFloat("BGMVolume", dbVolume);  // Audio Mixer 추가
            }

            _bgmAudioSource.volume = CalculateBGMVolume(1f);
            PlayerPrefs.SetFloat("BGMVolume", volume);
            PlayerPrefs.Save();
        }

        public void SetSFXSoundVolume(float volume)
        {
            sfxSoundVolume = Mathf.Clamp01(volume);

            if (audioMixer)
            {
                float dbVolume;

                if (volume > 0f)
                {
                    // 선형 볼륨을 데시벨로 변환
                    dbVolume = Mathf.Log10(volume) * 20f;
                }
                else
                {
                    // 볼륨이 0 이하일 때는 음소거 (-80dB)
                    dbVolume = -80f;
                }

                audioMixer.SetFloat("SFXVolume", dbVolume);
            }

            PlayerPrefs.SetFloat("SFXVolume", volume);
            PlayerPrefs.Save();
        }

        #endregion

        #region 내부 Addressable 처리
        private async void LoadGameSoundsAsync(GameType gameType)
        {
            try
            {
                if (currentGameType == gameType && _currentSettings != null)
                    return;

                GameSoundSettings settings = System.Array.Find(allGameSettings, s => s.gameType == gameType);

                if (settings == null)
                {
                    Debug.LogWarning($"GameSoundSettings not found for {gameType}");
                    return;
                }

                if (_currentSettings != null && settings.autoUnloadOnSceneChange)
                {
                    await UnloadPreviousGameSounds();
                }

                _currentSettings = settings;
                currentGameType = gameType;

                await PreloadSounds();

                if (!string.IsNullOrEmpty(settings.defaultBGM))
                {
                    PlayBGM(settings.defaultBGM);
                }

                Debug.Log($"[SoundManager] Loaded sounds for {gameType}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load sounds for {gameType}: {ex.Message}");
            }
        }

        private async void PlayBGMAsync(string soundName)
        {
            try
            {
                if (_currentSettings?.soundCollection == null) return;

                var soundData = _currentSettings.soundCollection.GetBGM(soundName);
                if (soundData == null)
                {
                    Debug.LogWarning($"BGM '{soundName}' not found");
                    return;
                }

                var audioClip = await LoadAudioClipAsync(soundData);

                if (_bgmAudioSource.isPlaying && _bgmAudioSource.clip == audioClip)
                    return;

                _bgmAudioSource.clip = audioClip;
                _bgmAudioSource.loop = soundData.isLoop;
                _bgmAudioSource.volume = CalculateBGMVolume(soundData.defaultVolume);
                _bgmAudioSource.Play();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to play BGM '{soundName}': {ex.Message}");
            }
        }

        private async void PlaySFXAsync(string soundName)
        {
            try
            {
                if (_currentSettings?.soundCollection == null) return;

                var soundData = _currentSettings.soundCollection.GetSFX(soundName);
                if (soundData == null)
                {
                    Debug.LogWarning($"SFX '{soundName}' not found");
                    return;
                }

                var audioClip = await LoadAudioClipAsync(soundData);
                float volume = CalculateSFXVolume(soundData.defaultVolume);
                _sfxAudioSource.PlayOneShot(audioClip, volume);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to play SFX '{soundName}': {ex.Message}");
            }
        }

        private async Task<AudioClip> LoadAudioClipAsync(SoundData soundData)
        {
            // 이미 로드됨
            if (_loadedClips.TryGetValue(soundData.soundName, out AudioClip cachedClip))
            {
                _clipLastUsedTime[soundData.soundName] = Time.time;
                return cachedClip;
            }

            // 이미 로딩 중
            if (_loadingTasks.TryGetValue(soundData.soundName, out Task<AudioClip> existingTask))
            {
                return await existingTask;
            }

            // 새로운 로딩 시작
            var loadTask = LoadAudioClipInternal(soundData);
            _loadingTasks[soundData.soundName] = loadTask;

            try
            {
                return await loadTask;
            }
            finally
            {
                _loadingTasks.Remove(soundData.soundName);
            }
        }

        private async Task<AudioClip> LoadAudioClipInternal(SoundData soundData)
        {
            var handle = soundData.audioClip.LoadAssetAsync<AudioClip>();
            _loadingHandles[soundData.soundName] = handle;

            var audioClip = await handle.Task;

            if (audioClip != null)
            {
                _loadedClips[soundData.soundName] = audioClip;
                _clipLastUsedTime[soundData.soundName] = Time.time;
                CheckCacheSize();
            }

            return audioClip;
        }

        private async Task PreloadSounds()
        {
            if (_currentSettings?.soundCollection == null) return;

            var preloadSounds = _currentSettings.soundCollection.GetPreloadSounds();
            var loadTasks = new List<Task>();

            foreach (var soundData in preloadSounds)
            {
                loadTasks.Add(LoadAudioClipAsync(soundData));

                if (soundData.keepInMemory && !_keepInMemoryClips.Contains(soundData.soundName))
                {
                    _keepInMemoryClips.Add(soundData.soundName);
                }
            }

            await Task.WhenAll(loadTasks);
        }

        #endregion

        #region Helper
        private void LoadVolumeSettings()
        {
            bgmSoundVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
            sfxSoundVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

            SetBGMSoundVolume(bgmSoundVolume);
            SetSFXSoundVolume(sfxSoundVolume);
        }

        private float CalculateBGMVolume(float defaultVolume)
        {
            float gameMultiplier = _currentSettings?.bgmVolumeMultiplier ?? 1f;
            return bgmSoundVolume * gameMultiplier * defaultVolume;
        }

        private float CalculateSFXVolume(float defaultVolume)
        {
            float gameMultiplier = _currentSettings?.sfxVolumeMultiplier ?? 1f;
            return sfxSoundVolume * gameMultiplier * defaultVolume;
        }
        #endregion

        #region 메모리 관리

        private void CheckCacheSize()
        {
            int maxClips = _currentSettings?.maxCachedClips ?? 15;
            if (_loadedClips.Count <= maxClips) return;

            var sortedClips = new List<KeyValuePair<string, float>>();
            foreach (var kvp in _clipLastUsedTime)
            {
                if (!_keepInMemoryClips.Contains(kvp.Key))
                {
                    sortedClips.Add(kvp);
                }
            }

            sortedClips.Sort((x, y) => x.Value.CompareTo(y.Value));

            int removeCount = _loadedClips.Count - maxClips;
            for (int i = 0; i < removeCount && i < sortedClips.Count; i++)
            {
                UnloadAudioClip(sortedClips[i].Key);
            }
        }

        private void UnloadAudioClip(string clipName)
        {
            if (_loadedClips.TryGetValue(clipName, out AudioClip clip))
            {
                _loadedClips.Remove(clipName);
                _clipLastUsedTime.Remove(clipName);

                if (_loadingHandles.TryGetValue(clipName, out var handle))
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    _loadingHandles.Remove(clipName);
                }
            }
        }

        private async Task UnloadPreviousGameSounds()
        {
            var clipsToUnload = new List<string>();

            foreach (var clipName in _loadedClips.Keys)
            {
                if (!_keepInMemoryClips.Contains(clipName))
                {
                    clipsToUnload.Add(clipName);
                }
            }

            foreach (string clipName in clipsToUnload)
            {
                UnloadAudioClip(clipName);
            }

            await Task.Yield();
        }

        private void UnloadAllAudioClips()
        {
            foreach (string clipName in new List<string>(_loadedClips.Keys))
            {
                UnloadAudioClip(clipName);
            }
            _loadedClips.Clear();
            _clipLastUsedTime.Clear();
            _loadingHandles.Clear();
            _loadingTasks.Clear();
        }

        /// <summary>
        /// 씬 로드 시 자동으로 호출되어 해당 씬의 사운드를 로드
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (sceneMappingConfig != null)
            {
                GameType gameType = sceneMappingConfig.GetGameTypeForScene(scene.name);
                LoadGameSounds(gameType);
                Debug.Log($"씬 '{scene.name}'에서 '{gameType}' 사운드 자동 로드됨");
            }
            else
            {
                Debug.LogWarning("SceneSoundMappingConfig가 설정되지 않았습니다.");
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (_currentSettings?.autoUnloadOnSceneChange == true)
            {
                var clipsToUnload = new List<string>();
                foreach (var clipName in _loadedClips.Keys)
                {
                    if (!_keepInMemoryClips.Contains(clipName))
                        clipsToUnload.Add(clipName);
                }
                foreach (string clipName in clipsToUnload)
                    UnloadAudioClip(clipName);
            }
        }

        #endregion
    }