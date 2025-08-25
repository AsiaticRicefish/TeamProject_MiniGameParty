using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;

public class CardSelectState : ShootingGameState
{
    private bool hasBroadcasted = false;
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - CardSelectState Enter");
        //CardUI가 나타나도록
    }
    public override void Update() 
    { 
        if(!hasBroadcasted && PhotonNetwork.IsMasterClient && true) //ShootingGameManager.Instance.IsTurnSetup
        {
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
            hasBroadcasted = true;
        }
    }
    public override void Exit() 
    {
        Debug.Log("[ShootingGameState] - CardSelectState Exit");
        //CardUI가 사라지도록
    }
}