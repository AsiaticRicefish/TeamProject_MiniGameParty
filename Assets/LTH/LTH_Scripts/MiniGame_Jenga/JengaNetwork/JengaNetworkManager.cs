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

        thisPhotonView = GetComponent<PhotonView>();
        if (thisPhotonView == null)
        {
            Debug.LogError("[JengaNetwork] PhotonView MISSING on JengaNetworkManager!");
        }
    }

    /// <summary>
    /// �Ŵ��� �ʱ�ȭ ������
    /// </summary>
    public void Initialize()
    {
        Debug.Log($"[JengaNetworkManager.Initialize] ViewID: {thisPhotonView?.ViewID}, InRoom: {PhotonNetwork.InRoom}");

        // PhotonView ���� ��ȭ
        if (thisPhotonView == null)
        {
            Debug.LogError("[JengaNetworkManager] PhotonView is NULL - RPC will fail!");
            return;
        }

        // ViewID�� 0�̸� ������ �̸� ������ ViewID ����
        if (thisPhotonView.ViewID == 0)
        {
            Debug.LogError("[JengaNetworkManager] ViewID is 0! Set Scene ViewID in Inspector or use PhotonNetwork.AllocateViewID before Initialize");

            // ���� �Ҵ� ��õ�
            if (PhotonNetwork.InRoom && !PhotonNetwork.AllocateViewID(thisPhotonView))
            {
                Debug.LogError("[JengaNetworkManager] AllocateViewID failed - RPC communication will not work");
            }
            else
            {
                Debug.Log($"[JengaNetworkManager] ViewID allocated: {thisPhotonView.ViewID}");
            }
        }
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
        if (PhotonNetwork.IsMasterClient)
        {
            // �������� ��� �ٷ� ���� ó��
            ApplyBlockRemoval_OnMaster(actorNumber, blockId, clientSuggestedScore, clientAccuracy);
        }
        else
        {
            // �񸶽��ʹ� �����Ϳ��� ��û
            thisPhotonView.RPC(nameof(RPC_RequestBlockRemoval), RpcTarget.MasterClient,
                               actorNumber, blockId, clientSuggestedScore, clientAccuracy);
        }
    }

    [PunRPC]
    private void RPC_RequestBlockRemoval(int actorNumber, int blockId, int clientSuggestedScore, float clientAccuracy)
    {
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[NET] Reject: not master"); return; }
        ApplyBlockRemoval_OnMaster(actorNumber, blockId, clientSuggestedScore, clientAccuracy);
    }

    private void ApplyBlockRemoval_OnMaster(int actorNumber, int blockId, int clientSuggestedScore, float clientAccuracy)
    {
        Debug.Log($"[NET] REQ_Remove recv on Master actor={actorNumber} blockId={blockId} acc={clientAccuracy:0.00}");

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

        var tm = JengaTowerManager.Instance;
        if (tm == null) { Debug.LogError("[NET] Reject: TowerManager null"); ReplyDeny(actorNumber, blockId, "towerMgr-null"); return; }

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

        int bonus = Mathf.Clamp(Mathf.RoundToInt(clientAccuracy * MAX_BONUS), 0, MAX_BONUS);
        int finalScore = BASE_SCORE + bonus;

        Debug.Log($"[NET] OK �� Broadcast Apply owner={actorNumber} blockId={blockId} score={finalScore}");
        thisPhotonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, true, finalScore, true);
    }

    [PunRPC]
    private void RPC_ApplyBlockRemoval(int ownerActorNumber, int blockId, bool withAnimation, int score, bool isSuccess = true)
    {
        Debug.Log($"[NET] APPLY_Remove recv owner={ownerActorNumber} blockId={blockId} withAnim={withAnimation} succ={isSuccess}");
        
        // ���� ���� �ݿ�
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);

        if (tower == null)
        {
            Debug.LogError($"[NET] tower NULL for actor={ownerActorNumber}");
            return;
        }

        tower.ApplyBlockRemoval(blockId, withAnimation, isSuccess);

        // ���� �ݿ��� �����͸� ����
        if (PhotonNetwork.IsMasterClient)
        {
            // ActorNumber �� UID ��ȯ�� �ʿ��ϴٸ� PlayerList�� CustomProperties["uid"] ����
            string uid = TryGetUidFromActor(ownerActorNumber);
            if (!string.IsNullOrEmpty(uid))
            {
                JengaGameManager.Instance?.ApplyPlayerActionResult(uid, success: true, scoreGained: score);
            }
        }
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
        Debug.Log($"[JengaNetwork] RequestTowerCollapse_MasterAuth called. actor={ownerActorNumber}, IsMaster={PhotonNetwork.IsMasterClient}");

        if (PhotonNetwork.IsMasterClient)
        {
            // �����͸� �ٷ� ó��
            ProcessTowerCollapseRequest(ownerActorNumber);
        }
        else
        {
            // �񸶽��͸� �����Ϳ��� RPC ��û
            if (thisPhotonView == null)
            {
                Debug.LogError("[JengaNetwork] PhotonView is NULL in RequestTowerCollapse_MasterAuth");
                return;
            }

            Debug.Log($"[JengaNetwork] Sending collapse request to master for actor {ownerActorNumber}");
            thisPhotonView.RPC(nameof(RPC_RequestTowerCollapse_Master), RpcTarget.MasterClient, ownerActorNumber);
        }
    }

    // <summary>
    /// �����Ϳ��� Ÿ�� �ر� ��û ó��
    /// </summary>
    private void ProcessTowerCollapseRequest(int ownerActorNumber)
    {
        Debug.Log($"[JengaNetwork] Processing tower collapse request for actor {ownerActorNumber}");

        // TowerManager/Ÿ�� ���� ����
        var tm = JengaTowerManager.Instance;
        if (tm == null)
        {
            Debug.LogError("[JengaNetwork] JengaTowerManager.Instance is NULL");
            return;
        }

        var tower = tm.GetPlayerTower(ownerActorNumber);
        if (tower == null)
        {
            var uid = tm.GetOwnerUidByActor(ownerActorNumber);
            Debug.LogError($"[JengaNetwork] Tower not found for actor={ownerActorNumber}, uid={uid ?? "(null)"}");
            return;
        }

        Debug.Log($"[JengaNetwork] Broadcasting tower collapse for actor={ownerActorNumber}");

        // ��� Ŭ���̾�Ʈ���� �ر� ���� ��ε�ĳ��Ʈ
        thisPhotonView.RPC(nameof(RPC_ApplyTowerCollapse_All), RpcTarget.All, ownerActorNumber);
    }

    [PunRPC]
    private void RPC_RequestTowerCollapse_Master(int ownerActorNumber, PhotonMessageInfo info)
    {
        Debug.Log($"[JengaNetwork] RPC_RequestTowerCollapse_Master received from {info.Sender.ActorNumber} for actor {ownerActorNumber}");

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[JengaNetwork] Received collapse request but not master");
            return;
        }

        ProcessTowerCollapseRequest(ownerActorNumber);
    }

    // RPC_ApplyTowerCollapse_All�� ������ �����ϵ� �α� �߰�
    [PunRPC]
    private void RPC_ApplyTowerCollapse_All(int ownerActorNumber)
    {
        Debug.Log($"[JengaNetwork] RPC_ApplyTowerCollapse_All received for actor={ownerActorNumber}");

        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);

        if (tower == null)
        {
            Debug.LogError($"[JengaNetwork] tower NULL for actor={ownerActorNumber} in RPC_ApplyTowerCollapse_All");
            return;
        }

        Debug.Log($"[JengaNetwork] Triggering collapse animation for actor={ownerActorNumber}");

        // �ر� �ִϸ��̼� ����
        JengaTowerManager.Instance.WithSuppressedCollapse(() =>
        {
            tower.TriggerCollapseOnce();
        });

        // ���� ���� ó�� (�����͸�)
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log($"[JengaNetwork] Notifying GameManager of collapse for actor={ownerActorNumber}");
            JengaGameManager.Instance?.OnTowerCollapsed(ownerActorNumber);
        }
    }

    #endregion

    #region Ÿ�̸� ����ȭ: ������ �� ��ü Ŭ���̾�Ʈ

    /// <summary>
    /// �����Ϳ��� ��� Ŭ���̾�Ʈ���� ���� ���� �ð��� ����ȭ
    /// </summary>
    public void BroadcastTimeSync(float remainingTime)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        thisPhotonView.RPC(nameof(RPC_SyncTime), RpcTarget.All, remainingTime);
    }

    [PunRPC]
    private void RPC_SyncTime(float syncedTime)
    {
        JengaGameManager.Instance?.SyncRemainingTime(syncedTime);
    }

    #endregion

    #region ī��Ʈ�ٿ� ����ȭ: ������ �� ��ü Ŭ���̾�Ʈ

    /// <summary>
    /// �����Ϳ��� ��� Ŭ���̾�Ʈ���� ī��Ʈ�ٿ� ���� ��ȣ �۽�
    /// </summary>
    public void BroadcastStartCountdown(float countdownDuration)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"[JengaNetwork] Broadcasting countdown start: {countdownDuration}s");
        thisPhotonView.RPC(nameof(RPC_StartCountdown), RpcTarget.All, countdownDuration);
    }

    /// <summary>
    /// [RPC] ��� Ŭ���̾�Ʈ���� ���ÿ� ī��Ʈ�ٿ� ����
    /// </summary>
    [PunRPC]
    private void RPC_StartCountdown(float duration)
    {
        Debug.Log($"[JengaNetwork] Received countdown start RPC: {duration}s");

        // UI �Ŵ������� ī��Ʈ�ٿ� ���� �˸�
        JengaUIManager.Instance?.StartCountdown(duration);
    }

    /// <summary>
    /// �����Ϳ��� ī��Ʈ�ٿ� �Ϸ� �� ���� ���� ��ȣ �۽�
    /// </summary>
    public void BroadcastCountdownComplete()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        thisPhotonView.RPC(nameof(RPC_CountdownComplete), RpcTarget.All);
    }

    /// <summary>
    /// [RPC] ī��Ʈ�ٿ� �Ϸ� ó��
    /// </summary>
    [PunRPC]
    private void RPC_CountdownComplete()
    {
        Debug.Log("[JengaNetwork] Received countdown complete RPC");
        // UI���� ī��Ʈ�ٿ� �����
        JengaUIManager.Instance?.HideCountdown();
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