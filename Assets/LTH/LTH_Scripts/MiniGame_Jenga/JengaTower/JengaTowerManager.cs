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

    [Header("배치")]
    [SerializeField] private Transform towersParent;
    [SerializeField]
    private Vector3[] fixedPositions = 
    {
        new (-8, 0,  8),
        new ( 8, 0,  8),
        new (-8, 0, -8),
        new ( 8, 0, -8),
    };
    #endregion

    private readonly Dictionary<int, JengaTower> _playerTowers = new(); // ActorNumber → Tower

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
        CreateAllPlayerTowers();
        Debug.Log("[JengaTowerManager] 초기화 완료 - 개별 타워 생성");
    }

    private void CreateAllPlayerTowers()
    {
        _playerTowers.Clear();

        // 슬롯 맵: 시작 시점에 확정된 순서(관전자 포함 이슈 방지)
        var slotActors = GetSlotMap(); // int[] actorNumbers

        for (int i = 0; i < slotActors.Length; i++)
        {
            var actorNumber = slotActors[i];
            var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);
            if (p == null) continue; // 떠난 플레이어 등
            CreatePlayerTower(actorNumber, i, TryGetUid(p));
        }
    }

    private void CreatePlayerTower(int actorNumber, int index, string ownerUid)
    {
        var pos = GetPlayerTowerPosition(index);

        GameObject towerRootGO;
        JengaTower tower;

        if (buildMode == BuildMode.Prefab)
        {
            // 완성된 타워 프리팹을 그대로 소환
            towerRootGO = Instantiate(towerPrefab, pos, Quaternion.identity, towersParent);
            towerRootGO.name = $"JengaTower_Player{actorNumber}";
            tower = towerRootGO.GetComponent<JengaTower>() ?? towerRootGO.AddComponent<JengaTower>();

            tower.InitializeOwner(actorNumber, ownerUid);
            tower.InitializeFromExistingHierarchy();
        }
        else
        {
            // 기존 프로시저럴 방식 유지
            towerRootGO = new GameObject($"JengaTower_Player{actorNumber}");
            towerRootGO.transform.SetParent(towersParent, false);
            towerRootGO.transform.position = pos;

            tower = towerRootGO.AddComponent<JengaTower>();
            tower.InitializeOwner(actorNumber, ownerUid);
            tower.Initialize(blockPrefab, towerHeight);
        }

        tower.OnTowerCollapsed += () =>
        {
            if (PhotonNetwork.IsMasterClient)
            {
                JengaNetworkManager.Instance.NotifyTowerCollapsed(actorNumber);
            }
        };

        _playerTowers[actorNumber] = tower;
    }

    // 슬롯 맵(참가자 고정) 저장/조회: 관전자 모드 대비 필수
    // 키는 프로젝트 맥락에 맞게 바꿔도 됨
    private const string ROOMKEY_SLOTS = "JG_SLOTS";

    private void EnsureSlotMap()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        if (!room.CustomProperties.ContainsKey(ROOMKEY_SLOTS))
        {
            var ordered = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).Select(p => p.ActorNumber).ToArray();
            var h = new ExitGames.Client.Photon.Hashtable { [ROOMKEY_SLOTS] = ordered };
            room.SetCustomProperties(h);
        }
    }

    private int[] GetSlotMap()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room != null && room.CustomProperties.TryGetValue(ROOMKEY_SLOTS, out var obj) && obj is int[] arr)
            return arr;

        // 룸이 없거나(오프라인) 저장이 안 되어 있으면 현재 리스트로 대체
        return PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).Select(p => p.ActorNumber).ToArray();
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

    private Vector3 GetPlayerTowerPosition(int index)
    {
        if (fixedPositions is { Length: > 0 })
            return fixedPositions[index % fixedPositions.Length];
        return Vector3.zero;
    }

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
}