using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// ���� ������ ��Ʈ��ũ ����ȭ�� ����ϴ� ���� �Ŵ���
/// - ��� ���� �ִϸ��̼�
/// - Ÿ�̹� ���� ���
/// - ���� ����
/// ���� ��Ʈ��ũ ����� Photon RPC�� ���� ó��
/// </summary>
public class JengaNetworkManager : PunSingleton<JengaNetworkManager>, IGameComponent
{

    /// <summary>
    /// ���� �������� ����ִ� �Ͻ��� �̱���
    /// </summary>
    protected override void OnAwake()
    {
        base.isPersistent = false;
    }

    /// <summary>
    /// �Ŵ��� �ʱ�ȭ ������ (IGameComponent��)
    /// </summary>
    public void Initialize()
    {
        Debug.Log("JengaNetworkManager �ʱ�ȭ �Ϸ�");
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
        photonView.RPC(nameof(ReceiveGameState), RpcTarget.All, state.ToString());
    }

    /// <summary>
    /// [RPC] ���ŵ� ���� ���¸� GameManager�� �ݿ�
    /// </summary>
    [PunRPC]
    private void ReceiveGameState(string stateStr)
    {
        if (System.Enum.TryParse(stateStr, out JengaGameState state))
        {
            Debug.Log($"[JengaNetwork - ReceiveGameState] ���� ���� ����ȭ ����: {state}");
            JengaGameManager.Instance.ApplyGameStateChange(state);
        }
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

    #region  ��� ���� �ִϸ��̼� ����ȭ: ������ �� ��ü Ŭ���̾�Ʈ

    /// <summary>
    /// ��� ���� �� �ִϸ��̼� ������ ��ü Ŭ���̾�Ʈ�� ����ȭ
    /// </summary>
    public void SyncBlockRemoval(int blockId, bool isSuccess)
    {
        photonView.RPC(nameof(ReceiveBlockRemoval), RpcTarget.All, blockId, isSuccess);
    }

    /// <summary>
    /// [RPC] ��� ���� ���� ���� (����� �α� ��¸� ó��, ���� ���� ����)
    /// </summary>
    [PunRPC]
    private void ReceiveBlockRemoval(int blockId, bool isSuccess)
    {
        Debug.Log($"[JengaNetwork - ReceiveBlockRemoval] ��� ���� �ִϸ��̼� ����ȭ: BlockId = {blockId}, �������� = {isSuccess}");

        // TODO: Ÿ�� �κп��� ó�� ��) JengaTowerManager.Instance.RemoveBlock(blockId, isSuccess);
    }

    #endregion
}