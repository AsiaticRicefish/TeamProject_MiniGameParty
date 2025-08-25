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
/// 젠가 게임의 네트워크 동기화를 담당하는 전용 매니저
/// - 블록 제거 애니메이션
/// - 타이밍 게임 결과
/// - 게임 상태
/// 등의 네트워크 통신을 Photon RPC를 통해 처리
/// <summary>

[RequireComponent(typeof(PhotonView))]
public class JengaNetworkManager : PunSingleton<JengaNetworkManager>, IGameComponent
{
    private PhotonView thisPhotonView;

    // 관전자 스냅샷 대기 적용 용 코루틴
    private Coroutine _pendingApplyCo;

    // 점수 계산 상수
    private const int BASE_SCORE = 10;
    private const int MAX_BONUS = 10;

    /// <summary>
    /// 젠가 씬에서만 살아있는 일시적 싱글톤
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
    /// 매니저 초기화 진입점
    /// </summary>
    public void Initialize()
    {
        Debug.Log($"[JengaNetworkManager.Initialize] ViewID: {thisPhotonView?.ViewID}, InRoom: {PhotonNetwork.InRoom}");

        // PhotonView 검증 강화
        if (thisPhotonView == null)
        {
            Debug.LogError("[JengaNetworkManager] PhotonView is NULL - RPC will fail!");
            return;
        }

        // ViewID가 0이면 씬에서 미리 설정된 ViewID 권장
        if (thisPhotonView.ViewID == 0)
        {
            Debug.LogError("[JengaNetworkManager] ViewID is 0! Set Scene ViewID in Inspector or use PhotonNetwork.AllocateViewID before Initialize");

            // 동적 할당 재시도
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


    #region 플레이어 결과 보고 → 마스터

    /// <summary>
    /// 클라이언트가 자신의 행동 결과(성공/실패, 점수)를 마스터에게 보고
    /// </summary>
    public void SendPlayerActionResult(string uid, bool success, int score)
    {
        thisPhotonView.RPC(nameof(ReceivePlayerActionResult), RpcTarget.MasterClient, uid, success, score);
    }

    /// <summary>
    /// [RPC] 마스터 클라이언트에서 수신된 결과를 GameManager에 반영
    /// </summary>
    [PunRPC]
    private void ReceivePlayerActionResult(string uid, bool success, int score)
    {
        // 마스터 클라이언트에서만 실행
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"[JengaNetwork - ReceivePlayerActionResult] 결과 수신: {uid} | 성공 여부: {success} | 점수: {score}");
        JengaGameManager.Instance.ApplyPlayerActionResult(uid, success, score);
    }

    #endregion

    #region 게임 상태 동기화: 마스터 → 전체 클라이언트

    /// <summary>
    /// 게임 상태 변경을 전체 클라이언트에 송신
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
        Debug.Log($"[NM] ApplyGameState → {state}");
        JengaGameManager.Instance?.ApplyGameStateChange(state);
    }

    #endregion

    #region  타이밍 미니게임 결과: 클라이언트 → 마스터

    /// <summary>
    /// 클라이언트가 타이밍 게임의 결과(정확도 포함)를 마스터에게 보고
    /// </summary>
    public void SendTimingResult(string uid, bool success, float accuracy)
    {
        thisPhotonView.RPC(nameof(ReceiveTimingResult), RpcTarget.MasterClient, uid, success, accuracy);
    }

    /// <summary>
    /// [RPC] 마스터가 정확도 기반 점수를 계산해 GameManager에 반영
    /// </summary>
    [PunRPC]
    private void ReceiveTimingResult(string uid, bool success, float accuracy)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int bonusScore = success ? Mathf.RoundToInt(accuracy * 5) : 0;
        JengaGameManager.Instance.ApplyPlayerActionResult(uid, success, bonusScore);
    }

    #endregion

    #region 블록 제거 애니메이션 동기화: 마스터 → 전체 클라이언트

    // === 로컬 → 마스터: 블록 제거 요청 ===
    // 요청자는 ActorNumber로 식별. UID 매핑이 필요하면 내부에서 변환.
    public void RequestBlockRemoval_MasterAuth(int actorNumber, int blockId, int clientSuggestedScore, float clientAccuracy)
    {  
        if (PhotonNetwork.IsMasterClient)
        {
            // 마스터일 경우 바로 로컬 처리
            ApplyBlockRemoval_OnMaster(actorNumber, blockId, clientSuggestedScore, clientAccuracy);
        }
        else
        {
            // 비마스터는 마스터에게 요청
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
            Debug.LogWarning("[NET] GM NULL → 점수 반영은 스킵하고 검증/적용은 진행");
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

        Debug.Log($"[NET] OK → Broadcast Apply owner={actorNumber} blockId={blockId} score={finalScore}");
        thisPhotonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, true, finalScore, true);
    }

    [PunRPC]
    private void RPC_ApplyBlockRemoval(int ownerActorNumber, int blockId, bool withAnimation, int score, bool isSuccess = true)
    {
        Debug.Log($"[NET] APPLY_Remove recv owner={ownerActorNumber} blockId={blockId} withAnim={withAnimation} succ={isSuccess}");
        
        // 실제 제거 반영
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);

        if (tower == null)
        {
            Debug.LogError($"[NET] tower NULL for actor={ownerActorNumber}");
            return;
        }

        tower.ApplyBlockRemoval(blockId, withAnimation, isSuccess);

        // 점수 반영은 마스터만 집계
        if (PhotonNetwork.IsMasterClient)
        {
            // ActorNumber → UID 변환이 필요하다면 PlayerList의 CustomProperties["uid"] 참조
            string uid = TryGetUidFromActor(ownerActorNumber);
            if (!string.IsNullOrEmpty(uid))
            {
                JengaGameManager.Instance?.ApplyPlayerActionResult(uid, success: true, scoreGained: score);
            }
        }
    }

    /// <summary>
    /// 거절 시 요청자에게만 통지 → 클라에서 pending 잠금 해제/토스트 표시 등에 사용
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
    /// ActorNumber → UID 매핑
    /// </summary>
    private string TryGetUidFromActor(int actorNumber)
    {
        var p = Array.Find(PhotonNetwork.PlayerList, x => x.ActorNumber == actorNumber);
        if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("uid", out var uidObj))
            return uidObj as string;
        return null;
    }

    /// <summary>
    /// (유틸) 마스터가 특정 제거 상태를 강제 동기화
    /// <summary>
    public void SyncBlockRemovalForOwner(int actorNumber, int blockId, bool withAnimation)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        thisPhotonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, withAnimation, 0);
    }

    #endregion

    #region 타워 붕괴 알림

    // 클라이언트 → 마스터: "이 사람의 타워를 붕괴시켜 주세요"
    public void RequestTowerCollapse_MasterAuth(int ownerActorNumber)
    {
        Debug.Log($"[JengaNetwork] RequestTowerCollapse_MasterAuth called. actor={ownerActorNumber}, IsMaster={PhotonNetwork.IsMasterClient}");

        if (PhotonNetwork.IsMasterClient)
        {
            // 마스터면 바로 처리
            ProcessTowerCollapseRequest(ownerActorNumber);
        }
        else
        {
            // 비마스터면 마스터에게 RPC 요청
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
    /// 마스터에서 타워 붕괴 요청 처리
    /// </summary>
    private void ProcessTowerCollapseRequest(int ownerActorNumber)
    {
        Debug.Log($"[JengaNetwork] Processing tower collapse request for actor {ownerActorNumber}");

        // TowerManager/타워 존재 검증
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

        // 모든 클라이언트에게 붕괴 적용 브로드캐스트
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

    // RPC_ApplyTowerCollapse_All은 기존과 동일하되 로그 추가
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

        // 붕괴 애니메이션 실행
        JengaTowerManager.Instance.WithSuppressedCollapse(() =>
        {
            tower.TriggerCollapseOnce();
        });

        // 게임 로직 처리 (마스터만)
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log($"[JengaNetwork] Notifying GameManager of collapse for actor={ownerActorNumber}");
            JengaGameManager.Instance?.OnTowerCollapsed(ownerActorNumber);
        }
    }

    #endregion

    #region 타이머 동기화: 마스터 → 전체 클라이언트

    /// <summary>
    /// 마스터에서 모든 클라이언트에게 현재 남은 시간을 동기화
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

    #region 카운트다운 동기화: 마스터 → 전체 클라이언트

    /// <summary>
    /// 마스터에서 모든 클라이언트에게 카운트다운 시작 신호 송신
    /// </summary>
    public void BroadcastStartCountdown(float countdownDuration)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log($"[JengaNetwork] Broadcasting countdown start: {countdownDuration}s");
        thisPhotonView.RPC(nameof(RPC_StartCountdown), RpcTarget.All, countdownDuration);
    }

    /// <summary>
    /// [RPC] 모든 클라이언트에서 동시에 카운트다운 시작
    /// </summary>
    [PunRPC]
    private void RPC_StartCountdown(float duration)
    {
        Debug.Log($"[JengaNetwork] Received countdown start RPC: {duration}s");

        // UI 매니저에게 카운트다운 시작 알림
        JengaUIManager.Instance?.StartCountdown(duration);
    }

    /// <summary>
    /// 마스터에서 카운트다운 완료 후 게임 시작 신호 송신
    /// </summary>
    public void BroadcastCountdownComplete()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        thisPhotonView.RPC(nameof(RPC_CountdownComplete), RpcTarget.All);
    }

    /// <summary>
    /// [RPC] 카운트다운 완료 처리
    /// </summary>
    [PunRPC]
    private void RPC_CountdownComplete()
    {
        Debug.Log("[JengaNetwork] Received countdown complete RPC");
        // UI에서 카운트다운 숨기기
        JengaUIManager.Instance?.HideCountdown();
    }

    #endregion

    #region 관전자(레이트 조인) 동기화: 스냅샷
    /// <summary>
    /// 새 플레이어 입장 시(마스터에서만) 현재 스냅샷 전송
    /// </summary>
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        SendSnapshotTo(newPlayer.ActorNumber);
    }

    /// <summary>
    /// (옵션) 조인 직후 본인이 명시적으로 스냅샷 요청하고 싶을 때 호출
    /// </summary>
    public void RequestSnapshotFromMaster()
    {
        thisPhotonView.RPC(nameof(RPC_RequestSnapshot), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    /// <summary>
    /// [RPC] 스냅샷 요청 수신(마스터)
    /// </summary>
    [PunRPC]
    private void RPC_RequestSnapshot(int requesterActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SendSnapshotTo(requesterActorNumber);
    }

    /// <summary>
    /// 마스터 → 특정 플레이어: 스냅샷 전송(현재까지 제거된 블록 목록)
    /// </summary>
    public void SendSnapshotTo(int targetActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PhotonHashtable table = BuildSnapshotHashtable();
        var target = PhotonNetwork.CurrentRoom?.GetPlayer(targetActorNumber);
        if (target == null)
        {
            Debug.LogWarning($"[JengaNetwork - SendSnapshotTo] 실패: 대상 없음 actor = {targetActorNumber}");
            return;
        }

        thisPhotonView.RPC(nameof(RPC_ReceiveSnapshot), target, table);
    }

    /// <summary>
    /// [RPC] 스냅샷 수신(관전자/레이트 조인)
    /// </summary>
    [PunRPC]
    private void RPC_ReceiveSnapshot(PhotonHashtable table)
    {
        // 매니저 또는 타워가 아직 준비 전이면 준비될 때까지 대기 적용
        if (_pendingApplyCo != null) StopCoroutine(_pendingApplyCo);
        _pendingApplyCo = StartCoroutine(CoApplySnapshotWhenReady(table));
    }

    /// <summary>
    /// (마스터) 현재 제거 상태를 Photon Hashtable로 직렬화
    ///  key: actorNumber(int), value: removedBlockIds(int[])
    /// </summary>
    private PhotonHashtable BuildSnapshotHashtable()
    {
        var snap = JengaTowerManager.Instance?.SnapshotRemovedBlocks()
                   ?? new Dictionary<int, IReadOnlyCollection<int>>();

        PhotonHashtable table = new PhotonHashtable();
        foreach (var kv in snap)
        {
            // PUN 직렬화 호환을 위해 int[] 보장
            int[] arr = kv.Value is int[] a ? a : kv.Value.ToArray();
            table[kv.Key] = arr;
        }
        return table;
    }

    /// <summary>
    /// 타워 매니저가 준비될 때까지 대기 후 스냅샷 적용
    /// </summary>
    private IEnumerator CoApplySnapshotWhenReady(PhotonHashtable table)
    {
        // 매니저 존재 & 최소 한 번 Initialize가 끝났다고 가정되는 프레임까지 대기
        while (JengaTowerManager.Instance == null)
            yield return null;

        ApplySnapshotHashtable(table);
        _pendingApplyCo = null;
    }

    /// <summary>
    /// 수신한 스냅샷을 로컬 타워에 적용
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
        Debug.Log($"[JengaNetwork - ApplySnapshotHashtable] 스냅샷 적용 완료 (actors = {dict.Count})");
    }

    #endregion
}