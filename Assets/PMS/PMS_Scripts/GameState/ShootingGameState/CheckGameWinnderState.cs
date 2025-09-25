using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
using PMS_Util;

public class CheckGameWinnderState : ShootingGameState
{
    public override SH_GameStateType GameStateType => SH_GameStateType.CheckGameWinner;
    public override void Enter() 
    {       
        Debug.Log("[CheckGameWinnderState] - CheckGameWinnderState Enter");

        ShootingNetworkManager.Instance.ShootingGameTurnAndRoundRoomPropertiesUnReigster();

        //BGM끄고 게임 종료 효과음 호출
        SoundManager.Instance.PauseBGM();
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
