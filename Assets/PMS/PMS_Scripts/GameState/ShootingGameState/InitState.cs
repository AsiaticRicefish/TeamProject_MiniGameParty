using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using PMS_Util;

public class InitState : ShootingGameState
{
    public override SH_GameStateType GameStateType => SH_GameStateType.Init;

    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - InitState 상태에 진입");
        //SetupUI();
        //manager.ChangeState(new CardPlacementState(manager));
    }
    public override void Update() 
    {

    }

    public override void Exit()
    {
        Debug.Log("[ShootingGameState] - InitState 상태에서 벗어남");       
    }
}
