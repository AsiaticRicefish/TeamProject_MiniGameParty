using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PrefabPool
{
    readonly AssetReferenceGameObject _prefabRef;
    readonly Stack<GameObject> _pool = new();
    readonly HashSet<GameObject> _inPool = new(); // 중복 방지용
    AsyncOperationHandle<GameObject>? _prefabHandle;
    int _alive; // 풀 보관 포함, 씬에 살아있는 총 인스턴스 수
    
    
    public string Id { get; set; } // 레지스트리에서 설정해줄 ID
    public int PooledCount => _pool.Count;
    public int AliveCount => _alive;
    
    public PrefabPool(AssetReferenceGameObject prefabRef) => _prefabRef = prefabRef;

    async UniTask<GameObject> EnsureLoaded()
    {
        if (!_prefabHandle.HasValue)
            _prefabHandle = _prefabRef.LoadAssetAsync<GameObject>();
        var h = _prefabHandle.Value;
        try
        {
            var prefab = await h.Task;
            return prefab;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red>[PrefabPool] LoadAssetAsync 실패: {_prefabRef.RuntimeKey} - {e.Message}</color>");
            throw;
        }
    }
    
    public async UniTask<GameObject> GetInstance(Transform parent)
    {
        await EnsureLoaded();
        var go = _pool.Count > 0 ? _pool.Pop() : Object.Instantiate(_prefabHandle!.Value.Result);
        if (_inPool.Count > 0) _inPool.Remove(go); // ★ 풀에서 꺼낼 때 제거

        _alive++;
        var t = go.transform;
        t.SetParent(parent, false);
        t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        go.SetActive(true);
        return go;
    }
    
    public void ReleaseInstance(GameObject go, Transform poolRoot = null)
    {
        if (!go) return;
        
        // 이미 풀에 들어간 걸 또 release하려는 경우 방지
        if (_inPool.Contains(go))
        {
            Debug.LogWarning($"<color=red>[PrefabPool] Duplicate release detected: {go.name}</color>");
            return;
        }
        
        go.SetActive(false);
        if (poolRoot) go.transform.SetParent(poolRoot, false);
        _pool.Push(go);
        _inPool.Add(go); 
        _alive = Mathf.Max(0, _alive - 1);
    }
    public void Dispose()
    {
        // DumpState("<color=red>Dispose-Before</color>");
        while (_pool.Count > 0) Object.Destroy(_pool.Pop());
        _inPool?.Clear(); // ★ 선택

        if (_prefabHandle.HasValue && _alive <= 0)
        {
            Addressables.Release(_prefabHandle.Value);
            _prefabHandle = null;
        }
        // DumpState("<color=red>Dispose-After</color>");
    }
    
    public void DumpState(string tag = "")
    {
        string id = string.IsNullOrEmpty(Id) ? "(no-id)" : Id;
        string handleStr;
        if (_prefabHandle.HasValue)
        {
            var h = _prefabHandle.Value;
            handleStr =
                $"<color=red>HasHandle=Y, IsValid={h.IsValid()}, IsDone={h.IsDone}, Status={h.Status}, " +
                $"Result={(h.IsValid() && h.Result ? h.Result.name : "null")}</color>";
        }
        else
        {
            handleStr = "HasHandle=N";
        }
        Debug.Log($"<color=red>[PrefabPool][{tag}] id={id} poolCount={_pool.Count} alive={_alive} | {handleStr}</color>");
    }

}