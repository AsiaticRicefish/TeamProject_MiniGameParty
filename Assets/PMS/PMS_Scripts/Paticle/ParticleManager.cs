using DesignPattern;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;

//게임 끝날때 까지 사용할 매니저
public class ParticleManager : CombinedSingleton<ParticleManager> //추후 SingleTon or PunSingleton으로 변경
{
    [Header("Inspector에 할당된 ParticleData SO들")]
    [SerializeField] Transform ParticlePoolRegister_Transform;
    [SerializeField] ParticleData[] particles;

    // id → SO 참조 캐시
    private Dictionary<string, ParticleData> dataMap;

    private readonly Dictionary<string, ParticlePool> pools = new();
    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> handles = new();
    private readonly Dictionary<string, float> durations = new();

    protected override void Awake()
    {
        // ParticleData SO들을 dataMap에 등록
        //BuildDataMap();
    }

    private void BuildDataMap()
    {
        dataMap.Clear();

        if (dataMap == null) return;

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
            pools[data.id] = new ParticlePool(prefab, data.initialPoolSize,ParticlePoolRegister_Transform);
            durations[data.id] = data.duration;
        }
        catch
        {
            Debug.LogError($"Preload failed: {data.addressableKey}");
        }

        Debug.Log("[Particle Manager - 파티클 데이터 비동기 로드 완료!]");
    }

    /// <summary>
    /// 파티클이 한번만 실행될 때
    /// </summary>
    // Play 시점
    public async UniTask PlayAsync(string id, Vector3 pos, Quaternion rot, Transform followTarget = null)
    {
        //해당 id에 생성된 풀이 존재하지 않으면, 직접 id로 해당 프리팹을 찾는다.
        if (!pools.ContainsKey(id))
        {
            Debug.LogWarning($"Auto-preload {id}");
            var data = FindParticleData(id);
            if (data != null) await PreloadAsync(data);
            else return;
        }
        // 1) 인스턴스 획득
        var ps = pools[id].Get(ParticlePoolRegister_Transform);

        // 2) 위치·회전 세팅
        ps.transform.SetPositionAndRotation(pos, rot);

        // 3) 부모 지정(따라다니기) 전, 원본 부모 저장
        var originalParent = ps.transform.parent;

        if (followTarget != null)
        {
            ps.transform.SetParent(followTarget, worldPositionStays: true);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        // 4) 파티클 재생
        ps.Play();


        // 5) 파티클 지속시간 대기
        /*var lifetime = ps.main.startLifetime.constantMax;
        await UniTask.Delay(System.TimeSpan.FromSeconds(lifetime), cancellationToken: this.GetCancellationTokenOnDestroy()); */
        await UniTask.Delay(System.TimeSpan.FromSeconds(ps.main.duration), cancellationToken: this.GetCancellationTokenOnDestroy());
        //await UniTask.Delay(System.TimeSpan.FromSeconds(durations[id]), cancellationToken: this.GetCancellationTokenOnDestroy());

        // 6) 원래 부모 Transform Child로 복원 & 릴리즈
        ps.transform.SetParent(originalParent);
        pools[id].Release(ps);
    }

    /// <summary>
    /// 파티클이 Loop형식 일 때(duration 값을 직접 지정해주세요)
    /// </summary>
    public async UniTask PlayLoopAsync(string id, Vector3 pos, Quaternion rot,float duration, Transform followTarget = null)
    {
        //해당 id에 생성된 풀이 존재하지 않으면, 직접 id로 해당 프리팹을 찾는다.
        if (!pools.ContainsKey(id))
        {
            Debug.LogWarning($"Auto-preload {id}");
            var data = FindParticleData(id);
            if (data != null) await PreloadAsync(data);
            else return;
        }

        var ps = pools[id].Get(ParticlePoolRegister_Transform);

        ps.transform.SetPositionAndRotation(pos, rot);

        var originalParent = ps.transform.parent;

        if (followTarget != null)
        {
            ps.transform.SetParent(followTarget, worldPositionStays: true);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        ps.Play();

        await UniTask.Delay(System.TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());

        ps.transform.SetParent(originalParent);
        pools[id].Release(ps);
    }

    /// <summary>
    /// 파티클이 특정 조건이 끝날 때 까지 사용되야 할 때
    /// </summary>
    public async UniTask PlayUntilAsync(string id, Vector3 pos, Quaternion rot, Func<bool> condition, Transform followTarget = null)
    {
        if (!pools.ContainsKey(id))
        {
            Debug.LogWarning($"Auto-preload {id}");
            var data = FindParticleData(id);
            if (data != null) await PreloadAsync(data);
            else return;
        }

        var ps = pools[id].Get(ParticlePoolRegister_Transform);

        ps.transform.SetPositionAndRotation(pos, rot);

        var originalParent = ps.transform.parent;

        if (followTarget != null)
        {
            ps.transform.SetParent(followTarget, worldPositionStays: true);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        ps.Play();

        await UniTask.WaitUntil(condition, cancellationToken: this.GetCancellationTokenOnDestroy());

        ps.transform.SetParent(originalParent);
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
