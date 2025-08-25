using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using MiniGameJenga;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class JengaSceneController : BaseGameSceneController
{
    protected override string GameType => "Jenga";
    private static JengaSceneController _only;

    private bool _startNotified;

    [Header("UI")]
    [SerializeField] private LoadingOverlay loading;

    private void Awake()
    {
        if (_only && _only != this)
        {
            Debug.LogWarning("[Jenga] Duplicate JengaSceneController destroyed.");
            Destroy(gameObject);
            return;
        }
        _only = this;

        if (!loading) loading = FindObjectOfType<LoadingOverlay>(true);
        loading?.Show("초기화 준비 중...", 0.05f);
    }

    protected override IEnumerator WaitForManagersAwake()
    {
        // 모든 플레이어가 uid 셋팅될 때까지 잠깐 대기
        loading?.Set("플레이어 동기화 확인 중...", 0.10f);
        yield return WaitForAllPlayerUids(5f);

        // 각 매니저들이 Awake에서 생성되기를 기다림
        loading?.Set("매니저 준비 중...", 0.20f);
        yield return WaitForSingletonReady<JengaGameManager>();
        loading?.Set(progress: 0.30f);
        yield return WaitForSingletonReady<JengaNetworkManager>();
        loading?.Set(progress: 0.40f);
        yield return WaitForSingletonReady<JengaTowerManager>();
        loading?.Set(progress: 0.50f);

        Debug.Log("젠가 매니저들 Awake 완료");
    }

    protected override IEnumerator InitializeSequentialManagers()
    {
        // 순차 초기화
        loading?.Set("핵심 시스템 초기화 (1/2)...", 0.55f);

        // 순차적으로 초기화해야 할 매니저들
        var sequentialComponents = new IGameComponent[]
        {
            JengaNetworkManager.Instance,     // 네트워크 먼저
            JengaGameManager.Instance,        // 게임 로직
            JengaTowerManager.Instance,       // 타워 생성
        };

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));

        loading?.Set(progress: 0.80f);
    }

    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("[Scene] InitializeParallelManagers START");

        // 병렬 초기화
        loading?.Set("보조 시스템 초기화 (2/2)...", 0.85f);

        // 병렬로 초기화해도 되는 매니저들
        var parallelComponents = new ICoroutineGameComponent[]
        {
           JengaTimingManager.Instance      // 타이밍 시스템 준비
        };

        Debug.Log("[Scene] About to call InitializeCoroutineComponentsSafely");
        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
        Debug.Log("[Scene] InitializeCoroutineComponentsSafely returned - proceeding to step 6");

        loading?.Set(progress: 0.95f);
        Debug.Log($"[Scene] Before failsafe check - _startNotified: {_startNotified}");

        // 페일세이프: 여기서 한 번 더 직접 시작 호출
        if (!_startNotified)
        {
            Debug.Log("[Scene] Failsafe start after parallel init - calling NotifyGameStart()");
            NotifyGameStart();
        }
        else
        {
            Debug.Log("[Scene] Failsafe skipped - already started");
        }

        Debug.Log("[Scene] InitializeParallelManagers END");
    }

    protected override void NotifyGameStart()
    {
        Debug.Log($"=== [Scene] NotifyGameStart START ===");

        if (_startNotified)
        {
            Debug.Log("[Scene] NotifyGameStart skipped (already started)");
            loading?.Hide();
            return;
        }
        _startNotified = true;

        Debug.Log($"[Scene] PhotonNetwork.IsMasterClient = {PhotonNetwork.IsMasterClient}");
        Debug.Log($"[Scene] JengaGameManager.Instance = {JengaGameManager.Instance != null}");

        try
        {
            loading?.Set("시작 준비 완료!", 1.0f);

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"[Scene] About to call JengaGameManager.Instance.StartGame()");
                JengaGameManager.Instance.StartGame();
            }
            else
            {
                Debug.Log("[Scene] Non-master: waiting for state broadcast...");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NotifyGameStart] Exception: {ex}\n{ex.StackTrace}");
        }
        finally
        {
            loading?.Hide();
        }

        Debug.Log($"=== [Scene] NotifyGameStart END ===");
    }


    private IEnumerator ForceStartAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        Debug.Log("[FORCE START] Attempting to start game...");

        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.StartGame();
        }
        else
        {
            Debug.LogError("[FORCE START] JengaGameManager.Instance is null!");
        }
    }

    // --- 유틸: 모든 플레이어가 uid 세팅될 때까지 대기 ---
    private IEnumerator WaitForAllPlayerUids(float timeoutSec = 5f)
    {
        float end = Time.time + timeoutSec;
        while (Time.time < end)
        {
            var list = PhotonNetwork.PlayerList;
            bool allHaveUid = list != null && list.Length > 0 && list.All(p =>
                p.CustomProperties != null &&
                p.CustomProperties.TryGetValue("uid", out var v) &&
                v is string s && !string.IsNullOrEmpty(s));

            if (allHaveUid) yield break;
            yield return new WaitForSeconds(0.1f);
        }
        Debug.LogWarning("[Init] Not all players have UID. Continue anyway.");
    }
}