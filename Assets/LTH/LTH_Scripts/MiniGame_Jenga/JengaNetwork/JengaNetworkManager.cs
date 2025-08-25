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

        base.OnAwake();

        // 중복 PhotonView가 붙어 있으면 정리
        var pvs = GetComponents<PhotonView>();
        if (pvs.Length > 1)
        {
            for (int i = 1; i < pvs.Length; i++) Destroy(pvs[i]);
        }
    }

    /// <summary>
    /// 매니저 초기화 진입점
    /// </summary>
    public void Initialize()
    {
//#if PHOTON_UNITY_NETWORKING_2_OR_NEWER
//          if (PhotonNetwork.InRoom && thisPhotonView.ViewID == 0)
//        {
//            if (!PhotonNetwork.AllocateViewID(thisPhotonView))
//                Debug.LogError("[NM] AllocateViewID failed. (씬에 미리 배치 + Scene ViewID 권장)");
//        }
//#endif
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
        thisPhotonView.RPC(nameof(RPC_RequestBlockRemoval), RpcTarget.MasterClient, actorNumber, blockId, clientSuggestedScore, clientAccuracy);
    }

    // === 마스터에서 수신/검증 ===
    [PunRPC]
    private void RPC_RequestBlockRemoval(int actorNumber, int blockId, int clientSuggestedScore, float clientAccuracy)
    {
        Debug.Log($"[NET] REQ_Remove recv on Master actor={actorNumber} blockId ={blockId} acc={clientAccuracy:0.00}");

        // A) 마스터/상태
        if (!PhotonNetwork.IsMasterClient) { Debug.LogWarning("[NET] Reject: not master"); return; }

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


        // --- 타워/블록 해석 ---
        var tm = JengaTowerManager.Instance;
        if (tm == null) { Debug.LogError("[NET] Reject: TowerManager null"); ReplyDeny(actorNumber, blockId, "towerMgr-null"); return; }

        // 타워/블록 조회
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


        // C) 제거 가능성 검증 (캐시 vs 규칙)
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

        // D) 점수 계산(서버 결정)
        int bonus = Mathf.Clamp(Mathf.RoundToInt(clientAccuracy * MAX_BONUS), 0, MAX_BONUS);
        int finalScore = BASE_SCORE + bonus;

        Debug.Log($"[NET] OK → Broadcast Apply owner={actorNumber} blockId={blockId} score={finalScore}");
        thisPhotonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, true, finalScore, true);
    }

    [PunRPC]
    private void RPC_ApplyBlockRemoval(int ownerActorNumber, int blockId, bool withAnimation, int score, bool isSuccess = true)
    {
        Debug.Log($"[NET] APPLY_Remove recv owner={ownerActorNumber} blockId={blockId} withAnim={withAnimation} succ={isSuccess}");
        // 1) 실제 제거 반영
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);
        var block = tower?.GetBlockById(blockId);

        if (block != null && withAnimation)
        {
            block.RemoveWithAnimation(isSuccess); // isSuccess 파라미터 전달
        }
        else
        {
            tower?.ApplyBlockRemoval(blockId, withAnimation, isSuccess);
        }

        // 2) 점수 반영은 마스터만 집계
        if (PhotonNetwork.IsMasterClient)
        {
            // ActorNumber → UID 변환이 필요하다면 PlayerList의 CustomProperties["uid"] 참조
            string uid = TryGetUidFromActor(ownerActorNumber);
            if (!string.IsNullOrEmpty(uid))
            {
                JengaGameManager.Instance?.ApplyPlayerActionResult(uid, success: true, scoreGained: score);
            }
        }

        // 3) 공통 연출/사운드/UI
        // JengaUIManager
        // JengaSoundManager
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
        thisPhotonView.RPC(nameof(RPC_RequestTowerCollapse_Master), RpcTarget.MasterClient, ownerActorNumber);
    }

    [PunRPC]
    private void RPC_RequestTowerCollapse_Master(int ownerActorNumber, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        thisPhotonView.RPC(nameof(RPC_ApplyTowerCollapse_All), RpcTarget.All, ownerActorNumber);
    }

    // 전체 적용: 모든 클라에서 같은 코드로 실제 붕괴 실행
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