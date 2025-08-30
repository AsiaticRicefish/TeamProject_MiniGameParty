using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class JengaTower : MonoBehaviour
{
    [Header("타워 정보")]
    [Tooltip("층 수(프리팹 스캔 시 자동 계산)")]
    public int towerHeight;

    // 소유자 표준/보조 키
    public int ownerActorNumber { get; private set; }
    public string ownerUid { get; private set; }

    [Header("블록 관리")]
    public readonly List<JengaBlock> allBlocks = new();                         // 전체 블록
    private readonly Dictionary<int, List<JengaBlock>> _blocksByLayer = new();  // layer -> blocks
    private readonly HashSet<int> _removedBlockIds = new();
    private readonly List<JengaBlock> _removableCache = new();

    [Header("프리팹 & 사이징")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private float blockWidth = 1.0f;    // 긴 변
    [SerializeField] private float blockDepth = 0.3f;    // 짧은 변
    [SerializeField] private float blockHeight = 0.3f;   // 높이 (y)
    [SerializeField] private float blockGap = 0.01f;     // 블록 사이 미세 간격

    [Header("안정성 판정")]
    [SerializeField, Range(0f, 30f)] private float tiltFailAngle = 15f; // 블록이 이 각도 이상 기울면 불안정
    [SerializeField] private float dropFailY = -0.2f;                   // 바닥 기준 낙하 허용치
    [SerializeField] private bool allowTopRemoval = true;               // 최상단 제거 허용 여부
    [SerializeField, Min(0)] private int topSafeLayers = 1;             // 최상단 보호층 수

    [Header("난이도 시스템")]
    [SerializeField] private int blocksRemovedCount = 0;      // 제거된 블록 수 (성공한 제거만 카운트)

    [Header("프리팹 모드 옵션")]
    [SerializeField] private bool preferLayerByParentName = true; // "Layer_0" 부모명 우선
    [SerializeField] private string layerPrefix = "Layer_";
    [SerializeField] private bool allowTopRemovalInPrefab = true; // 프리팹 모드 기본값
    [SerializeField, Range(0.0005f, 0.05f)]
    private float yQuantizeEpsilon = 0.01f; // Y 그룹핑 오차 허용치

    private bool _isCollapsed = false;

    // 층별 사이드 제거 모드 상태 : layer -> expectedSide(0 또는 2)
    private readonly Dictionary<int, int> _pairSessionExpectedSide = new();

    public event Action<int> OnBlockRemoved;
    public event Action OnTowerCollapsed;

    #region Session for side-pair removal
    public bool IsPairSessionActiveOn(int layer)
        => _pairSessionExpectedSide.ContainsKey(layer);

    public int? GetExpectedSide(int layer)
        => _pairSessionExpectedSide.TryGetValue(layer, out var side) ? side : null;

    public void BeginPairSession(int layer, int firstSideIndex)
    {
        int opposite = (firstSideIndex == 0) ? 2 : 0;
        _pairSessionExpectedSide[layer] = opposite;
        RebuildRemovableCache(false);
    }

    public void EndPairSession(int layer)
    {
        if (_pairSessionExpectedSide.Remove(layer))
            RebuildRemovableCache(false);
    }

    private void ValidateOrCancelPairSession(int layer)
    {
        if (!IsPairSessionActiveOn(layer)) return;
        if (!_blocksByLayer.TryGetValue(layer, out var list)) { EndPairSession(layer); return; }
        int alive = list.Count(b => !b.IsRemoved);
        // 세션은 “센터+사이드=2개” 상태에서만 유효
        if (alive != 2) EndPairSession(layer);
    }

    #endregion

    public bool CanRemoveBlock(JengaBlock b)
    {
        if (b == null || b.IsRemoved) return false;

        int topAlive = GetTopAliveLayer();
        if (!allowTopRemoval && topAlive >= 0)
        {
            int firstProtected = Mathf.Max(0, topAlive - (topSafeLayers - 1));
            if (b.Layer >= firstProtected && b.Layer <= topAlive)
                return false;
        }

        if (!_blocksByLayer.TryGetValue(b.Layer, out var list)) return false;

           // 세션 유효성 검증(해당 레이어만)
        ValidateOrCancelPairSession(b.Layer);

        var alive = list.Where(x => !x.IsRemoved).OrderBy(x => x.IndexInLayer).ToList();

        // 세션 중: 해당 레이어에서는 expectedSide만 허용(센터 클릭 금지)
        if (IsPairSessionActiveOn(b.Layer))
        {
            var expected = GetExpectedSide(b.Layer);
            return expected.HasValue && b.IndexInLayer == expected.Value;
        }

        // 평상 시 : 센터 쪽만 단일 제거 허용 (사이드는 세션 경로로만 진행)
        if (alive.Count == 3)
        {
            // 3개 모두 살아있으면 가운데 하나만 제거 가능
            return b.IndexInLayer == 1;
        }
        else if (alive.Count == 2)
        {
            // 세션이 아니라면(첫 사이드 성공 직후가 아님) 허용하지 않음
            return false;
        }

        // 그 외(1개 이하)는 제거 불가
        return false;

        #region 단순히 2개만 빠질 경우 나머지 하나는 안 빠지도록 제한
        //int alive = 0;
        //foreach (var x in list) if (!x.IsRemoved) alive++;
        //return alive >= 2;
        #endregion
    }

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
            Debug.LogError("[JengaTower - InitializeFromExistingHierarchy] 이 타워에서 젠가 블록을 찾을 수 없습니다.");
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

    public void ApplyBlockRemoval(int blockId, bool withAnimation, bool isSuccess = true)
        => RemoveBlockInternal(blockId, withAnimation, raiseEvent: false, isSuccess);

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
        int top = GetTopAliveLayer();
        float topBonus = (top + 1f) / Mathf.Max(1, towerHeight);
        return Mathf.Clamp01(0.7f * density + 0.3f * topBonus);
    }

    public JengaBlock GetBlockById(int id)
        => (id >= 0 && id < allBlocks.Count) ? allBlocks[id] : null;

    /// <summary>
    /// 현재 플레이어의 제거된 블록 수 반환
    /// </summary>
    public int GetRemovedBlocksCount()
    {
        return blocksRemovedCount;
    }

    private void ResetRuntimeState()
    {
        allBlocks.Clear();
        _blocksByLayer.Clear();
        _removedBlockIds.Clear();
        _removableCache.Clear();
        _isCollapsed = false;
        blocksRemovedCount = 0;
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

    private void RemoveBlockInternal(int blockId, bool withAnimation, bool raiseEvent, bool isSuccess = true)
    {
        var block = (blockId >= 0 && blockId < allBlocks.Count) ? allBlocks[blockId] : null;
        if (block == null || block.IsRemoved) return;

        if (withAnimation) block.RemoveWithAnimation();
        else block.RemoveImmediately();

        _removedBlockIds.Add(blockId);

        // 성공한 제거만 난이도 계산에 포함
        if (isSuccess)
        {
            blocksRemovedCount++;
        }

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

        int topAlive = GetTopAliveLayer();

        bool IsProtected(int layer)
        {
            if (allowTopRemoval) return false;
            if (topAlive < 0) return false;
            int firstProtected = Mathf.Max(0, topAlive - (topSafeLayers - 1));
            return layer >= firstProtected && layer <= topAlive;
        }

        for (int layer = 0; layer < towerHeight; layer++)
        {
            if (!_blocksByLayer.TryGetValue(layer, out var list)) continue;
            if (IsProtected(layer)) continue;

            // 세션 유효성 체크(레이어별)
            ValidateOrCancelPairSession(layer);

            var alive = list.Where(b => !b.IsRemoved).OrderBy(b => b.IndexInLayer).ToList();

            if (IsPairSessionActiveOn(layer))
            {
                var expected = GetExpectedSide(layer);
                if (expected.HasValue)
                {
                    var target = alive.FirstOrDefault(x => x.IndexInLayer == expected.Value);
                    if (target != null) _removableCache.Add(target);
                }
                continue;
            }

            if (alive.Count == 3)
            {
                var center = alive.FirstOrDefault(x => x.IndexInLayer == 1);
                if (center != null) _removableCache.Add(center);
            }

            #region 단순히 2개가 빠졌을 경우 나머지 하나는 안 빠지도록 제한
            //int alive = 0;
            //foreach (var b in list) if (!b.IsRemoved) alive++;
            //if (alive >= 2)
            //    foreach (var b in list) if (!b.IsRemoved) _removableCache.Add(b);
            #endregion
        }

        #region Don't use this logic for now
        //if (forceRelaxIfEmpty && _removableCache.Count == 0 && topAlive >= 0)
        //{
        //    for (int layer = topAlive; layer >= 0; layer--)
        //    {
        //        if (!_blocksByLayer.TryGetValue(layer, out var list)) continue;
        //        int alive = 0;
        //        foreach (var b in list) if (!b.IsRemoved) alive++;
        //        if (alive >= 2)
        //        {
        //            foreach (var b in list) if (!b.IsRemoved) _removableCache.Add(b);
        //            break;
        //        }
        //    }
        //}
        #endregion
    }

    private int GetTopAliveLayer()
    {
        for (int layer = towerHeight - 1; layer >= 0; layer--)
        {
            if (_blocksByLayer.TryGetValue(layer, out var list))
                if (list.Any(b => !b.IsRemoved)) return layer;
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

    public void TriggerCollapseOnce()
    {
        if (_isCollapsed) return;
        _isCollapsed = true;
        StartCoroutine(CollapseAnimation());
        OnTowerCollapsed?.Invoke();
    }

    private IEnumerator CollapseAnimation()
    {
        // 타워 루트를 기울이기
        float tiltAngle = UnityEngine.Random.Range(15f, 25f);
        Vector3 tiltAxis = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            0f,
            UnityEngine.Random.Range(-1f, 1f)
        ).normalized;

        // 타워 전체를 천천히 기울이기
        float duration = 1f;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.AngleAxis(tiltAngle, tiltAxis) * startRot;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Lerp(startRot, targetRot, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 이제 모든 블록을 물리 적용 (중력만으로)
        foreach (var block in allBlocks.Where(b => !b.IsRemoved))
        {
            if (block.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
            }
            yield return new WaitForSeconds(0.02f);
        }

        yield return new WaitForSeconds(5f);

        foreach (var block in allBlocks)
        {
            if (block) block.gameObject.SetActive(false);
        }
    }

    public void ConfigureTopProtection(bool allowTopRemoval, int topSafeLayers = 1)
    {
        this.allowTopRemoval = allowTopRemoval;
        this.topSafeLayers = Mathf.Max(0, topSafeLayers);
        RebuildRemovableCache(false);
    }

    // 같은 레이어의 반대편 사이드 블록 찾기(보기용 하이라이트 등에 사용)
    public JengaBlock GetOppositeSideInLayer(int layer, int indexInLayer)
    {
        if (!_blocksByLayer.TryGetValue(layer, out var list)) return null;
        int opposite = (indexInLayer == 0) ? 2 : (indexInLayer == 2 ? 0 : -1);
        if (opposite < 0) return null;
        return list.FirstOrDefault(b => !b.IsRemoved && b.IndexInLayer == opposite);
    }
}