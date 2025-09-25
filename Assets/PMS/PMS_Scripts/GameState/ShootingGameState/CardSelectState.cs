using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
using PMS_Util;

public class CardSelectState : ShootingGameState
{

    public override SH_GameStateType GameStateType => SH_GameStateType.CardSelect;
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - CardSelectState Enter");
        if (PhotonNetwork.IsMasterClient)
        {

            CardManager.Instance.BuildAndBroadcastDeck();
        }
        else
        {
            // 이미 방에 deck이 있을 수 있으니 즉시 읽기 시도
            CardManager.Instance.TryInitFromRoomProps();
        }     
    }

    public override void Update() 
    {

    }

    public override void Exit() 
    {
        Debug.Log("[ShootingGameState] - CardSelectState Exit");
        //카드 선택이 다된 시점
        ShootingNetworkManager.Instance.ShootingGameTurnAndRoundRoomPropertiesReigster();

        ShootingScene.ShootingGame.ShootingUIManager.Instance.StartRanking();
    }
}