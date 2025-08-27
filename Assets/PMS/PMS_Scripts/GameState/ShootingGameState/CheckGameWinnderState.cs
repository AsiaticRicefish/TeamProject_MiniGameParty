using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CheckGameWinnderState : ShootingGameState
{
    public override void Enter() 
    {
        Debug.Log("[CheckGameWinnderState] - CheckGameWinnderState Enter");
        Debug.Log($"[CheckGameWinnderState] - 내 FireBaseUID {PMS_Util.PMS_Util.GetMyUid()}");
        if (PhotonNetwork.IsMasterClient)
        {
            ShootingGameManager.Instance.CheckGameWinner();
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
