using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEngine.ParticleSystem;

//게임 끝날때 까지 사용할 매니저
public class ParticleManager : MonoBehaviour //추후 SingleTon or PunSingleton으로 변경
{
    public static ParticleManager Instance { get; private set; }

    [Header("Inspector에 할당된 ParticleData SO들")]
    [SerializeField] ParticleData[] particles;

    // id → SO 참조 캐시
    private Dictionary<string, ParticleData> dataMap;
    private readonly Dictionary<string, ParticlePool> pools = new();
    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> handles = new();
    private readonly Dictionary<string, float> durations = new();

    void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(this);
            // ParticleData SO들을 dataMap에 등록

            BuildDataMap();
        }
    }

    private void BuildDataMap()
    {
        dataMap.Clear();
        foreach (var pd in particles)
        {
            if (pd == null || dataMap.ContainsKey(pd.id))
                continue;
            dataMap[pd.id] = pd;
        }
    }

    // 씬 로드 시 호출: 비동기 로드 + 풀 생성
    public async UniTask PreloadAsync(ParticleData data)
    {
        if (handles.ContainsKey(data.id)) return;

        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(data.addressableKey);
            handles[data.id] = handle;
            var prefab = await handle.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
            pools[data.id] = new ParticlePool(prefab, data.initialPoolSize);
            durations[data.id] = data.duration;
        }
        catch
        {
            Debug.LogError($"Preload failed: {data.addressableKey}");
        }
    }

    // Play 시점
    public async UniTask PlayAsync(string id, Vector3 pos, Quaternion rot)
    {
        if (!pools.ContainsKey(id))
        {
            Debug.LogWarning($"Auto-preload {id}");
            var data = FindParticleData(id);
            if (data != null) await PreloadAsync(data);
            else return;
        }

        var ps = pools[id].Get();
        ps.transform.SetPositionAndRotation(pos, rot);
        ps.Play();
        await UniTask.Delay(System.TimeSpan.FromSeconds(durations[id]), cancellationToken: this.GetCancellationTokenOnDestroy());
        pools[id].Release(ps);
    }

    // 씬 언로드 시 호출: 풀·핸들 릴리즈
    public void Release(string id)
    {
        if (handles.TryGetValue(id, out var handle))
        {
            Addressables.Release(handle);
            handles.Remove(id);
        }
        if (pools.TryGetValue(id, out var pool))
        {
            pool.ClearAll();   // 풀 내 모든 인스턴스 파괴
            pools.Remove(id);
            durations.Remove(id);
        }
    }

    private ParticleData FindParticleData(string id)
    {
        // 필요하면 전체 SO 목록에서 검색하거나,
        // 별도 캐시된 딕셔너리에서 꺼내도록 구현
        return null;
    }
}
