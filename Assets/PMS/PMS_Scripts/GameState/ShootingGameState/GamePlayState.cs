using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamePlayState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Enter");
    }
    public override void Update()
    {
        if(ShootingGameManager.Instance.CurrentRound < ShootingGameManager.Instance.MaxRounds)
        {

        }
    }
    public override void Exit()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Exit");
    }
}
