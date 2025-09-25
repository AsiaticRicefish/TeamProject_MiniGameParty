using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using PMS_Util;
using ShootingScene;
using ShootingScene.ShootingGame;
using UnityEngine;

public class TurnCheckState : ShootingGameState
{
    public override SH_GameStateType GameStateType => SH_GameStateType.TurnCheck;

    public override void Enter() 
    {
        Debug.Log("[TurnCheckState] - TurnCheckState Enter");
        if (PhotonNetwork.IsMasterClient)
        {
            TurnManager.Instance.TurnCheck();
        }
        ShootingUIManager.Instance.ShowWindUI();
    }
    public override void Update() { }
    public override void Exit() 
    {
        Debug.Log("[TurnCheckState] - TurnCheckState Exit");
    }
}
