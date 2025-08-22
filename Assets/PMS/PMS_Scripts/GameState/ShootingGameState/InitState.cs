using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class InitState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - InitState 상태에 진입");
        //SetupUI();
        //manager.ChangeState(new CardPlacementState(manager));
    }
    public override void Update() 
    {
        /*if (PhotonNetwork.IsMasterClient) 
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ShootingGameManager.Instance.photonView.RPC(nameof(ShootingGameManager.Instance.RPC_ChangeState), RpcTarget.All, "CardSelectState");
            }
        }*/
    }

    public override void Exit()
    {
        Debug.Log("[ShootingGameState] - InitState 상태에서 벗어남");
    }
}
