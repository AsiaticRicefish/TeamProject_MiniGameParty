using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ShootingScene;

public class CardSelectState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - CardSelect 상태에 진입");
        //플레이어 입력 가능하도록 처리
        //InputManager.Instance.EnableInput();
    }
    public override void Update() 
    { 

    }
    public override void Exit() { }
}
