using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GameEndState : ShootingGameState
{
    public override void Enter()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            ShootingGameManager.Instance.EndGame();
        }
    }
    public override void Update()
    {

    }
    public override void Exit()
    {

    }
}
