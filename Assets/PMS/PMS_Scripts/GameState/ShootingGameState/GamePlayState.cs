using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamePlayState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - CardSelect ���¿� ����");
        //�÷��̾� �Է� �����ϵ��� ó��
        //InputManager.Instance.EnableInput();
    }
    public override void Update()
    {

    }
    public override void Exit()
    {
        Debug.Log("[ShootingGameState] - CardSelect ���¿��� ���");
    }
}
