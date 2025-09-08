using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using InputBlocker;

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

    // 입력 차단 토큰
    private InputLockToken _countdownLock;
    private Coroutine _countdownFailsafeCo;

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
        var gm = JengaGameManager.Instance;
 
        if (gm.currentState != JengaGameState.Playing)
        {
            ReplyDeny(actorNumber, blockId, "state-not-playing");
            return;
        }

        var tm = JengaTowerManager.Instance;
        if (tm == null) { ReplyDeny(actorNumber, blockId, "towerMgr-null"); return; }

        var tower = tm.GetPlayerTower(actorNumber);
        if (tower == null)
        {
            ReplyDeny(actorNumber, blockId, "tower-null");
            return;
        }

        var block = tower?.GetBlockById(blockId);
        if (block == null)
        {
            ReplyDeny(actorNumber, blockId, "block-null");
            return;
        }

        if (block.OwnerActorNumber != actorNumber)
        {
            ReplyDeny(actorNumber, blockId, "owner-mismatch");
            return;
        }

        if (block.IsRemoved)
        {
            ReplyDeny(actorNumber, blockId, "already-removed");
            return;
        }

        if (tower.IsLayerTopProtected(block.Layer))
        {
            ReplyDeny(actorNumber, blockId, "top-protected");
            return;
        }

        // 규칙 검증 + (필요 시) 세션 시작
        bool validated = ValidateRemovalAndMaybeStartPairSession_OnMaster(tower, block);

        if (!validated)
        {
            ReplyDeny(actorNumber, blockId, "illegal-by-rule");
            return;
        }

        int bonus = Mathf.Clamp(Mathf.RoundToInt(clientAccuracy * MAX_BONUS), 0, MAX_BONUS);
        int finalScore = BASE_SCORE + bonus;

        thisPhotonView.RPC(nameof(RPC_ApplyBlockRemoval), RpcTarget.All, actorNumber, blockId, true, finalScore, true);
    }

    [PunRPC]
    private void RPC_ApplyBlockRemoval(int ownerActorNumber, int blockId, bool withAnimation, int score, bool isSuccess = true)
    {
        
        // 실제 제거 반영
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);

        if (tower == null)  return;

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
        if (block != null)
        {
            block.OnRemovalDenied(reason);
        }
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

    #region 블록 사이드 제거 세션 동기화
    private bool ValidateRemovalAndMaybeStartPairSession_OnMaster(JengaTower tower, JengaBlock block)
    {
        if (tower.IsLayerTopProtected(block.Layer))
            return false;

        // 같은 레이어에서 살아있는 블록들(인덱스 순)
        var alive = tower.allBlocks
            .Where(b => b.Layer == block.Layer && !b.IsRemoved)
            .OrderBy(b => b.IndexInLayer)
            .ToList();

        bool isSide = (block.IndexInLayer == 0 || block.IndexInLayer == 2);
        bool isCenter = (block.IndexInLayer == 1);

        if (tower.IsPairSessionActiveOn(block.Layer))
        {
            var expected = tower.GetExpectedSide(block.Layer);
            bool isExpectedSide = expected.HasValue && isSide && block.IndexInLayer == expected.Value;

            if (isExpectedSide)
            {
                tower.EndPairSession(block.Layer);
                BroadcastPairSessionEnd(block.OwnerActorNumber, block.Layer);
            }

            return isExpectedSide;
        }


        // 세션 아님(평상시)
        if (alive.Count == 3)
        {
            if (isCenter)  return true;

            var centerBlock = alive.FirstOrDefault(x => x.IndexInLayer == 1);
            bool centerAllowed = centerBlock != null && tower.CanRemoveBlock(centerBlock);

            if (isSide && centerAllowed)
            {
                tower.BeginPairSession(block.Layer, block.IndexInLayer);
                BroadcastPairSessionStart(block.OwnerActorNumber, block.Layer, block.IndexInLayer);
                return true;
            }
            return false;
        }

        else if (alive.Count == 2)
        {
            return false;
        }

        return false;
    }

    public void BroadcastPairSessionStart(int ownerActorNumber, int layer, int firstSideIndex)
    {
        thisPhotonView.RPC(nameof(RPC_StartPairSession), RpcTarget.All, ownerActorNumber, layer, firstSideIndex);
    }

    [PunRPC]
    private void RPC_StartPairSession(int ownerActorNumber, int layer, int firstSideIndex)
    {
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);
        if (tower != null)
        {
            tower.BeginPairSession(layer, firstSideIndex);
        }
        else
        {
            Debug.LogError($"[NET DEBUG] 타워를 찾을 수 없음: actor = {ownerActorNumber}");
        }
    }

    public void BroadcastPairSessionEnd(int ownerActorNumber, int layer)
    {
        thisPhotonView.RPC(nameof(RPC_EndPairSession), RpcTarget.All, ownerActorNumber, layer);
    }

    [PunRPC]
    private void RPC_EndPairSession(int ownerActorNumber, int layer)
    {
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);
        tower?.EndPairSession(layer);
    }

    #endregion

    #region 타워 붕괴 알림

    // 클라이언트 → 마스터: "이 사람의 타워를 붕괴시켜 주세요"
    public void RequestTowerCollapse_MasterAuth(int ownerActorNumber)
    {
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
                return;
            }

            thisPhotonView.RPC(nameof(RPC_RequestTowerCollapse_Master), RpcTarget.MasterClient, ownerActorNumber);
        }
    }

    // <summary>
    /// 마스터에서 타워 붕괴 요청 처리
    /// </summary>
    private void ProcessTowerCollapseRequest(int ownerActorNumber)
    {

        // TowerManager/타워 존재 검증
        var tm = JengaTowerManager.Instance;
        if (tm == null)
        {
            return;
        }

        var tower = tm.GetPlayerTower(ownerActorNumber);
        if (tower == null)
        {
            var uid = tm.GetOwnerUidByActor(ownerActorNumber);
            return;
        }

        // 모든 클라이언트에게 붕괴 적용 브로드캐스트
        thisPhotonView.RPC(nameof(RPC_ApplyTowerCollapse_All), RpcTarget.All, ownerActorNumber);
    }

    [PunRPC]
    private void RPC_RequestTowerCollapse_Master(int ownerActorNumber, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        ProcessTowerCollapseRequest(ownerActorNumber);
    }


    [PunRPC]
    private void RPC_ApplyTowerCollapse_All(int ownerActorNumber)
    {
        var tower = JengaTowerManager.Instance?.GetPlayerTower(ownerActorNumber);

        if (tower == null) return;

        // 붕괴 애니메이션 실행
        JengaTowerManager.Instance.WithSuppressedCollapse(() =>
        {
            tower.TriggerCollapseOnce(); // 여기서 붕괴 연출 시작부터 끝까지 이벤트까지 발생
        });

        // 게임 로직 처리 (마스터만)
        if (PhotonNetwork.IsMasterClient)
        {
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

        // 카운트다운 동안 입력 잠금 (모든 클라 공통)
        AcquireCountdownLock(duration);

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

        // 카운트다운 락 해제
        ReleaseCountdownLock();

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

    #region Util
    // === 카운트다운용 락 유틸 ===
    private void AcquireCountdownLock(float duration)
    {
        _countdownLock?.Dispose();
        _countdownLock = InputManager.Instance?.Acquire(
            InputType.Interaction | InputType.UI
        );

        if (_countdownFailsafeCo != null) StopCoroutine(_countdownFailsafeCo);
        _countdownFailsafeCo = StartCoroutine(CoReleaseCountdownAfter(duration + 1.2f));
    }

    private IEnumerator CoReleaseCountdownAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        ReleaseCountdownLock();
    }

    private void ReleaseCountdownLock()
    {
        if (_countdownFailsafeCo != null) { StopCoroutine(_countdownFailsafeCo); _countdownFailsafeCo = null; }
        _countdownLock?.Dispose();
        _countdownLock = null;
    }

    #endregion

    #region 강제 정리 (플레이어 1명이라도 이탈 시 호출)

    protected override void OnDestroy()
    {
        Debug.Log("[JengaNetworkManager] OnDestroy - cleaning up resources");

        // 실행 중인 코루틴들 정리
        if (_pendingApplyCo != null)
        {
            StopCoroutine(_pendingApplyCo);
            _pendingApplyCo = null;
        }

        if (_countdownFailsafeCo != null)
        {
            StopCoroutine(_countdownFailsafeCo);
            _countdownFailsafeCo = null;
        }

        // 입력 락 해제
        ReleaseCountdownLock();

        base.OnDestroy();
    }

    #endregion
}