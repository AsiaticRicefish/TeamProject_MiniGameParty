using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

public class JengaSceneController : BaseGameSceneController
{
    protected override string GameType => "Jenga";

    protected override IEnumerator WaitForManagersAwake()
    {
        // 각 매니저들이 Awake에서 생성되기를 기다림
        yield return WaitForSingletonReady<JengaGameManager>();
        // yield return WaitForSingletonReady<JengaUIManager>();
        // yield return WaitForSingletonReady<JengaNetworkManager>();
        // yield return WaitForSingletonReady<JengaTowerManager>();
        // yield return WaitForSingletonReady<JengaTimingManager>();
        // yield return WaitForSingletonReady<JengaSoundManager>();

        Debug.Log("젠가 매니저들 Awake 완료");
    }

    protected override IEnumerator InitializeSequentialManagers()
    {
        // 순차적으로 초기화해야 할 매니저들
        var sequentialComponents = new IGameComponent[]
        {
          //  JengaNetworkManager.Instance,     // 네트워크 먼저
            JengaGameManager.Instance,        // 게임 로직
          //  JengaTowerManager.Instance,       // 타워 생성
        };

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));
    }

    protected override IEnumerator InitializeParallelManagers()
    {
        // 병렬로 초기화해도 되는 매니저들
        var parallelComponents = new ICoroutineGameComponent[]
        {
          //  JengaUIManager.Instance,          // UI 준비
          //  JengaTimingManager.Instance,      // 타이밍 시스템 준비
          //  JengaSoundManager.Instance        // 사운드 준비
        };

        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
    }

    protected override void NotifyGameStart()
    {
        // 실제 젠가 게임 시작
        JengaGameManager.Instance?.StartGame();
      //  JengaUIManager.Instance?.ShowGameStartUI();
      //  JengaSoundManager.Instance?.PlayGameStartSound();
    }
}