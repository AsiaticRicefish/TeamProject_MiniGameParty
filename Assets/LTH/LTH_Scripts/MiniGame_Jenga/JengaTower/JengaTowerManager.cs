using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

public class JengaTowerManager : CombinedSingleton<JengaTowerManager>, IGameComponent
{
    public enum BuildMode { Procedural, Prefab }

    [Header("타워 모드")]
    [SerializeField] private BuildMode buildMode = BuildMode.Procedural;

    [Header("프리팹 모드")]
    [SerializeField] private GameObject towerPrefab; // ← 완성된 타워 프리팹(자식들에 JengaBlock 붙어 있음)

    #region 타워 직접 생성 (사용 안함)
    [Header("타워 설정")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private int towerHeight = 13;
    #endregion

    [Header("배치(아레나 앵커)")]
    [Tooltip("개별 아레나의 TowerAnchor들을 등록 (Arena_i/TowerAnchor)")]
    [SerializeField] private List<Transform> arenaTowerAnchors = new(); // 인스펙터에 드래그 등록

    [Tooltip("Instantiate 시 타워를 앵커 하위로 붙일지 여부")]
    [SerializeField] private bool parentTowerUnderAnchor = true;

    [Header("카메라 앵커")]
    [SerializeField] private List<Transform> arenaCameraAnchors = new();

    [SerializeField] private string[] arenaLayerNames = { "Arena_0", "Arena_1", "Arena_2", "Arena_3" };

    [SerializeField] private Transform towersParent;

    private readonly Dictionary<int, JengaTower> _playerTowers = new(); // ActorNumber → Tower

    // 룸 커스텀 프로퍼티 키 (관전자/재접속 대비 슬롯 고정)
    private const string ROOMKEY_SLOTS = "JG_SLOTS";

    // 네트워크 적용 중 이벤트 재브로드캐스트 방지 플래그
    private bool _suppressCollapseBroadcast;

    public void WithSuppressedCollapse(Action action)
    {
        _suppressCollapseBroadcast = true;
        action?.Invoke();
        _suppressCollapseBroadcast = false;
    }

    public bool IsSuppressingCollapse => _suppressCollapseBroadcast;


    protected override void OnAwake()
    {
        base.isPersistent = false;

        // 부모 트랜스폼이 비어 있으면 안전하게 하나 만들어 둔다
        if (towersParent == null)
        {
            var go = new GameObject("JengaTowersRoot");
            towersParent = go.transform;
        }
    }

    public void Initialize()
    {
        // 마스터에서만 슬롯맵 고정(오프라인/비마스터는 읽기만)
        EnsureSlotMap();
        CreateAllPlayerTowers();
        Debug.Log("[JengaTowerManager] 초기화 완료 - 개별 타워 생성");
    }

    #region Camera Anchor 찾기
    private Transform GetCameraAnchor(int slotIndex)
    {
        // 병렬 리스트가 있으면 우선 사용
        if (arenaCameraAnchors != null && arenaCameraAnchors.Count > 0)
        {
            var idx = Mathf.Abs(slotIndex) % arenaCameraAnchors.Count;
            var t = arenaCameraAnchors[idx];
            if (t) return t;
        }

        // 2) TowerAnchor의 자식에서 "CameraAnchor" 이름으로 탐색
        var towerAnchor = GetPlayerTowerAnchor(slotIndex);
        if (towerAnchor)
        {
            var child = towerAnchor.Find("CameraAnchor");
            if (child) return child;

            child = FindDeepChild(towerAnchor, "CameraAnchor");
            if (child) return child;
        }

        Debug.LogWarning($"[JengaTowerManager] CameraAnchor not found for slot={slotIndex}. Fallback to TowerAnchor.");
        return towerAnchor ? towerAnchor : towersParent;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var r = FindDeepChild(c, name);
            if (r) return r;
        }
        return null;
    }

    #endregion


    #region Tower Creation

    // 초기화 단계에서는 p == null이어도 강제로 생성
    private void CreateAllPlayerTowers(bool allowNullPlayer = true)
    {
        _playerTowers.Clear();

        // 슬롯 맵: 시작 시점에 확정된 순서 (관전자 포함 이슈 방지)
        var slotActors = GetSlotMap(); // int[] actorNumbers

        for (int i = 0; i < slotActors.Length; i++)
        {
            var actorNumber = slotActors[i];
            var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);

            if (!allowNullPlayer && p == null)
            {
                // 런타임 중 재호출일 경우엔 탈주 처리
                continue;
            }

            string ownerUid = TryGetUid(p);

            CreatePlayerTower(actorNumber, i, ownerUid);
        }
    }

    private void CreatePlayerTower(int actorNumber, int slotIndex, string ownerUid)
    {
        // 앵커 기준 위치/회전
        var anchor = GetPlayerTowerAnchor(slotIndex);
        var pos = anchor ? anchor.position : Vector3.zero;
        var rot = anchor ? anchor.rotation : Quaternion.identity;
        var parent = parentTowerUnderAnchor && anchor ? anchor : towersParent;

        GameObject towerRootGO;
        JengaTower tower;

        if (buildMode == BuildMode.Prefab)
        {
            // 완성된 타워 프리팹을 그대로 소환
            towerRootGO = Instantiate(towerPrefab, pos, rot, parent);
            towerRootGO.name = $"JengaTower_Player{actorNumber}";
            tower = towerRootGO.GetComponent<JengaTower>() ?? towerRootGO.AddComponent<JengaTower>();

            tower.InitializeOwner(actorNumber, ownerUid);
            tower.InitializeFromExistingHierarchy();

            ApplyArenaLayer(towerRootGO, slotIndex);
        }
        else
        {
            // 기존 프로시저럴 방식 유지
            towerRootGO = new GameObject($"JengaTower_Player{actorNumber}");
            towerRootGO.transform.SetParent(towersParent, false);
            towerRootGO.transform.SetPositionAndRotation(pos, rot);

            tower = towerRootGO.AddComponent<JengaTower>();
            tower.InitializeOwner(actorNumber, ownerUid);
            tower.Initialize(blockPrefab, towerHeight);

            ApplyArenaLayer(towerRootGO, slotIndex);
        }

        // 붕괴 이벤트 → 네트워크 통지(마스터만)
        tower.OnTowerCollapsed += () =>
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (IsSuppressingCollapse) return;

        JengaNetworkManager.Instance.RequestTowerCollapse_MasterAuth(actorNumber);
    };

        _playerTowers[actorNumber] = tower;
        tower.ConfigureTopProtection(allowTopRemoval: false, topSafeLayers: 1);

        if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            int slot = slotIndex; // 재계산하지 말고 생성에 사용한 동일 값 사용
            var cameraAnchor = GetCameraAnchor(slot);
            var lookTarget = tower.transform;

            var binder = FindFirstObjectByType<JengaLocalCameraBinder>(FindObjectsInactive.Include);
            if (binder)
            {
                binder.BindForLocal(actorNumber, slot, cameraAnchor, lookTarget, arenaLayerNames);
            }
        }

    }
    #endregion

    #region Slot Map (Room Properties)
    private void EnsureSlotMap()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        // 기존 슬롯맵이 있는지 확인하고, 현재 플레이어와 맞지 않으면 갱신
        bool needsUpdate = false;

        if (room.CustomProperties.TryGetValue(ROOMKEY_SLOTS, out var existingObj))
        {
            var existingSlots = ConvertToIntArray(existingObj);
            var currentActors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(x => x).ToArray();

            // 기존 슬롯과 현재 플레이어가 다르면 업데이트 필요
            if (!existingSlots.OrderBy(x => x).SequenceEqual(currentActors))
            {
                needsUpdate = true;
                Debug.Log($"[JengaTowerManager] Slot map mismatch - updating. Existing: [{string.Join(",", existingSlots)}], Current: [{string.Join(",", currentActors)}]");
            }
        }
        else
        {
            needsUpdate = true;
            Debug.Log("[JengaTowerManager] No existing slot map - creating new one");
        }

        if (needsUpdate)
        {
            var ordered = PhotonNetwork.PlayerList
                .OrderBy(p => p.ActorNumber)
                .Select(p => p.ActorNumber)
                .ToArray();

            var h = new ExitGames.Client.Photon.Hashtable { [ROOMKEY_SLOTS] = ordered };
            room.SetCustomProperties(h);
            Debug.Log($"[JengaTowerManager] Updated slot map: [{string.Join(",", ordered)}]");
        }
    }

    private int[] ConvertToIntArray(object obj)
    {
        if (obj is int[] arrInt) return arrInt;
        if (obj is object[] arrObj) return arrObj.Select(o => Convert.ToInt32(o)).ToArray();
        return new int[0];
    }

    private int[] GetSlotMap()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room != null && room.CustomProperties.TryGetValue(ROOMKEY_SLOTS, out var obj))
        {
            if (obj is int[] arrInt) return arrInt;
            if (obj is object[] arrObj) return arrObj.Select(o => Convert.ToInt32(o)).ToArray();
        }

        return PhotonNetwork.PlayerList
            .OrderBy(p => p.ActorNumber)
            .Select(p => p.ActorNumber)
            .ToArray();
    }

    /// <summary>
    /// ActorNumber → 슬롯 인덱스
    /// </summary>
    public int GetSlotIndexOf(int actorNumber)
    {
        var map = GetSlotMap();
        for (int i = 0; i < map.Length; i++)
            if (map[i] == actorNumber) return i;
        return 0;
    }
    #endregion

    #region Anchors & Utilities
    private Transform GetPlayerTowerAnchor(int slotIndex)
    {
        if (arenaTowerAnchors != null && arenaTowerAnchors.Count > 0)
        {
            var idx = Mathf.Abs(slotIndex) % arenaTowerAnchors.Count;
            return arenaTowerAnchors[idx];
        }
        return null;
    }

    private static string TryGetUid(Photon.Realtime.Player p)
    {
        if (p?.CustomProperties != null &&
            p.CustomProperties.TryGetValue("uid", out var uidObj))
        {
            return uidObj as string;
        }
        return null;
    }
    #endregion

    #region Public API
    public JengaTower GetPlayerTower(int actorNumber) =>
        _playerTowers.TryGetValue(actorNumber, out var t) ? t : null;

    public string GetOwnerUidByActor(int actorNumber) =>
    _playerTowers.TryGetValue(actorNumber, out var t) ? t.ownerUid : null;

    public void RemovePlayerBlock(int actorNumber, int blockId, bool withAnimation = true)
    {
        GetPlayerTower(actorNumber)?.RemoveBlock(blockId, withAnimation);
    }

    public Dictionary<int, bool> CheckAllTowersStability()
    {
        var dict = new Dictionary<int, bool>(_playerTowers.Count);
        foreach (var kv in _playerTowers)
            dict[kv.Key] = kv.Value.IsStable();
        return dict;
    }

    public Dictionary<int, IReadOnlyCollection<int>> SnapshotRemovedBlocks()
    {
        var snap = new Dictionary<int, IReadOnlyCollection<int>>(_playerTowers.Count);
        foreach (var kv in _playerTowers)
            snap[kv.Key] = kv.Value.GetRemovedBlockIds().ToArray();
        return snap;
    }

    public void ApplySnapshot(Dictionary<int, IReadOnlyCollection<int>> snapshot)
    {
        foreach (var kv in snapshot)
            GetPlayerTower(kv.Key)?.ApplyRemovedBlocks(kv.Value, withAnimation: false);
    }
    #endregion

    private void ApplyArenaLayer(GameObject root, int slotIndex)
    {
        var name = arenaLayerNames[slotIndex % arenaLayerNames.Length];
        int layer = LayerMask.NameToLayer(name);

        if (layer < 0) return;

        SetLayerRecursively(root, layer);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;

        foreach (Transform c in go.transform)
            SetLayerRecursively(c.gameObject, layer);
    }
}