using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
public class GamePlayState : ShootingGameState
{
    public override void Enter()
    {
        Debug.Log("[ShootingGameState] - GamePlayState Enter");

        if (PhotonNetwork.IsMasterClient)
        {
            WindSystem.Instance.UpdateWind();
            TurnManager.Instance.BroadcastCurrentTurn();
        }       

        if (!TurnManager.Instance.IsMyTurn())       //턴 정보 업데이트 전에 호출
        {
            ShootingCameraManager.Instance.SwipePosInit();
            ShootingScene.PlayerInputManager.Instance.EnableInput();
            ShootingScene.PlayerInputManager.Instance.DisableCameraControl();
            ShootingScene.PlayerInputManager.Instance.DisableCameraPosition();
            Debug.Log("난 인풋 활성화");
        }
        else
        {
            ShootingScene.PlayerInputManager.Instance.DisableInput();
            ShootingScene.PlayerInputManager.Instance.EnableCameraControl();
            ShootingScene.PlayerInputManager.Instance.EnableCameraPosition();
            Debug.Log("난 인풋 비활성화");
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
