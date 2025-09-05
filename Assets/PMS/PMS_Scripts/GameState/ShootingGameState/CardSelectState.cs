using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;

public class CardSelectState : ShootingGameState
{
    private bool flag = true;
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - CardSelectState Enter");
        //TurnManager.Instance.SetupTurn();
        //TurnManager.Instance.TestSetupTurn();
    }
    public override void Update() 
    {

    }
    public override void Exit() 
    {
        Debug.Log("[ShootingGameState] - CardSelectState Exit");

        //카드 선택이 다된 시점
        ShootingNetworkManager.Instance.ShootingGameTurnAndRoundRoomPropertiesReigster();
        if (PhotonNetwork.IsMasterClient)
        {
            ShootingGameManager.Instance.photonView.RPC("InputOn", RpcTarget.All);
        }
    }
}