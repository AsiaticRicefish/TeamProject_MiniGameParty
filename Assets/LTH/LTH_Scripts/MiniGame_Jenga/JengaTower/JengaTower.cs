using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class JengaTower : MonoBehaviour
{
    [Header("Ÿ�� ����")]
    [Tooltip("�� ��(������ ��ĵ �� �ڵ� ���)")]
    public int towerHeight;

    // ������ ǥ��/���� Ű
    public int ownerActorNumber { get; private set; }
    public string ownerUid { get; private set; }

    [Header("��� ����")]
    public readonly List<JengaBlock> allBlocks = new();                         // ��ü ���
    private readonly Dictionary<int, List<JengaBlock>> _blocksByLayer = new();  // layer -> blocks
    private readonly HashSet<int> _removedBlockIds = new();
    private readonly List<JengaBlock> _removableCache = new();

    [Header("������ & ����¡")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private float blockWidth = 1.0f;    // �� ��
    [SerializeField] private float blockDepth = 0.3f;    // ª�� ��
    [SerializeField] private float blockHeight = 0.3f;   // ���� (y)
    [SerializeField] private float blockGap = 0.01f;     // ��� ���� �̼� ����

    [Header("������ ����")]
    [SerializeField, Range(0f, 30f)] private float tiltFailAngle = 15f; // ����� �� ���� �̻� ���� �Ҿ���
    [SerializeField] private float dropFailY = -0.2f;                   // �ٴ� ���� ���� ���ġ
    [SerializeField] private bool allowTopRemoval = true;               // �ֻ�� ���� ��� ����
    [SerializeField, Min(0)] private int topSafeLayers = 1;             // �ֻ�� ��ȣ�� ��

    [Header("������ ��� �ɼ�")]
    [SerializeField] private bool preferLayerByParentName = true; // "Layer_0" �θ�� �켱
    [SerializeField] private string layerPrefix = "Layer_";
    [SerializeField] private bool allowTopRemovalInPrefab = true; // ������ ��� �⺻��
    [SerializeField, Range(0.0005f, 0.05f)]
    private float yQuantizeEpsilon = 0.01f; // Y �׷��� ���� ���ġ

    private bool _isCollapsed = false;

    public event Action<int> OnBlockRemoved;
    public event Action OnTowerCollapsed;

    public void InitializeOwner(int actorNumber, string uid)
    {
        ownerActorNumber = actorNumber;
        ownerUid = uid;
    }

    public void InitializeFromExistingHierarchy()
    {
        ResetRuntimeState();

        if (!TryInferSizeFromPrefabOrChildren())
            Debug.LogWarning("[JengaTower - InitializeFromExistingHierarchy] Could not infer size from prefab/children. Using inspector values.");

        var blocks = GetComponentsInChildren<JengaBlock>(includeInactive: true);
        if (blocks == null || blocks.Length == 0)
        {
            Debug.LogError("[JengaTower - InitializeFromExistingHierarchy] �� Ÿ������ ���� ����� ã�� �� �����ϴ�.");
            return;
        }

        Dictionary<int, List<JengaBlock>> byLayer;

        if (preferLayerByParentName && TryGroupByParentName(blocks, out byLayer))
        {

        }
        else
        {
            byLayer = GroupByY(blocks, blockHeight, yQuantizeEpsilon);
        }

        AssignIdsAndSlots(byLayer);

        towerHeight = _blocksByLayer.Count > 0 ? _blocksByLayer.Keys.Max() + 1 : 0;
        allowTopRemoval = allowTopRemovalInPrefab;

        RebuildRemovableCache(forceRelaxIfEmpty: true);

        Debug.Log($"[JengaTower - InitializeFromExistingHierarchy] Prefab scan complete. blocks = {allBlocks.Count}, layers = {_blocksByLayer.Count}, height = {towerHeight}");
    }

    public void Initialize(GameObject prefab, int height)
    {
        ResetRuntimeState();
        blockPrefab = prefab;
        towerHeight = Mathf.Max(1, height);

        if (!TryInferSizeFromPrefabOrChildren())
            Debug.LogWarning("[JengaTower - Initialize] Size inference failed from prefab. Using inspector values.");

        BuildTowerProcedurally();
        RebuildRemovableCache(forceRelaxIfEmpty: false);
    }

    public List<JengaBlock> GetRemovableBlocks()
        => _removableCache.Where(b => !b.IsRemoved).ToList();

    public void ApplyBlockRemoval(int blockId, bool withAnimation)
        => RemoveBlockInternal(blockId, withAnimation, raiseEvent: false);

    public void RemoveBlock(int blockId, bool withAnimation = true)
        => RemoveBlockInternal(blockId, withAnimation, raiseEvent: true);

    public IReadOnlyCollection<int> GetRemovedBlockIds() => _removedBlockIds;

    public void ApplyRemovedBlocks(IEnumerable<int> removedIds, bool withAnimation = false)
    {
        foreach (var id in removedIds)
            if (!_removedBlockIds.Contains(id))
                RemoveBlockInternal(id, withAnimation, raiseEvent: false);

        if (!IsStable()) TriggerCollapseOnce();
    }

    public int GetRemainingBlocks() => allBlocks.Count(b => !b.IsRemoved);

    public float GetTowerStabilityScore()
    {
        int remain = GetRemainingBlocks();
        float density = (float)remain / Mathf.Max(1, allBlocks.Count);
        int top = GetTopCompleteLayer();
        float topBonus = (top + 1f) / Mathf.Max(1, towerHeight);
        return Mathf.Clamp01(0.7f * density + 0.3f * topBonus);
    }

    public JengaBlock GetBlockById(int id)
        => (id >= 0 && id < allBlocks.Count) ? allBlocks[id] : null;

    private void ResetRuntimeState()
    {
        allBlocks.Clear();
        _blocksByLayer.Clear();
        _removedBlockIds.Clear();
        _removableCache.Clear();
        _isCollapsed = false;
    }

    private bool TryInferSizeFromPrefabOrChildren()
    {
        if (blockPrefab != null)
        {
            var col = blockPrefab.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                var lossy = col.transform.lossyScale;
                blockHeight = Mathf.Abs(col.size.y * lossy.y);
                float x = Mathf.Abs(col.size.x * lossy.x);
                float z = Mathf.Abs(col.size.z * lossy.z);
                if (x >= z) { blockWidth = x; blockDepth = z; } else { blockWidth = z; blockDepth = x; }
                return true;
            }
        }
        var anyBlock = GetComponentInChildren<JengaBlock>();
        if (anyBlock != null)
        {
            var col = anyBlock.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                var lossy = col.transform.lossyScale;
                blockHeight = Mathf.Abs(col.size.y * lossy.y);
                float x = Mathf.Abs(col.size.x * lossy.x);
                float z = Mathf.Abs(col.size.z * lossy.z);
                if (x >= z) { blockWidth = x; blockDepth = z; } else { blockWidth = z; blockDepth = x; }
                return true;
            }
        }
        return false;
    }

    private static Dictionary<int, List<JengaBlock>> GroupByY(IEnumerable<JengaBlock> blocks, float unitHeight, float eps)
    {
        var ys = new SortedDictionary<int, List<JengaBlock>>();
        float h = Mathf.Max(0.0001f, unitHeight);

        foreach (var b in blocks)
        {
            float y = b.transform.localPosition.y / h;
            int key = Mathf.RoundToInt(y);

            float yReal = b.transform.localPosition.y;
            float yRef = key * h;
            if (Mathf.Abs(yReal - yRef) > eps) key = Mathf.RoundToInt(yReal / h);

            if (!ys.TryGetValue(key, out var list)) { list = new List<JengaBlock>(); ys[key] = list; }
            list.Add(b);
        }

        var result = new Dictionary<int, List<JengaBlock>>();
        int layer = 0;
        foreach (var kv in ys)
            result[layer++] = kv.Value;
        return result;
    }

    private bool TryGroupByParentName(IEnumerable<JengaBlock> blocks, out Dictionary<int, List<JengaBlock>> byLayer)
    {
        byLayer = null;

        var groups = new Dictionary<int, List<JengaBlock>>();
        foreach (var b in blocks)
        {
            var p = b.transform.parent;
            if (p == null || !p.name.StartsWith(layerPrefix)) { groups.Clear(); return false; }
            if (!int.TryParse(p.name.Substring(layerPrefix.Length), out var idx)) { groups.Clear(); return false; }
            if (!groups.TryGetValue(idx, out var list)) { list = new List<JengaBlock>(); groups[idx] = list; }
            list.Add(b);
        }

        var result = new Dictionary<int, List<JengaBlock>>();
        foreach (var idx in groups.Keys.OrderBy(i => i))
            result[result.Count] = groups[idx];

        byLayer = result;
        return true;
    }

    private void AssignIdsAndSlots(Dictionary<int, List<JengaBlock>> byLayer)
    {
        int id = 0;
        foreach (var kv in byLayer.OrderBy(k => k.Key))
        {
            int layer = kv.Key;
            var list = kv.Value;

            bool isHorizontal = (layer % 2 == 0);

            Transform commonParent = list[0].transform.parent;
            if (commonParent != null)
            {
                float y = Mathf.Abs(Mathf.DeltaAngle(commonParent.eulerAngles.y, 0f));
                if (Mathf.Abs(y) > 45f) isHorizontal = false;
                else isHorizontal = true;
            }

            list = isHorizontal
                ? list.OrderBy(b => b.transform.localPosition.x).ToList()
                : list.OrderBy(b => b.transform.localPosition.z).ToList();

            _blocksByLayer[layer] = new List<JengaBlock>(3);

            for (int slot = 0; slot < list.Count; slot++)
            {
                var b = list[slot];
                b.Initialize(id, layer, slot, ownerActorNumber, ownerUid);
                allBlocks.Add(b);
                _blocksByLayer[layer].Add(b);
                id++;
            }
        }
    }

    private void BuildTowerProcedurally()
    {
        for (int layer = 0; layer < towerHeight; layer++)
        {
            bool isHorizontal = (layer % 2 == 0);

            var layerRoot = new GameObject($"{layerPrefix}{layer}");
            layerRoot.transform.SetParent(transform, false);
            layerRoot.transform.localPosition = Vector3.up * (layer * blockHeight);
            layerRoot.transform.localRotation = isHorizontal ? Quaternion.identity : Quaternion.Euler(0, 90f, 0f);

            for (int slot = 0; slot < 3; slot++)
            {
                Vector3 localPos = LocalPosForLayerSlot(slot, isHorizontal);
                var worldPos = layerRoot.transform.TransformPoint(localPos);

                var blockObj = Instantiate(blockPrefab, worldPos, layerRoot.transform.rotation, layerRoot.transform);
                if (!blockObj.TryGetComponent<JengaBlock>(out var jb))
                    jb = blockObj.AddComponent<JengaBlock>();

                int id = allBlocks.Count;
                jb.Initialize(id, layer, slot, ownerActorNumber, ownerUid);

                allBlocks.Add(jb);
                if (!_blocksByLayer.TryGetValue(layer, out var list))
                    _blocksByLayer[layer] = list = new List<JengaBlock>(3);
                list.Add(jb);
            }
        }
    }

    private Vector3 LocalPosForLayerSlot(int slot, bool isHorizontal)
    {
        float centerIndex = 1f;
        float offset = (slot - centerIndex) * (blockWidth + blockGap);
        return isHorizontal ? new Vector3(offset, 0f, 0f) : new Vector3(0f, 0f, offset);
    }

    private void RemoveBlockInternal(int blockId, bool withAnimation, bool raiseEvent)
    {
        var block = (blockId >= 0 && blockId < allBlocks.Count) ? allBlocks[blockId] : null;
        if (block == null || block.IsRemoved) return;

        if (withAnimation) block.RemoveWithAnimation();
        else block.RemoveImmediately();

        _removedBlockIds.Add(blockId);
        RebuildRemovableCache(forceRelaxIfEmpty: false);

        if (!IsStable())
            TriggerCollapseOnce();

        if (raiseEvent)
            OnBlockRemoved?.Invoke(blockId);
    }

    private void RebuildRemovableCache(bool forceRelaxIfEmpty)
    {
        _removableCache.Clear();
        if (towerHeight <= 0) return;

        int maxLayer = _blocksByLayer.Keys.Count > 0 ? _blocksByLayer.Keys.Max() : 0;

        int protectedFrom = allowTopRemoval ? int.MaxValue : Mathf.Max(0, maxLayer - topSafeLayers + 1);

        for (int layer = 1; layer < towerHeight; layer++)
        {
            if (layer >= protectedFrom) continue;
            if (!_blocksByLayer.TryGetValue(layer, out var list)) continue;

            var alive = list.Where(b => !b.IsRemoved).ToList();
            if (alive.Count >= 2)
                _removableCache.AddRange(alive);
        }

        if (allowTopRemoval)
        {
            int top = GetTopCompleteLayer();
            if (top >= 0 && _blocksByLayer.TryGetValue(top, out var topList))
            {
                var alive = topList.Where(b => !b.IsRemoved).ToList();
                if (alive.Count >= 2)
                    _removableCache.AddRange(alive);
            }
        }

        if (forceRelaxIfEmpty && _removableCache.Count == 0)
        {
            foreach (var kv in _blocksByLayer)
            {
                var alive = kv.Value.Where(b => !b.IsRemoved).ToList();
                if (alive.Count >= 2) _removableCache.AddRange(alive);
            }
        }
    }

    private int GetTopCompleteLayer()
    {
        for (int layer = towerHeight - 1; layer >= 0; layer--)
        {
            if (_blocksByLayer.TryGetValue(layer, out var list))
            {
                int alive = list.Count(b => !b.IsRemoved);
                if (alive == 3) return layer;
            }
        }
        return -1;
    }


    public bool IsStable()
    {
        if (_isCollapsed) return false;

        foreach (var kv in _blocksByLayer)
            if (kv.Value.All(b => b.IsRemoved))
                return false;

        foreach (var b in allBlocks)
        {
            if (b.IsRemoved) continue;
            var t = b.transform;

            float tiltX = Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.x, 0f));
            float tiltZ = Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.z, 0f));
            if (Mathf.Max(tiltX, tiltZ) >= tiltFailAngle) return false;

            if (t.position.y < transform.position.y + dropFailY) return false;
        }
        return true;
    }

    private void TriggerCollapseOnce()
    {
        if (_isCollapsed) return;
        _isCollapsed = true;
        StartCoroutine(CollapseAnimation());
        OnTowerCollapsed?.Invoke();
    }

    private IEnumerator CollapseAnimation()
    {
        foreach (var block in allBlocks.Where(b => !b.IsRemoved))
        {
            if (block.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.AddForce(UnityEngine.Random.insideUnitSphere * 5f, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);
            }
            yield return new WaitForSeconds(0.03f);
        }

        yield return new WaitForSeconds(5f);
        foreach (var block in allBlocks)
            if (block) block.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            transform.position + Vector3.up * (towerHeight * blockHeight * 0.5f),
            new Vector3(blockWidth * 3f + 0.2f, towerHeight * blockHeight, blockWidth * 3f + 0.2f)
        );
    }
#endif
}