using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PrefabPool
{
    readonly AssetReferenceGameObject _prefabRef;
    readonly Stack<GameObject> _pool = new();
    AsyncOperationHandle<GameObject>? _prefabHandle;
    int _alive; // 풀 보관 포함, 씬에 살아있는 총 인스턴스 수
    
    public PrefabPool(AssetReferenceGameObject prefabRef) => _prefabRef = prefabRef;

    async UniTask<GameObject> EnsureLoaded()
    {
        if (!_prefabHandle.HasValue)
            _prefabHandle = _prefabRef.LoadAssetAsync<GameObject>();
        return await _prefabHandle.Value.Task;
    }
    
    public async UniTask<GameObject> GetInstance(Transform parent)
    {
        await EnsureLoaded();
        var go = _pool.Count > 0 ? _pool.Pop() : Object.Instantiate(_prefabHandle!.Value.Result);
        _alive++;
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        go.SetActive(true);
        return go;
    }
    
    public void ReleaseInstance(GameObject go, Transform poolRoot = null)
    {
        if (!go) return;
        go.SetActive(false);
        if (poolRoot) go.transform.SetParent(poolRoot, false);
        _pool.Push(go);
        _alive--;
    }
    public void Dispose()
    {
        while (_pool.Count > 0) Object.Destroy(_pool.Pop());
        if (_prefabHandle.HasValue && _alive <= 0)
        {
            Addressables.Release(_prefabHandle.Value);
            _prefabHandle = null;
        }
    }
}