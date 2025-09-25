using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
using PMS_Util;

public class GameEndState : ShootingGameState
{
    public override SH_GameStateType GameStateType => SH_GameStateType.GameEnd;
    public override void Enter()
    {
        Debug.Log("[GameEndState] - GameEndState Enter");

        //각자 풀로 생성했던 유니모 제거
        EggManager.Instance.DestroyAllMyEggs();

        //슈팅게임 룸프로퍼티 게임상태 구독 해제 
        ShootingNetworkManager.Instance.ShootingGameSceneChangeRoomPropertiesUnReigster();

        //룸 프로퍼티(마스터만) 및 플레이어 프로퍼티 초기화(로컬)        
        ShootingNetworkManager.Instance.ClearShootingGamePlayerProperties();

        //플레이어 인풋 매니저 구독 해제 처리
        PlayerInputManager.Instance.Cleanup();

        //사운드 정리
        SoundManager.Instance.StopAllSounds();

        if (PhotonNetwork.IsMasterClient)
        {          
            ShootingNetworkManager.Instance.ClearShootingGameRoomProperties();
            ShootingGameManager.Instance.EndGame();
        }
        
    }
}
