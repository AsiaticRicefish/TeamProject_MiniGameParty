using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// ���� ������ ��Ʈ��ũ ����ȭ�� ����ϴ� ���� �Ŵ���
/// - ��� ���� �ִϸ��̼�
/// - Ÿ�̹� ���� ���
/// - ���� ����
/// ���� ��Ʈ��ũ ����� Photon RPC�� ���� ó��
/// <summary>

[RequireComponent(typeof(PhotonView))]
public class JengaNetworkManager : PunSingleton<JengaNetworkManager>, IGameComponent
{
    private PhotonView thisPhotonView;

    // ������ ������ ��� ���� �� �ڷ�ƾ
    private Coroutine _pendingApplyCo;

    /// <summary>
    /// ���� �������� ����ִ� �Ͻ��� �̱���
    /// </summary>
    protected override void OnAwake()
    {
        base.isPersistent = false;

        thisPhotonView = GetComponent<PhotonView>();
        
        // �ߺ� PhotonView�� �پ� ������ ����
        var pvs = GetComponents<PhotonView>();
        if (pvs.Length > 1)
        {
            for (int i = 1; i < pvs.Length; i++) Destroy(pvs[i]);
        }
    }

    /// <summary>
    /// �Ŵ��� �ʱ�ȭ ������
    /// </summary>
    public void Initialize()
    {
#if PHOTON_UNITY_NETWORKING_2_OR_NEWER
          if (PhotonNetwork.InRoom && thisPhotonView.ViewID == 0)
        {
            if (!PhotonNetwork.AllocateViewID(thisPhotonView))
                Debug.LogError("[NM] AllocateViewID failed. (���� �̸� ��ġ + Scene ViewID ����)");
        }
#endif
    }


    #region �÷��̾� ��� ���� �� ������

    /// <summary>
    /// Ŭ���̾�Ʈ�� �ڽ��� �ൿ ���(����/����, ����)�� �����Ϳ��� ����
    /// </summary>
    public void SendPlayerActionResult(string uid, bool success, int score)
    {
        photonView.RPC(nameof(ReceivePlayerActionResult), RpcTarget.MasterClient, uid, success, score);
    }

    /// <summary>
    /// [RPC] ������ Ŭ���̾�Ʈ���� ���ŵ� ����� GameManager�� �ݿ�
    /// </summary>
    [PunRPC]
    private void ReceivePlayerActionResult(string uid, bool success, int score)
    {
        // ������ Ŭ���̾�Ʈ������ ����
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"[JengaNetwork - ReceivePlayerActionResult] ��� ����: {uid} | ���� ����: {success} | ����: {score}");
        JengaGameManager.Instance.ApplyPlayerActionResult(uid, success, score);
    }

    #endregion

    #region ���� ���� ����ȭ: ������ �� ��ü Ŭ���̾�Ʈ

    /// <summary>
    /// ���� ���� ������ ��ü Ŭ���̾�Ʈ�� �۽�
    /// </summary>
    public void BroadcastGameState(JengaGameState state)
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }
        if (thisPhotonView == null)
        {
            return;
        }

        thisPhotonView.RPC(nameof(RPC_ApplyGameState), RpcTarget.All, (int)state);
    }

    [PunRPC]
    private void RPC_ApplyGameState(int stateInt)
    {
        var state = (JengaGameState)stateInt;
        Debug.Log($"[JengaNetwork - RPC_ApplyGameState] {state}");
        JengaGameManager.Instance?.ApplyGameStateChange(state);
    }

    #endregion

    #region  Ÿ�̹� �̴ϰ��� ���: Ŭ���̾�Ʈ �� ������

    /// <summary>
    /// Ŭ���̾�Ʈ�� Ÿ�̹� ������ ���(��Ȯ�� ����)�� �����Ϳ��� ����
    /// </summary>
    public void SendTimingResult(string uid, bool success, float accuracy)
    {
        photonView.RPC(nameof(ReceiveTimingResult), RpcTarget.MasterClient, uid, success, accuracy);
    }

    /// <summary>
    /// [RPC] �����Ͱ� ��Ȯ�� ��� ������ ����� GameManager�� �ݿ�
    /// </summary>
    [PunRPC]
    private void ReceiveTimingResult(string uid, bool success, float accuracy)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int bonusScore = success ? Mathf.RoundToInt(accuracy * 5) : 0;
        JengaGameManager.Instance.ApplyPlayerActionResult(uid, success, bonusScore);
    }

    #endregion

    #region ��� ���� �ִϸ��̼� ����ȭ: ������ �� ��ü Ŭ���̾�Ʈ

    // === ���� �� ������: ��� ���� ��û ===
    // ��û�ڴ� ActorNumber�� �ĺ�. UID ������ �ʿ��ϸ� ���ο��� ��ȯ.
    public void RequestBlockRemoval_MasterAuth(int actorNumber, int blockId, int clientSuggestedScore, float clientAccuracy)
    {
        photonView.RPC(nameof(RPC_RequestBlockRemoval), RpcTarget.MasterClient, actorNumber, blockId, clientSuggestedScore, clientAccuracy);
    }

    // === �����Ϳ��� ����/���� ===
    [PunRPC]
    private void RPC_RequestBlockRemoval(int actorNumber, int blockId, int clientSuggestedScore, float clientAccuracy)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 0) ���� üũ
        if (JengaGameManager.Instance == null || JengaGameManager.Instance.currentState != JengaGameState.Playing)
            return;

        // 1) Ÿ��/��� ��ȸ
        var tower = JengaTowerManager.Instance?.GetPlayerTower(actorNumber);
        var block = tower?.GetBlockById(blockId);

        bool valid =
            tower != null &&
            block != null &&
            !block.IsRemoved &&
            tower.GetRemovableBlocks().Contains(block);

        if (!valid) return;


        // 2) ���� ����� ������ ���� ����
        int baseScore = 10;
        int bonus = Mathf.Clamp(Mathf.RoundToInt(clientAccuracy * 10f), 0, 10);
        int finalScore = baseScore + bonus;

        // 3) ��ü ���� ��ε�ĳ��Ʈ (������ + ���ID �ʼ�)
        photonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, /*withAnimation*/ true, finalScore);
    }

    [PunRPC]
    private void RPC_ApplyBlockRemoval(int ownerActorNumber, int blockId, bool withAnimation, int score)
    {
        // 1) ���� ���� �ݿ�
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);
        tower?.ApplyBlockRemoval(blockId, withAnimation);

        // 2) ���� �ݿ��� �����͸� ����
        if (PhotonNetwork.IsMasterClient)
        {
            // ActorNumber �� UID ��ȯ�� �ʿ��ϴٸ� PlayerList�� CustomProperties["uid"] ����
            string uid = TryGetUidFromActor(ownerActorNumber);
            if (!string.IsNullOrEmpty(uid))
            {
                JengaGameManager.Instance?.ApplyPlayerActionResult(uid, success: true, scoreGained: score);
            }
        }

        // 3) ���� ����/����/UI
        // JengaUIManager
        // JengaSoundManager
    }

    /// <summary>
    /// ActorNumber �� UID ����
    /// </summary>
    private string TryGetUidFromActor(int actorNumber)
    {
        var p = Array.Find(PhotonNetwork.PlayerList, x => x.ActorNumber == actorNumber);
        if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("uid", out var uidObj))
            return uidObj as string;
        return null;
    }

    /// <summary>
    /// (��ƿ) �����Ͱ� Ư�� ���� ���¸� ���� ����ȭ
    /// <summary>
    public void SyncBlockRemovalForOwner(int actorNumber, int blockId, bool withAnimation)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, withAnimation, 0);
    }

    #endregion

    #region Ÿ�� �ر� �˸�

    /// <summary>
    /// ������ �� ��ü: Ÿ�� �ر� �˸�
    /// </summary>
    public void NotifyTowerCollapsed(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC(nameof(RPC_NotifyTowerCollapsed), RpcTarget.All, actorNumber);
    }

    /// <summary>
    /// [RPC] �ر� ����
    /// </summary>
    [PunRPC]
    private void RPC_NotifyTowerCollapsed(int ownerActorNumber)
    {
        Debug.Log($"[JengaNetwork - RPC_NotifyTowerCollapsed] �ر��� Ÿ�� ������ = {ownerActorNumber}");
        JengaGameManager.Instance?.OnTowerCollapsed(ownerActorNumber);
    }
    #endregion

    #region ������(����Ʈ ����) ����ȭ: ������
    /// <summary>
    /// �� �÷��̾� ���� ��(�����Ϳ�����) ���� ������ ����
    /// </summary>
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        SendSnapshotTo(newPlayer.ActorNumber);
    }

    /// <summary>
    /// (�ɼ�) ���� ���� ������ ��������� ������ ��û�ϰ� ���� �� ȣ��
    /// </summary>
    public void RequestSnapshotFromMaster()
    {
        photonView.RPC(nameof(RPC_RequestSnapshot), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    /// <summary>
    /// [RPC] ������ ��û ����(������)
    /// </summary>
    [PunRPC]
    private void RPC_RequestSnapshot(int requesterActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SendSnapshotTo(requesterActorNumber);
    }

    /// <summary>
    /// ������ �� Ư�� �÷��̾�: ������ ����(������� ���ŵ� ��� ���)
    /// </summary>
    public void SendSnapshotTo(int targetActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PhotonHashtable table = BuildSnapshotHashtable();
        var target = PhotonNetwork.CurrentRoom?.GetPlayer(targetActorNumber);
        if (target == null)
        {
            Debug.LogWarning($"[JengaNetwork - SendSnapshotTo] ����: ��� ���� actor = {targetActorNumber}");
            return;
        }

        photonView.RPC(nameof(RPC_ReceiveSnapshot), target, table);
    }

    /// <summary>
    /// [RPC] ������ ����(������/����Ʈ ����)
    /// </summary>
    [PunRPC]
    private void RPC_ReceiveSnapshot(PhotonHashtable table)
    {
        // �Ŵ��� �Ǵ� Ÿ���� ���� �غ� ���̸� �غ�� ������ ��� ����
        if (_pendingApplyCo != null) StopCoroutine(_pendingApplyCo);
        _pendingApplyCo = StartCoroutine(CoApplySnapshotWhenReady(table));
    }

    /// <summary>
    /// (������) ���� ���� ���¸� Photon Hashtable�� ����ȭ
    ///  key: actorNumber(int), value: removedBlockIds(int[])
    /// </summary>
    private PhotonHashtable BuildSnapshotHashtable()
    {
        var snap = JengaTowerManager.Instance?.SnapshotRemovedBlocks()
                   ?? new Dictionary<int, IReadOnlyCollection<int>>();

        PhotonHashtable table = new PhotonHashtable();
        foreach (var kv in snap)
        {
            // PUN ����ȭ ȣȯ�� ���� int[] ����
            int[] arr = kv.Value is int[] a ? a : kv.Value.ToArray();
            table[kv.Key] = arr;
        }
        return table;
    }

    /// <summary>
    /// Ÿ�� �Ŵ����� �غ�� ������ ��� �� ������ ����
    /// </summary>
    private IEnumerator CoApplySnapshotWhenReady(PhotonHashtable table)
    {
        // �Ŵ��� ���� & �ּ� �� �� Initialize�� �����ٰ� �����Ǵ� �����ӱ��� ���
        while (JengaTowerManager.Instance == null)
            yield return null;

        ApplySnapshotHashtable(table);
        _pendingApplyCo = null;
    }

    /// <summary>
    /// ������ �������� ���� Ÿ���� ����
    /// </summary>
    private void ApplySnapshotHashtable(PhotonHashtable table)
    {
        var dict = new Dictionary<int, IReadOnlyCollection<int>>(table.Count);

        foreach (DictionaryEntry e in table)
        {
            int actor = (int)e.Key;
            int[] removed = e.Value as int[] ?? Array.Empty<int>();
            dict[actor] = removed;
        }

        JengaTowerManager.Instance?.ApplySnapshot(dict);
        Debug.Log($"[JengaNetwork - ApplySnapshotHashtable] ������ ���� �Ϸ� (actors = {dict.Count})");
    }

    #endregion
}