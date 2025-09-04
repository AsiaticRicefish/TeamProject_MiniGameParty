using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using ShootingScene;
using UnityEngine;

public class TurnCheckState : ShootingGameState
{
    public override void Enter() 
    {
        Debug.Log("[TurnCheckState] - TurnCheckState Enter");
        if (PhotonNetwork.IsMasterClient)
        {
            TurnManager.Instance.TurnCheck();
        }
        

    }
    public override void Update() { }
    public override void Exit() 
    {
        Debug.Log("[TurnCheckState] - TurnCheckState Exit");
    }
}
