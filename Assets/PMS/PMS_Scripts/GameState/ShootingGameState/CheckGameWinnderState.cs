using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
public class CheckGameWinnderState : ShootingGameState
{
    public override void Enter() 
    {       
        Debug.Log("[CheckGameWinnderState] - CheckGameWinnderState Enter");

        ShootingNetworkManager.Instance.ShootingGameTurnAndRoundRoomPropertiesUnReigster();

        if (PhotonNetwork.IsMasterClient)
        {       
            ShootingGameManager.Instance.CheckGameWinner();
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GameEndState");
        }      
    }

    public override void Exit() 
    {
        Debug.Log("[CheckGameWinnderState] - CheckGameWinnderState Exit");
    }
}
