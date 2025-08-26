using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
public class GamePlayState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Enter");
        if (PhotonNetwork.IsMasterClient)
        {
            TurnManager.Instance.BroadcastCurrentTurn();
        }
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
