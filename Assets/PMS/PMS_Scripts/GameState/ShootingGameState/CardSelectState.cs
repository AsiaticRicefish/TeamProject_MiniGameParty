using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;

public class CardSelectState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - CardSelectState Enter");
        //TurnManager.Instance.SetupTurn();
        //TurnManager.Instance.TestSetupTurn();
    }
    public override void Update() 
    {
        /*if(true) //다 눌렀을 때 플레이어들이 
        {
            TurnManager.Instance.SetupTurn();
        }*/
    }
    public override void Exit() 
    {
        Debug.Log("[ShootingGameState] - CardSelectState Exit");
        //CardUI가 사라지도록
    }
}