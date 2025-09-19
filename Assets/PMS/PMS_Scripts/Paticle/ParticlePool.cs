using System.Collections.Generic;
using UnityEngine;

public class ParticlePool
{
    private readonly GameObject prefab;
    private readonly Queue<ParticleSystem> pool;

    /// <summary>
    /// 풀 생성자: 프리팹과 초기 사이즈 지정
    /// </summary>
    public ParticlePool(GameObject prefab, int initialSize)
    {
        this.prefab = prefab;
        pool = new Queue<ParticleSystem>(initialSize);

        for (int i = 0; i < initialSize; i++)
        {
            var ps = CreateInstance();
            ps.gameObject.SetActive(false);
            pool.Enqueue(ps);
        }
    }

    /// <summary>
    /// 풀에서 인스턴스가 모자라면 새로 만들고, 사용 대기 큐에서 하나 꺼내 활성화 후 반환
    /// </summary>
    public ParticleSystem Get()
    {
        if (pool.Count == 0)
        {
            pool.Enqueue(CreateInstance());
        }

        var ps = pool.Dequeue();
        ps.gameObject.SetActive(true);
        return ps;
    }

    /// <summary>
    /// 사용이 끝난 파티클을 정지 및 초기화 후 풀에 반환
    /// </summary>
    public void Release(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
        pool.Enqueue(ps);
    }

    /// <summary>
    /// 풀 내부에 남은 모든 인스턴스를 파괴하고 비운다 (Pool 해제 시 사용)
    /// </summary>
    public void ClearAll()
    {
        while (pool.Count > 0)
        {
            var ps = pool.Dequeue();
            Object.Destroy(ps.gameObject);
        }
    }

    /// <summary>
    /// 프리팹에서 새로운 ParticleSystem 인스턴스를 생성하고
    /// ParticleManager 오브젝트 아래에 두어 계층 정리
    /// </summary>
    private ParticleSystem CreateInstance()
    {
        var go = Object.Instantiate(prefab);
        go.transform.SetParent(ParticleManager.Instance.transform, false);
        return go.GetComponent<ParticleSystem>();
    }
}