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

    [Header("Ÿ�� ���")]
    [SerializeField] private BuildMode buildMode = BuildMode.Procedural;

    [Header("������ ���")]
    [SerializeField] private GameObject towerPrefab; // �� �ϼ��� Ÿ�� ������(�ڽĵ鿡 JengaBlock �پ� ����)

    #region Ÿ�� ���� ���� (��� ����)
    [Header("Ÿ�� ����")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private int towerHeight = 13;

    [Header("��ġ")]
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

    private readonly Dictionary<int, JengaTower> _playerTowers = new(); // ActorNumber �� Tower

    protected override void OnAwake()
    {
        base.isPersistent = false;

        // �θ� Ʈ�������� ��� ������ �����ϰ� �ϳ� ����� �д�
        if (towersParent == null)
        {
            var go = new GameObject("JengaTowersRoot");
            towersParent = go.transform;
        }
    }

    public void Initialize()
    {
        CreateAllPlayerTowers();
        Debug.Log("[JengaTowerManager] �ʱ�ȭ �Ϸ� - ���� Ÿ�� ����");
    }

    private void CreateAllPlayerTowers()
    {
        _playerTowers.Clear();

        // ���� ��: ���� ������ Ȯ���� ����(������ ���� �̽� ����)
        var slotActors = GetSlotMap(); // int[] actorNumbers

        for (int i = 0; i < slotActors.Length; i++)
        {
            var actorNumber = slotActors[i];
            var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);
            if (p == null) continue; // ���� �÷��̾� ��
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
            // �ϼ��� Ÿ�� �������� �״�� ��ȯ
            towerRootGO = Instantiate(towerPrefab, pos, Quaternion.identity, towersParent);
            towerRootGO.name = $"JengaTower_Player{actorNumber}";
            tower = towerRootGO.GetComponent<JengaTower>() ?? towerRootGO.AddComponent<JengaTower>();

            tower.InitializeOwner(actorNumber, ownerUid);
            tower.InitializeFromExistingHierarchy();
        }
        else
        {
            // ���� ���ν����� ��� ����
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

    // ���� ��(������ ����) ����/��ȸ: ������ ��� ��� �ʼ�
    // Ű�� ������Ʈ �ƶ��� �°� �ٲ㵵 ��
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

        // ���� ���ų�(��������) ������ �� �Ǿ� ������ ���� ����Ʈ�� ��ü
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