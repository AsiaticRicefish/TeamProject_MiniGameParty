using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class PrefabPoolRegistry
{
    readonly Dictionary<string, PrefabPool> _pools = new();
    readonly Transform _poolRoot;

    public PrefabPoolRegistry(Transform poolRoot = null) { _poolRoot = poolRoot; }


    PrefabPool GetOrCreate(string id, AssetReferenceGameObject prefabRef)
    {
        if (!_pools.TryGetValue(id, out var p))
            _pools[id] = p = new PrefabPool(prefabRef);
        return p;
    }

    public async UniTask<GameObject> GetInstanceAsync(string id,
        AssetReferenceGameObject prefabRef, Transform parent)
    {
        var pool = GetOrCreate(id, prefabRef);
        return await pool.GetInstance(parent);
    }

    public void ReleaseInstance(string id, GameObject go)
    {
        if (go == null || !_pools.TryGetValue(id, out var pool)) return;
        pool.ReleaseInstance(go, _poolRoot);
    }

    public void DisposeAll()
    {
        // DumpAll("DisposeAll-Before");
        foreach (var p in _pools.Values) p.Dispose();
        _pools.Clear();
        Debug.Log("[PoolRegistry] DisposeAll complete.");

    }

    public void DisposePool(string id)
    {
        if (_pools.TryGetValue(id, out var p))
        {
            // p.DumpState("<color=red>DisposePool-Before");
            p.Dispose();
            _pools.Remove(id);
            // Debug.Log($"<color=red>[PoolRegistry] DisposePool({id}) done.</color>");

        }
    }
    
    public void DumpAll(string tag = "")
    {
        int totalAlive = 0, totalPooled = 0;
        Debug.Log($"<color=red>[PoolRegistry][{tag}] ---- POOLS SNAPSHOT START ----</color>");
        foreach (var kv in _pools)
        {
            var id = kv.Key;
            var p  = kv.Value;
            // p.DumpState(tag);
            totalAlive  += p.AliveCount;
            totalPooled += p.PooledCount;
        }
        Debug.Log($"<color=red>[PoolRegistry][{tag}] ---- SUMMARY: pooled={totalPooled}, alive={totalAlive}, pools={_pools.Count} ----</color>");
    }
    
}