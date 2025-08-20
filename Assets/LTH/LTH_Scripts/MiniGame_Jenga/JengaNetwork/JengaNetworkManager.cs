using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// 젠가 게임의 네트워크 동기화를 담당하는 전용 매니저
/// - 블록 제거 애니메이션
/// - 타이밍 게임 결과
/// - 게임 상태
/// 등의 네트워크 통신을 Photon RPC를 통해 처리
/// </summary>
public class JengaNetworkManager : PunSingleton<JengaNetworkManager>, IGameComponent
{

    /// <summary>
    /// 젠가 씬에서만 살아있는 일시적 싱글톤
    /// </summary>
    protected override void OnAwake()
    {
        base.isPersistent = false;
    }

    /// <summary>
    /// 매니저 초기화 진입점 (IGameComponent용)
    /// </summary>
    public void Initialize()
    {
        Debug.Log("JengaNetworkManager 초기화 완료");
    }


    #region 플레이어 결과 보고 → 마스터

    /// <summary>
    /// 클라이언트가 자신의 행동 결과(성공/실패, 점수)를 마스터에게 보고
    /// </summary>
    public void SendPlayerActionResult(string uid, bool success, int score)
    {
        photonView.RPC(nameof(ReceivePlayerActionResult), RpcTarget.MasterClient, uid, success, score);
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
        photonView.RPC(nameof(ReceiveGameState), RpcTarget.All, state.ToString());
    }

    /// <summary>
    /// [RPC] 수신된 게임 상태를 GameManager에 반영
    /// </summary>
    [PunRPC]
    private void ReceiveGameState(string stateStr)
    {
        if (System.Enum.TryParse(stateStr, out JengaGameState state))
        {
            Debug.Log($"[JengaNetwork - ReceiveGameState] 게임 상태 동기화 수신: {state}");
            JengaGameManager.Instance.ApplyGameStateChange(state);
        }
    }

    #endregion

    #region  타이밍 미니게임 결과: 클라이언트 → 마스터

    /// <summary>
    /// 클라이언트가 타이밍 게임의 결과(정확도 포함)를 마스터에게 보고
    /// </summary>
    public void SendTimingResult(string uid, bool success, float accuracy)
    {
        photonView.RPC(nameof(ReceiveTimingResult), RpcTarget.MasterClient, uid, success, accuracy);
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

    #region  블록 제거 애니메이션 동기화: 마스터 → 전체 클라이언트

    /// <summary>
    /// 블록 제거 시 애니메이션 연출을 전체 클라이언트에 동기화
    /// </summary>
    public void SyncBlockRemoval(int blockId, bool isSuccess)
    {
        photonView.RPC(nameof(ReceiveBlockRemoval), RpcTarget.All, blockId, isSuccess);
    }

    /// <summary>
    /// [RPC] 블록 제거 연출 수신 (현재는 로그 출력만 처리, 향후 연동 예정)
    /// </summary>
    [PunRPC]
    private void ReceiveBlockRemoval(int blockId, bool isSuccess)
    {
        Debug.Log($"[JengaNetwork - ReceiveBlockRemoval] 블록 제거 애니메이션 동기화: BlockId = {blockId}, 성공여부 = {isSuccess}");

        // TODO: 타워 부분에서 처리 예) JengaTowerManager.Instance.RemoveBlock(blockId, isSuccess);
    }

    #endregion
}