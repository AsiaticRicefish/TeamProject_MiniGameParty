using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;

public class GameEndState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[GameEndState] - GameEndState Enter");

        //슈팅게임 룸프로퍼티 게임상태 구독 해제 
        ShootingNetworkManager.Instance.ShootingGameSceneChangeRoomPropertiesUnReigster();

        //룸 프로퍼티(마스터만) 및 플레이어 프로퍼티 초기화(로컬)        
        ShootingNetworkManager.Instance.ClearShootingGamePlayerProperties();

        if (PhotonNetwork.IsMasterClient)
        {
            ShootingNetworkManager.Instance.ClearShootingGameRoomProperties();
            ShootingGameManager.Instance.EndGame();
        }
    }
    public override void Update()
    {

    }
    public override void Exit()
    {

    }
}
