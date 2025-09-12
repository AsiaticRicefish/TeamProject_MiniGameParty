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
        if (PhotonNetwork.IsMasterClient)
        {
            double lead = 0.3;
            double duration = 9.0; // 10초
            double startAt = PhotonNetwork.Time + lead;
            double endAt = startAt + duration;
            CardManager.Instance.BuildAndBroadcastDeck();
            ShootingNetworkManager.Instance.photonView.RPC("RPC_StartTimer", RpcTarget.All, startAt,endAt);
            //CardManager.Instance.StartAutoCardSelect();
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
        if (PhotonNetwork.IsMasterClient)
        {
            ShootingGameManager.Instance.photonView.RPC("InputOn", RpcTarget.All);
        }
    }
}