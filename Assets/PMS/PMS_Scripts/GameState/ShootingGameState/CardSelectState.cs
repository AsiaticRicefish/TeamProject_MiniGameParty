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
        if(flag && CardManager.Instance.allPicked && PhotonNetwork.IsMasterClient) //다 눌렀을 때 플레이어들이 
        {
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
            flag = false;
        }
    }
    public override void Exit() 
    {
        Debug.Log("[ShootingGameState] - CardSelectState Exit");
        //CardUI가 사라지도록
    }
}