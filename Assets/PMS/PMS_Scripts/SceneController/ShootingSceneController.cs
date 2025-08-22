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

        Debug.Log("모든 ShootingGameScene 매니저 Awake완료");
    }

    //순차 초기화
    protected override IEnumerator InitializeSequentialManagers()
    {
        Debug.Log("ShootingGameScene 순차 초기화 시작");

        var sequentialComponents = new IGameComponent[]
        {
            ShootingNetworkManager.Instance,
            ShootingGameManager.Instance
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
            ShootingGameManager.Instance.OnGameStart();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NotifyGameStart] {ex}\n{ex.StackTrace}");
        }
    }

}
