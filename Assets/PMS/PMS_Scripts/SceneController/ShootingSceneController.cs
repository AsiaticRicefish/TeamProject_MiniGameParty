using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using ShootingScene;

public class ShootingSceneController : BaseGameSceneController
{
    //따로 이벤트는 없는거 같음
    public Action OnGameStarted;

    protected override string GameType => "Shooting";

    protected override IEnumerator WaitForManagersAwake()
    {
        yield return new WaitUntil(() =>
            ShootingGameManager.Instance != null &&
            TurnManager.Instance != null);

        Debug.Log("모든 ShootingGameScene 매니저 Awake완료");
    }

    //순차 초기화
    protected override IEnumerator InitializeSequentialManagers()
    {
        Debug.Log("ShootingGameScene 순차 초기화 시작");

        var sequentialComponents = new List<IGameComponent>();

        // 의존성 순서대로 추가
        /* if (ShootingGameManager.Instance is IGameComponent gameManagerComp)
            sequentialComponents.Add(gameManagerComp);*/

        /* if (TurnManager.Instance is IGameComponent turnManagerComp)
            sequentialComponents.Add(turnManagerComp);*/

        // 필요하다면 다른 순차 초기화 컴포넌트들 추가

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));
    }

    //벙렬 초기화
    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("ShootingGameScene 병렬 초기화 시작");

        var parallelComponents = new List<ICoroutineGameComponent>();

        // 독립적으로 초기화 가능한 컴포넌트들 추가
        /*if (ShootingGameManager.Instance is ICoroutineGameComponent gameComp)
            parallelComponents.Add(gameComp);*/

        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
    }

    
    protected override void NotifyGameStart()
    {
        //OnGameStarted?.Invoke();

        if (ShootingGameManager.Instance is IGameStartHandler gameStart)
            gameStart.OnGameStart();

        Debug.Log("Shooting 게임 시작 알림 완료");
    }

}
