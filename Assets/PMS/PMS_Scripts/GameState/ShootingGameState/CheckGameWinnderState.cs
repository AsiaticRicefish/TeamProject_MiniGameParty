using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
public class CheckGameWinnderState : ShootingGameState
{
    public override void Enter() 
    {
        if (PhotonNetwork.IsMasterClient)
        {
            ShootingGameManager.Instance.photonView.RPC("InputOff", RpcTarget.All);
        }
        ShootingNetworkManager.Instance.ShootingGameTurnAndRoundRoomPropertiesUnReigster();


        Debug.Log("[CheckGameWinnderState] - CheckGameWinnderState Enter");
        Debug.Log($"[CheckGameWinnderState] - 내 FireBaseUID {PMS_Util.Util.GetMyUid()}");
        if (PhotonNetwork.IsMasterClient)
        {       
            ShootingGameManager.Instance.CheckGameWinner();
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GameEndState");
        }      
    }
    public override void Update() 
    { 
        
    }
    public override void Exit() 
    {
        Debug.Log("[CheckGameWinnderState] - CheckGameWinnderState Exit");
    }
}
