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
            ShootingGameManager.Instance.photonView.RPC("InputOn", RpcTarget.All);
            TurnManager.Instance.NextTurn();
        }
    }
    public override void Update()
    {

    }
    public override void Exit()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Exit");

        if (PhotonNetwork.IsMasterClient)
        {
            ShootingGameManager.Instance.photonView.RPC("InputOff", RpcTarget.All);
        }
    }
}
