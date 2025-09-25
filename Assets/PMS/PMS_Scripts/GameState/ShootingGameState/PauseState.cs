using System.Collections;
using System.Collections.Generic;
using PMS_Util;
using UnityEngine;

public class PauseState : ShootingGameState
{
    public override SH_GameStateType GameStateType => SH_GameStateType.Pause;

    public override void Enter()
    {
        Debug.LogWarning("[PauseState] - 게임 일시정지 상태 Enter");
    }

    public override void Update()
    {
       
    }
    
    public override void Exit() 
    {
    
    } 
}
