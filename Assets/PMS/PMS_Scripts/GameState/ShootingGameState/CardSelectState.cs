using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ShootingScene;

public class CardSelectState : ShootingGameState
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
    public override void Exit() { }
}
