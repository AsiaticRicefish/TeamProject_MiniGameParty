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

    // ���� ��� ���
    private const int BASE_SCORE = 10;
    private const int MAX_BONUS = 10;

    /// <summary>
    /// ���� �������� ����ִ� �Ͻ��� �̱���
    /// </summary>
    protected override void OnAwake()
    {
        base.isPersistent = false;

        base.OnAwake();

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
//#if PHOTON_UNITY_NETWORKING_2_OR_NEWER
//          if (PhotonNetwork.InRoom && thisPhotonView.ViewID == 0)
//        {
//            if (!PhotonNetwork.AllocateViewID(thisPhotonView))
//                Debug.LogError("[NM] AllocateViewID failed. (���� �̸� ��ġ + Scene ViewID ����)");
//        }
//#endif
    }


    #region �÷��̾� ��� ���� �� ������

    /// <summary>
    /// Ŭ���̾�Ʈ�� �ڽ��� �ൿ ���(����/����, ����)�� �����Ϳ��� ����
    /// </summary>
    public void SendPlayerActionResult(string uid, bool success, int score)
    {
        thisPhotonView.RPC(nameof(ReceivePlayerActionResult), RpcTarget.MasterClient, uid, success, score);
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
        Debug.Log($"[JengaNetwork] BroadcastGameState called: {state}");

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("[JengaNetwork] Not in room!");
            return;
        }
        if (thisPhotonView == null)
        {
            Debug.LogError("[JengaNetwork] PhotonView is null!");
            return;
        }
        if (thisPhotonView.ViewID == 0)
        {
            Debug.LogError("[JengaNetwork] PhotonView ViewID is 0!");
            return;
        }
        Debug.Log($"[JengaNetwork] Sending RPC to change state to: {state}");

        thisPhotonView.RPC(nameof(RPC_ApplyGameState), RpcTarget.All, (int)state);
    }

    [PunRPC]
    private void RPC_ApplyGameState(int stateInt)
    {
        var state = (JengaGameState)stateInt;
        Debug.Log($"[NM] ApplyGameState �� {state}");
        JengaGameManager.Instance?.ApplyGameStateChange(state);
    }

    #endregion

    #region  Ÿ�̹� �̴ϰ��� ���: Ŭ���̾�Ʈ �� ������

    /// <summary>
    /// Ŭ���̾�Ʈ�� Ÿ�̹� ������ ���(��Ȯ�� ����)�� �����Ϳ��� ����
    /// </summary>
    public void SendTimingResult(string uid, bool success, float accuracy)
    {
        thisPhotonView.RPC(nameof(ReceiveTimingResult), RpcTarget.MasterClient, uid, success, accuracy);
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
        thisPhotonView.RPC(nameof(RPC_RequestBlockRemoval), RpcTarget.MasterClient, actorNumber, blockId, clientSuggestedScore, clientAccuracy);
    }

    // === �����Ϳ��� ����/���� ===
    [PunRPC]
    private void RPC_RequestBlockRemoval(int actorNumber, int blockId, int clientSuggestedScore, float clientAccuracy)
    {
        Debug.Log($"[NET] REQ_Remove recv on Master actor={actorNumber} blockId ={blockId} acc={clientAccuracy:0.00}");

        // A) ������/����
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[NET] Reject: not master"); return; }

        var gm = JengaGameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[NET] GM NULL �� ���� �ݿ��� ��ŵ�ϰ� ����/������ ����");
        }
        else if (gm.currentState != JengaGameState.Playing)
        {
            Debug.LogWarning($"[NET] Reject: state={gm.currentState}");
            ReplyDeny(actorNumber, blockId, "state-not-playing");
            return;
        }


        // --- Ÿ��/��� �ؼ� ---
        var tm = JengaTowerManager.Instance;
        if (tm == null) { Debug.LogError("[NET] Reject: TowerManager null"); ReplyDeny(actorNumber, blockId, "towerMgr-null"); return; }

        // Ÿ��/��� ��ȸ
        var tower = tm.GetPlayerTower(actorNumber);
        if (tower == null)
        {
            Debug.LogWarning($"[NET] Reject: tower null for actor={actorNumber}");
            ReplyDeny(actorNumber, blockId, "tower-null");
            return;
        }


        var block = tower?.GetBlockById(blockId);
        if (block == null)
        {
            Debug.LogWarning($"[NET] Reject: block null (actor={actorNumber}, blockId={blockId})");
            ReplyDeny(actorNumber, blockId, "block-null");
            return;
        }
        if (block.OwnerActorNumber != actorNumber)
            Debug.LogWarning($"[NET] Owner mismatch: reqActor={actorNumber} blockOwner={block.OwnerActorNumber} id={blockId}");

        if (block.IsRemoved)
        {
            Debug.LogWarning($"[NET] Reject: already removed (actor={actorNumber}, blockId={blockId})");
            ReplyDeny(actorNumber, blockId, "already-removed");
            return;
        }


        // C) ���� ���ɼ� ���� (ĳ�� vs ��Ģ)
        bool removableCache = tower.GetRemovableBlocks().Contains(block);
        bool removableRule = tower.CanRemoveBlock(block);

        if (!removableCache || !removableRule)
        {
            Debug.LogWarning($"[NET] Not removable (cache={removableCache}, rule={removableRule}) remain={tower.GetRemainingBlocks()}");
            if (!removableRule)
            {
                ReplyDeny(actorNumber, blockId, "not-removable");
                return;
            }
            else
            {
                Debug.Log("[NET] Proceeding by RULE (cache stale suspected)");
            }
        }

        // D) ���� ���(���� ����)
        int bonus = Mathf.Clamp(Mathf.RoundToInt(clientAccuracy * MAX_BONUS), 0, MAX_BONUS);
        int finalScore = BASE_SCORE + bonus;

        Debug.Log($"[NET] OK �� Broadcast Apply owner={actorNumber} blockId={blockId} score={finalScore}");
        thisPhotonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, true, finalScore, true);
    }

    [PunRPC]
    private void RPC_ApplyBlockRemoval(int ownerActorNumber, int blockId, bool withAnimation, int score, bool isSuccess = true)
    {
        Debug.Log($"[NET] APPLY_Remove recv owner={ownerActorNumber} blockId={blockId} withAnim={withAnimation} succ={isSuccess}");
        // 1) ���� ���� �ݿ�
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);
        var block = tower?.GetBlockById(blockId);

        if (block != null && withAnimation)
        {
            block.RemoveWithAnimation(isSuccess); // isSuccess �Ķ���� ����
        }
        else
        {
            tower?.ApplyBlockRemoval(blockId, withAnimation, isSuccess);
        }

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
    /// ���� �� ��û�ڿ��Ը� ���� �� Ŭ�󿡼� pending ��� ����/�佺Ʈ ǥ�� � ���
    /// </summary>
    private void ReplyDeny(int actorNumber, int blockId, string reason)
    {
        var target = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (target == null) return;
        thisPhotonView.RPC(nameof(RPC_BlockRemovalDenied), target, blockId, reason);
    }

    [PunRPC]
    private void RPC_BlockRemovalDenied(int blockId, string reason)
    {
        var myTower = JengaTowerManager.Instance?.GetPlayerTower(PhotonNetwork.LocalPlayer.ActorNumber);
        var block = myTower?.GetBlockById(blockId);
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
        thisPhotonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, withAnimation, 0);
    }

    #endregion

    #region Ÿ�� �ر� �˸�

    // Ŭ���̾�Ʈ �� ������: "�� ����� Ÿ���� �ر����� �ּ���"
    public void RequestTowerCollapse_MasterAuth(int ownerActorNumber)
    {
        thisPhotonView.RPC(nameof(RPC_RequestTowerCollapse_Master), RpcTarget.MasterClient, ownerActorNumber);
    }

    [PunRPC]
    private void RPC_RequestTowerCollapse_Master(int ownerActorNumber, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        thisPhotonView.RPC(nameof(RPC_ApplyTowerCollapse_All), RpcTarget.All, ownerActorNumber);
    }

    // ��ü ����: ��� Ŭ�󿡼� ���� �ڵ�� ���� �ر� ����
    [PunRPC]
    private void RPC_ApplyTowerCollapse_All(int ownerActorNumber)
    {
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);
        
        if (tower == null) return;

        JengaTowerManager.Instance.WithSuppressedCollapse(() =>
        {
            tower.TriggerCollapseOnce();
        });

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
        thisPhotonView.RPC(nameof(RPC_RequestSnapshot), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
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

        thisPhotonView.RPC(nameof(RPC_ReceiveSnapshot), target, table);
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