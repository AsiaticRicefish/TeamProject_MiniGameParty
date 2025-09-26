using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace YG
{
    /// <summary>
    /// MeteorTap용 사운드 브릿지:
    /// - GameSoundSettings(SoundCollection 포함)를 인스펙터에서 연결
    /// - SoundFacade.TryFindInScene()가 이 컴포넌트를 찾아 ISoundFacade로 사용
    /// - Addressables로 SoundData.audioClip 로드 → 풀링된 AudioSource로 재생
    /// </summary>
    public class MeteorTapSoundBridge : MonoBehaviour, ISoundFacade
    {
        [Header("Game Sound Settings (MeteorGame용)")]
        [SerializeField] private GameSoundSettings soundSettings;

        [Header("AudioSource Pool")]
        [SerializeField] private int initialSources = 6;
        [SerializeField] private bool dontDestroyOnLoad = false;

        private readonly List<AudioSource> _pool = new();
        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private readonly Queue<string> _cacheOrder = new();

        public static MeteorTapSoundBridge Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            // 풀 준비
            for (int i = 0; i < Mathf.Max(1, initialSources); i++)
                _pool.Add(CreateSource());
        }

        private AudioSource CreateSource()
        {
            var go = new GameObject("SFX_AudioSource");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f; // UI/2D
            return src;
        }

        private AudioSource GetFreeSource()
        {
            foreach (var s in _pool) if (!s.isPlaying) return s;
            // 전부 바쁘면 하나 추가
            var extra = CreateSource();
            _pool.Add(extra);
            return extra;
        }

        public void PlaySFX(string soundName)
        {
            if (soundSettings == null || soundSettings.soundCollection == null)
            {
                Debug.LogWarning("[MeteorTapSoundBridge] GameSoundSettings / SoundCollection 미지정");
                return;
            }

            var data = soundSettings.soundCollection.GetSFX(soundName);
            if (data == null)
            {
                Debug.LogWarning($"[MeteorTapSoundBridge] SFX '{soundName}' 를 SoundCollection에서 찾을 수 없습니다.");
                return;
            }

            // 캐시에 클립 있으면 바로 재생
            if (_clipCache.TryGetValue(soundName, out var clip) && clip != null)
            {
                PlayClip(clip, data.defaultVolume);
                return;
            }

            // Addressables 로드
            if (data.audioClip == null)
            {
                Debug.LogWarning($"[MeteorTapSoundBridge] '{soundName}' 의 AudioClip 참조(Addressable)가 비어있습니다.");
                return;
            }

            var handle = data.audioClip.LoadAssetAsync<AudioClip>();
            handle.Completed += (AsyncOperationHandle<AudioClip> op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    var loaded = op.Result;
                    PutIntoCache(soundName, loaded);
                    PlayClip(loaded, data.defaultVolume);
                }
                else
                {
                    Debug.LogWarning($"[MeteorTapSoundBridge] Addressables 로드 실패: {soundName}");
                }
            };
        }

        private void PlayClip(AudioClip clip, float dataVolume)
        {
            var src = GetFreeSource();
            // 최종 볼륨 = 데이터 기본볼륨 × GameSoundSettings.sfxVolumeMultiplier
            float vol = Mathf.Clamp01(dataVolume) * Mathf.Clamp01(soundSettings.sfxVolumeMultiplier);
            src.volume = vol;
            src.clip = clip;
            src.loop = false;
            src.Play();
        }

        private void PutIntoCache(string key, AudioClip clip)
        {
            if (_clipCache.ContainsKey(key)) return;

            _clipCache[key] = clip;
            _cacheOrder.Enqueue(key);

            // 캐시 초과 시, 오래된 것부터 언로드
            int max = Mathf.Max(1, soundSettings.maxCachedClips);
            while (_cacheOrder.Count > max)
            {
                string oldKey = _cacheOrder.Dequeue();
                if (_clipCache.TryGetValue(oldKey, out var oldClip))
                {
                    // Addressables.Release에 대응하려면 AssetReference를 기억해두어야 하지만,
                    // 간단 구현에서는 캐시에서만 제거(필요 시 확장)
                    _clipCache.Remove(oldKey);
                }
            }
        }
    }
}
