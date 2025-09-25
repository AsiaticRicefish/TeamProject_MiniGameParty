using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DesignPattern;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using System;
using Cysharp.Threading.Tasks;

[Serializable]
public struct GameClipEntry
{
    public SfX_Game key;
    public AudioClip clip;
}
[Serializable]
public struct UIClipEntry
{
    public SFX_UI key;
    public AudioClip clip;
}


public class SoundManager : CombinedSingleton<SoundManager>
{
    [Header("Legacy Audio Arrays (기존 방식)")] 
    [SerializeField] AudioClip[] _bgmList;
    [SerializeField] AudioClip[] _rhythmList;
    [SerializeField] List<GameClipEntry> _gameSfxList;
    [SerializeField] List<UIClipEntry> _uiSfxList;

    private Dictionary<string, AudioClip> _gameSfxMap, _uiSfxMap;
    
    [Header("Audio Mixer")] 
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Audio Sources")] 
    [SerializeField] AudioSource _bgmAudioSource; // A/B 구성
    [SerializeField] AudioSource _sfxAudioSource; // Pool

    [Header("Addressable System")] 
    [SerializeField] private GameSoundSettings[] allGameSettings;
    [SerializeField] private SceneSoundMappingConfig sceneMappingConfig;
    [SerializeField] private GameType currentGameType = GameType.Title;

    // 현재 로드된 설정
    private GameSoundSettings _currentSettings;

    #region Addressable 캐시 (내부 처리용)

    private Dictionary<string, AudioClip> _loadedClips = new Dictionary<string, AudioClip>();

    private Dictionary<string, AsyncOperationHandle<AudioClip>> _loadingHandles =
        new Dictionary<string, AsyncOperationHandle<AudioClip>>();

    private Dictionary<string, Task<AudioClip>> _loadingTasks = new Dictionary<string, Task<AudioClip>>();

    #endregion

    #region 메모리 관리

    private Dictionary<string, float> _clipLastUsedTime = new Dictionary<string, float>();
    private List<string> _keepInMemoryClips = new List<string>();

    #endregion

    // 볼륨
    public float bgmSoundVolume { get; private set; } = 1f;
    public float sfxSoundVolume { get; private set; } = 1f;

    #region 최적화

    // BGM 크로스페이드용 A/B
    [Header("BGM Crossfade")] [SerializeField]
    private float defaultBgmFade = 0.5f;

    private AudioSource _bgmA, _bgmB;
    private AudioSource _bgmActive, _bgmIdle;
    private CancellationTokenSource _bgmCts; // BGM 전환 취소 토큰

    // SFX 풀
    [Header("SFX Voices")] [SerializeField, Range(1, 16)]
    private int sfxVoices = 8;

    private readonly List<AudioSource> _sfxPool = new();
    private int _sfxCursor = 0;

    private int _loadVersion = 0;

    #endregion

    protected override void Awake()
    {
        base.Awake();
        InitializeAudioSources();
        LoadVolumeSettings();
        BuildMaps();

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        //활성 씬 변경 감지
        SceneManager.activeSceneChanged += OnActiveSceneChanged;


        // 타이틀 사운드로 초기화
        LoadGameSounds(GameType.Title);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;


        _bgmCts?.Cancel();
        _bgmCts?.Dispose();

        UnloadAllAudioClips();
    }

    private void InitializeAudioSources()
    {
        #region BGM A/B

        // --- BGM A ---
        if (_bgmAudioSource == null)
        {
            var bgmObjA = new GameObject("BGM_A");
            bgmObjA.transform.SetParent(transform);
            _bgmAudioSource = bgmObjA.AddComponent<AudioSource>();
        }

        _bgmA = _bgmAudioSource;
        _bgmA.playOnAwake = false;
        _bgmA.loop = true;
        if (bgmMixerGroup) _bgmA.outputAudioMixerGroup = bgmMixerGroup;

        // --- BGM B ---
        var bgmObjB = new GameObject("BGM_B");
        bgmObjB.transform.SetParent(transform);
        _bgmB = bgmObjB.AddComponent<AudioSource>();
        _bgmB.playOnAwake = false;
        _bgmB.loop = true;
        if (bgmMixerGroup) _bgmB.outputAudioMixerGroup = bgmMixerGroup;

        _bgmActive = _bgmA;
        _bgmIdle = _bgmB;

        #endregion

        #region SFX Pool

        if (_sfxAudioSource == null)
        {
            var sfxLegacy = new GameObject("SFX_Legacy");
            sfxLegacy.transform.SetParent(transform);
            _sfxAudioSource = sfxLegacy.AddComponent<AudioSource>();
        }

        if (sfxMixerGroup) _sfxAudioSource.outputAudioMixerGroup = sfxMixerGroup;

        for (int i = 0; i < sfxVoices; i++)
        {
            var go = new GameObject($"SFX_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            if (sfxMixerGroup) src.outputAudioMixerGroup = sfxMixerGroup;
            _sfxPool.Add(src);
        }

        _bgmA.playOnAwake = false;
        _bgmB.playOnAwake = false;
        _sfxAudioSource.playOnAwake = false;
        foreach (var s in _sfxPool) s.playOnAwake = false;

        #endregion
    }

    #region Build Map(Dictionary)

    private void BuildMaps()
    {
        _gameSfxMap = BuildMap(_gameSfxList,
            e => e.key.ToString(),
            e => e.clip);

        _uiSfxMap = BuildMap(_uiSfxList,
            e => e.key.ToString(),
            e => e.clip);
    }
    
    private static Dictionary<string, AudioClip> BuildMap<T>(
        IEnumerable<T> entries,
        Func<T, string> keySelector,
        Func<T, AudioClip> clipSelector)
    {
        var dict = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        if (entries == null) return dict;

        foreach (var e in entries)
        {
            var key = keySelector(e);
            var clip = clipSelector(e);
            if (string.IsNullOrEmpty(key) || clip == null) continue;

            dict[key] = clip;
        }
        return dict;
    }


    #endregion
    

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
        if (_uiSfxList == null || !_uiSfxMap.TryGetValue(sfx.ToString(), out var clip)) return;
        _sfxAudioSource.PlayOneShot(clip);
    }

    public void PlaySFX_GAME(SfX_Game sfx)
    {
        if (_uiSfxList == null || !_gameSfxMap.TryGetValue(sfx.ToString(), out var clip)) return;
        _sfxAudioSource.PlayOneShot(clip);
    }

    #endregion

    #region 리듬게임 관련 로직

    public Bgm_RhythmGame RandomSelectBGM()
    {
        if (_rhythmList == null || _rhythmList.Length == 0) return 0;

        int index = UnityEngine.Random.Range(0, _rhythmList.Length);
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

    public void StopBGM(bool clearClip = true)
    {
        // 페이드 취소
        _bgmCts?.Cancel();

        // 양쪽 모두 정지
        _bgmA.Stop();
        _bgmB.Stop();

        // 볼륨/상태 초기화
        _bgmA.volume = 0f;
        _bgmB.volume = 0f;

        if (clearClip)
        {
            _bgmA.clip = null;
            _bgmB.clip = null;
        }

        _bgmActive = _bgmA;
        _bgmIdle = _bgmB;
    }

    public void PauseBGM() => _bgmAudioSource.Pause();
    public void ResumeBGM() => _bgmAudioSource.UnPause();

    public void StopSFX(bool clearClip = true)
    {
        foreach (var s in _sfxPool)
        {
            s.Stop();
            if (clearClip) s.clip = null;
        }

        _sfxAudioSource.Stop();
        if (clearClip) _sfxAudioSource.clip = null;
    }


    /// <summary>
    /// 모든 사운드를 정지하고 정리 (씬 전환 시 사용)
    /// </summary>
    public void StopAllSounds(bool clearClips = true)
    {
        StopBGM(clearClips);
        StopSFX(clearClips);
    }

    #endregion


    #region Volume Control

    private AudioMixer MixerForBGM => bgmMixerGroup ? bgmMixerGroup.audioMixer : audioMixer;
    private AudioMixer MixerForSFX => sfxMixerGroup ? sfxMixerGroup.audioMixer : audioMixer;

    public void SetBGMSoundVolume(float volume)
    {
        bgmSoundVolume = Mathf.Clamp01(volume);

        float dbVolume;

        if (volume > 0f)
        {
            dbVolume = Mathf.Log10(volume) * 20f;
        }

        else
        {
            dbVolume = -80f; // 음소거
        }

        var mixer = MixerForBGM;
        if (mixer != null)
        {
            bool ok = mixer.SetFloat("BGMVolume", dbVolume);
            if (!ok)
            {
                Debug.LogWarning($"[SoundManager] 'BGMVolume' 파라미터를 {mixer.name}에서 찾지 못했습니다.");
            }
        }

        PlayerPrefs.SetFloat("BGMVolume", volume);
    }

    public void SetSFXSoundVolume(float volume)
    {
        sfxSoundVolume = Mathf.Clamp01(volume);

        float dbVolume;

        if (volume > 0f)
        {
            dbVolume = Mathf.Log10(volume) * 20f;
        }
        else
        {
            dbVolume = -80f;
        }

        var mixer = MixerForSFX;
        if (mixer != null)
        {
            bool ok = mixer.SetFloat("SFXVolume", dbVolume);
            if (!ok)
            {
                Debug.LogWarning($"[SoundManager] 'SFXVolume' 파라미터를 {mixer.name}에서 찾지 못했습니다.");
            }
        }

        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    #endregion

    #region 내부 Addressable 처리

    private async UniTask LoadGameSoundsAsync(GameType gameType)
    {
        int myVersion = ++_loadVersion;
        try
        {
            if (currentGameType == gameType && _currentSettings != null)
                return;

            GameSoundSettings settings = Array.Find(allGameSettings, s => s.gameType == gameType);

            if (settings == null)
            {
                Debug.LogWarning($"GameSoundSettings not found for {gameType}");
                return;
            }

            if (_currentSettings != null && settings.autoUnloadOnSceneChange)
            {
                await UnloadPreviousGameSounds();
                if (myVersion != _loadVersion) return;
            }

            _currentSettings = settings;
            currentGameType = gameType;

            await PreloadSounds();
            if (myVersion != _loadVersion) return;

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

            await CrossfadeToAsync(audioClip, soundData.isLoop, defaultBgmFade);
        }
        catch (Exception ex)
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

            var clip = await LoadAudioClipAsync(soundData);
            var src = AcquireSfxVoice();
            src.spatialBlend = 0f;
            src.pitch = 1f;
            src.PlayOneShot(clip, CalculateSFXVolume(soundData.defaultVolume));
        }
        catch (Exception ex)
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
            LoadGameSoundsAsync(gameType);
            Debug.Log($"씬 '{scene.name}'에서 '{gameType}' 사운드 자동 로드됨");
        }
        else
        {
            Debug.LogWarning("SceneSoundMappingConfig가 설정되지 않았습니다.");
        }

        SetBGMSoundVolume(bgmSoundVolume);
        SetSFXSoundVolume(sfxSoundVolume);
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

    // 활성 씬이 바뀌는 그 순간에 세팅 교체
    private async void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (sceneMappingConfig == null)
        {
            Debug.LogWarning("SceneSoundMappingConfig가 설정되지 않았습니다.");
            return;
        }

        var gt = sceneMappingConfig.GetGameTypeForScene(newScene.name);

        if (currentGameType == gt && _currentSettings != null)
        {
            Debug.Log($"<color=yellow> current game type == gt : {currentGameType}</color>");
            return;
        }

        Debug.Log($"<color=yellow> current game type != gt : {currentGameType}</color>");

        GameSoundSettings settings = Array.Find(allGameSettings, s => s.gameType == gt);

        if (settings == null)
        {
            Debug.LogWarning($"GameSoundSettings not found for {gt}");
            return;
        }

        _currentSettings = settings;
        currentGameType = gt;


        // 믹서 재적용
        SetBGMSoundVolume(bgmSoundVolume);
        SetSFXSoundVolume(sfxSoundVolume);

        Debug.Log($"[SoundManager] ActiveScene → '{newScene.name}', GameType → {gt}, GameSetting 변경 완료");
    }

    #endregion

    #region 내부 유틸: SFX 보이스/크로스페이드

    private AudioSource AcquireSfxVoice()
    {
        for (int i = 0; i < _sfxPool.Count; i++)
        {
            int idx = (_sfxCursor + i) % _sfxPool.Count;
            if (!_sfxPool[idx].isPlaying)
            {
                _sfxCursor = (idx + 1) % _sfxPool.Count;
                return _sfxPool[idx];
            }
        }

        var steal = _sfxPool[_sfxCursor];
        _sfxCursor = (_sfxCursor + 1) % _sfxPool.Count;
        return steal;
    }

    private async Task CrossfadeToAsync(AudioClip clip, bool loop, float fadeSeconds)
    {
        _bgmCts?.Cancel();
        _bgmCts = new CancellationTokenSource();
        var ct = _bgmCts.Token;


        _bgmIdle.clip = clip;
        _bgmIdle.loop = loop;

        _bgmActive.volume = 1f;
        _bgmIdle.volume = 0f;
        _bgmIdle.Play();

        float fade = Mathf.Max(0.01f, fadeSeconds);
        float t = 0f;

        try
        {
            while (t < fade)
            {
                if (ct.IsCancellationRequested) return;
                t += Time.deltaTime;
                float k = t / fade;

                _bgmActive.volume = 1f - k;
                _bgmIdle.volume = k;
                await Task.Yield();
            }
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _bgmActive.Stop();

                _bgmActive.volume = 1f;
                _bgmIdle.volume = 1f;

                var tmp = _bgmActive;
                _bgmActive = _bgmIdle;
                _bgmIdle = tmp;
            }
        }
    }

    #endregion

    public async Task<float> GetMusicLengthAsync(string soundName, float fallbackSeconds = 180f)
    {
        try
        {
            if (_currentSettings?.soundCollection == null)
                return fallbackSeconds;

            // BGM 데이터 찾기
            var soundData = _currentSettings.soundCollection.GetBGM(soundName);
            if (soundData == null)
                return fallbackSeconds;

            // 로드된 경우
            if (_loadedClips.TryGetValue(soundData.soundName, out var cached) && cached != null)
                return Mathf.Max(0.01f, cached.length);

            // 로드해서 길이 얻기
            var clip = await LoadAudioClipAsync(soundData);

            if (clip == null)
                return fallbackSeconds;

            return Mathf.Max(0.01f, clip.length);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            return fallbackSeconds;
        }
    }
}