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
    #endregion

    [Header("��ġ(�Ʒ��� ��Ŀ)")]
    [Tooltip("���� �Ʒ����� TowerAnchor���� ��� (Arena_i/TowerAnchor)")]
    [SerializeField] private List<Transform> arenaTowerAnchors = new(); // �ν����Ϳ� �巡�� ���

    [Tooltip("Instantiate �� Ÿ���� ��Ŀ ������ ������ ����")]
    [SerializeField] private bool parentTowerUnderAnchor = true;

    [SerializeField] private string[] arenaLayerNames = { "Arena_0", "Arena_1", "Arena_2", "Arena_3" };

    [Tooltip("�Ʒ��� Ʈ�� ��ü�� ���δ� ��Ʈ. ��� ������ �ڵ� ����")]
    [SerializeField] private Transform towersParent;

    private readonly Dictionary<int, JengaTower> _playerTowers = new(); // ActorNumber �� Tower

    // �� Ŀ���� ������Ƽ Ű (������/������ ��� ���� ����)
    private const string ROOMKEY_SLOTS = "JG_SLOTS";

    // ��Ʈ��ũ ���� �� �̺�Ʈ ���ε�ĳ��Ʈ ���� �÷���
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

        // �θ� Ʈ�������� ��� ������ �����ϰ� �ϳ� ����� �д�
        if (towersParent == null)
        {
            var go = new GameObject("JengaTowersRoot");
            towersParent = go.transform;
        }
    }

    public void Initialize()
    {
        // �����Ϳ����� ���Ը� ����(��������/�񸶽��ʹ� �б⸸)
        EnsureSlotMap();
        CreateAllPlayerTowers();
        Debug.Log("[JengaTowerManager] �ʱ�ȭ �Ϸ� - ���� Ÿ�� ����");
    }

    #region Tower Creation
    private void CreateAllPlayerTowers()
    {
        _playerTowers.Clear();

        // ���� ��: ���� ������ Ȯ���� ���� (������ ���� �̽� ����)
        var slotActors = GetSlotMap(); // int[] actorNumbers

        for (int i = 0; i < slotActors.Length; i++)
        {
            var actorNumber = slotActors[i];
            var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);
            if (p == null) continue; // ���� �÷��̾� ��

            CreatePlayerTower(actorNumber, i, TryGetUid(p));
        }
    }

    private void CreatePlayerTower(int actorNumber, int slotIndex, string ownerUid)
    {
        // ��Ŀ ���� ��ġ/ȸ��
        var anchor = GetPlayerTowerAnchor(slotIndex);
        var pos = anchor ? anchor.position : Vector3.zero;
        var rot = anchor ? anchor.rotation : Quaternion.identity;
        var parent = parentTowerUnderAnchor && anchor ? anchor : towersParent;

        GameObject towerRootGO;
        JengaTower tower;

        if (buildMode == BuildMode.Prefab)
        {
            // �ϼ��� Ÿ�� �������� �״�� ��ȯ
            towerRootGO = Instantiate(towerPrefab, pos, rot, parent);
            towerRootGO.name = $"JengaTower_Player{actorNumber}";
            tower = towerRootGO.GetComponent<JengaTower>() ?? towerRootGO.AddComponent<JengaTower>();

            tower.InitializeOwner(actorNumber, ownerUid);
            tower.InitializeFromExistingHierarchy();

            ApplyArenaLayer(towerRootGO, slotIndex);
        }
        else
        {
            // ���� ���ν����� ��� ����
            towerRootGO = new GameObject($"JengaTower_Player{actorNumber}");
            towerRootGO.transform.SetParent(towersParent, false);
            towerRootGO.transform.SetPositionAndRotation(pos, rot);

            tower = towerRootGO.AddComponent<JengaTower>();
            tower.InitializeOwner(actorNumber, ownerUid);
            tower.Initialize(blockPrefab, towerHeight);

            ApplyArenaLayer(towerRootGO, slotIndex);
        }

        // �ر� �̺�Ʈ �� ��Ʈ��ũ ����(�����͸�)
        tower.OnTowerCollapsed += () =>
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (IsSuppressingCollapse) return;

        JengaNetworkManager.Instance.RequestTowerCollapse_MasterAuth(actorNumber);
    };

        _playerTowers[actorNumber] = tower;
        tower.ConfigureTopProtection(allowTopRemoval: false, topSafeLayers: 1);

    }
    #endregion

    #region Slot Map (Room Properties)
    private void EnsureSlotMap()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        if (!room.CustomProperties.ContainsKey(ROOMKEY_SLOTS))
        {
            var ordered = PhotonNetwork.PlayerList
                 .OrderBy(p => p.ActorNumber)
                 .Select(p => p.ActorNumber)
                 .ToArray();

            var h = new ExitGames.Client.Photon.Hashtable { [ROOMKEY_SLOTS] = ordered };
            room.SetCustomProperties(h);
        }
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
    /// ActorNumber �� ���� �ε���
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