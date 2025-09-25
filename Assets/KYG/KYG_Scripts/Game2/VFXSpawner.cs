using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 간단한 이름 기반 풀링 스포너.
/// Resources/ 또는 Addressables를 쓰고 있다면, Loader만 교체하면 됩니다.
/// </summary>
public class VFXSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string key;           // "VFX_Explosion_Round"
        public GameObject prefab;    // 해당 프리팹
        public int warmCount = 2;
    }

    [SerializeField] private List<Entry> entries = new();

    private readonly Dictionary<string, Queue<GameObject>> _pool = new();
    private readonly Dictionary<GameObject, string> _rev  = new();

    private void Awake()
    {
        foreach (var e in entries)
        {
            if (!_pool.ContainsKey(e.key)) _pool[e.key] = new Queue<GameObject>();
            for (int i = 0; i < e.warmCount; i++)
            {
                var go = Instantiate(e.prefab, transform);
                go.SetActive(false);
                _pool[e.key].Enqueue(go);
                _rev[go] = e.key;
            }
        }
    }

    public GameObject Spawn(string key, Vector3 pos, Quaternion rot, float autoDespawn = 0f)
    {
        if (!_pool.TryGetValue(key, out var q) || q.Count == 0)
        {
            var entry = entries.Find(x => x.key == key);
            if (entry == null || entry.prefab == null) { Debug.LogWarning($"[VFXSpawner] Missing key {key}"); return null; }
            // 새로 할당
            var extra = Instantiate(entry.prefab, transform);
            _rev[extra] = key;
            return Activate(extra, pos, rot, autoDespawn);
        }
        var go = q.Dequeue();
        return Activate(go, pos, rot, autoDespawn);
    }

    public void Despawn(GameObject go)
    {
        if (go == null) return;
        if (!_rev.TryGetValue(go, out var key)) { Destroy(go); return; }
        go.SetActive(false);
        go.transform.SetParent(transform);
        _pool[key].Enqueue(go);
    }

    private GameObject Activate(GameObject go, Vector3 pos, Quaternion rot, float auto)
    {
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.SetParent(null);
        go.SetActive(true);
        if (auto > 0f) StartCoroutine(Co_Auto(go, auto));
        return go;
    }

    private System.Collections.IEnumerator Co_Auto(GameObject go, float t)
    {
        yield return new WaitForSeconds(t);
        Despawn(go);
    }
}
