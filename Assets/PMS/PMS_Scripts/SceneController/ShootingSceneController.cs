using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Photon.Pun;
using ShootingScene;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class ShootingSceneController : BaseGameSceneController
{
    //따로 이벤트는 없는거 같음
    public Action OnGameStarted;

    protected override string GameType => "Shooting";

    protected override IEnumerator WaitForManagersAwake()
    {
        yield return WaitForSingletonReady<ShootingNetworkManager>();
        yield return WaitForSingletonReady<ShootingGameManager>();
        yield return WaitForSingletonReady<RoomPropertyObserver>();
        yield return WaitForSingletonReady<PlayerInputManager>();
        //턴매니저 추가
        yield return WaitForSingletonReady<TurnManager>();
        //카드 매니저 추가 
        yield return WaitForSingletonReady<CardManager>();
        yield return WaitForSingletonReady<Test_ShotFollowCamera>();
        yield return WaitForSingletonReady<EggManager>();

        Debug.Log("모든 ShootingGameScene 매니저 Awake완료");
    }

    //순차 초기화
    protected override IEnumerator InitializeSequentialManagers()
    {
        Debug.Log("ShootingGameScene 순차 초기화 시작");

        var sequentialComponents = new IGameComponent[]
        {
            RoomPropertyObserver.Instance,
            ShootingNetworkManager.Instance,
            ShootingGameManager.Instance,
            PlayerInputManager.Instance,
            TurnManager.Instance,
            EggManager.Instance,
        };

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));
    }

    //벙렬 초기화
    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("ShootingGameScene 병렬 초기화 시작");

        var parallelComponents = new List<ICoroutineGameComponent>();

        //parallelComponents.Add()

        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
    }

    
    protected override void NotifyGameStart()
    {
        //OnGameStarted?.Invoke();
        if (ShootingGameManager.Instance == null)
        {
            Debug.LogError("[NotifyGameStart] ShootingGameManager is NULL");
            return;
        }
        try
        {
            if (PhotonNetwork.IsMasterClient)
            {
                RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "CardSelectState");
            }
            // else if(RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.State).ToString() == "CardSelectState")
            // {
            //     Debug.Log("호출?");
            //     //이미 변경되어 룸프로퍼티가 callback을 못받았을 때
            //     //지연보상
            //     //늦게 들어와서 따로 RoomCallBack 못받은 상황에서는 자신의 State 변경 요청해야한다. 클라이언트 -> 마스터 클라이언트
            //     string state = (string)PhotonNetwork.CurrentRoom.CustomProperties[ShootingGamePropertyKeys.State];
            //     ShootingGameManager.Instance.ChangeStateByName("CardSelectState");//(state);
            // }
            //나머지 클라이어트도 룸프로퍼티 변경으로 인한 콜백함수로 ChangeState 실행되겠지?
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NotifyGameStart] {ex}\n{ex.StackTrace}");
        }
    }
}
