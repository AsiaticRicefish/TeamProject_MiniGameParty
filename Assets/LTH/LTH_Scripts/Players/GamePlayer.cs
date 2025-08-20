using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �� �÷��̾��� ���� �� ����(UID, �г���, ��, ��ġ, �¸� ���� ��)�� �����ϰ� ����
/// --------------------------------------------------------------------------------
/// �� �÷��̾��� ���� �ĺ� ����(Firebase UID, �г���) ����
/// ���� ���� ���� ���� ���� (�� ����, �غ� ����, ���� ��ġ ��)
/// �̴ϰ��� �� ���θ� ��� ���� (�¸� ����, �¼� ��)
/// ���� �帧 ���� �������� ���� ������ Ȱ���
/// </summary>


public class GamePlayer : MonoBehaviour
{
    #region �÷��̾��� ���� ����
    public string PlayerId { get; private set; }    // Firebase UID
    public string Nickname { get; private set; }    // �÷��̾� �г��� (Photon)
    #endregion

    #region �÷��̾� ���� ����
    public bool IsReady { get; private set; }       // ���� �÷��̾� ���� ���� �غ� ����
    public bool IsTurn { get; private set; }        // ���� �÷��̾��� �� ����
    #endregion

    public bool WinThisMiniGame { get; set; }       // �̴ϰ��ӿ��� �¸� ����

    public int BoardPosition { get; set; }          // ���� ���忡���� ��ġ (0���� ����, 0�� ������)


    // ��ü ���ӿ��� �̱� Ƚ�� (�̰� ���� �����̳� ���Ŀ� ��ũ�� ����ϴ� ��� ���)
    public int WinCount { get; set; }

    public GamePlayer(string id, string nickname)
    {
        PlayerId = id;
        Nickname = nickname;

        IsReady = false;
        IsTurn = false;

        WinCount = 0;
        BoardPosition = 0;
        WinThisMiniGame = false;
    }
}
