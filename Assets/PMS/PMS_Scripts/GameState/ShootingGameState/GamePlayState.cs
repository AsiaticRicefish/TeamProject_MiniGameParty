using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
using PMS_Util;

public class GamePlayState : ShootingGameState
{
    public override SH_GameStateType GameStateType =>   SH_GameStateType.GamePlay;
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Enter");

        if (PhotonNetwork.IsMasterClient)
        {
            WindSystem.Instance.UpdateWind();
            TurnManager.Instance.BroadcastCurrentTurn();
        }

        //ShootingNetworkManager.Instance.ShootingGameTurnAndRoundRoomPropertiesReigster();
        //if (PhotonNetwork.IsMasterClient)
        //{
        //    ShootingGameManager.Instance.photonView.RPC("InputOn", RpcTarget.All);
        //    TurnManager.Instance.NextTurn();
        //}
    }
    public override void Update()
    {

    }
    public override void Exit()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Exit");
        if (PhotonNetwork.IsMasterClient)
        {
            ShootingGameManager.Instance.CheckRanking();
        }
    }
}
