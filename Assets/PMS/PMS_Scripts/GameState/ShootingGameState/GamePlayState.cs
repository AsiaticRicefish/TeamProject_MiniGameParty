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
            TurnManager.Instance.NextTurn();
        }
    }
    public override void Update()
    {
        if(ShootingGameManager.Instance.CurrentRound < ShootingGameManager.Instance.MaxRounds)
        {
            TurnManager.Instance.StartTurnCorutine(10.0f);
        }
    }
    public override void Exit()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Exit");
    }
}
