using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using MiniGameJenga;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class JengaSceneController : BaseGameSceneController
{
    protected override string GameType => "Jenga";
    private static JengaSceneController _only;

    private bool _startNotified;

    [Header("UI")]
    [SerializeField] private LoadingOverlay loading;

    private const string ROOMKEY_SLOTS = "JG_SLOTS";

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

        // 슬롯맵이 준비될 때까지 잠깐 대기
        loading?.Set("슬롯 맵 동기화 중...", 0.15f);
        yield return WaitForSlotMapReady(5f);

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
            JengaUIManager.Instance,          // UI 매니저
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

            if (!Camera.main)
                Debug.LogWarning("[Scene] MainCamera가 아직 준비되지 않았습니다.");

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

    private void OnDestroy()
    {
        // 씬 전환 전 젠가 관련 매니저들 명시적 해제
        if (JengaGameManager.Instance != null)
        {
            CombinedSingleton<JengaGameManager>.Release();
        }
        
        if (JengaTowerManager.Instance != null)
        {
            CombinedSingleton<JengaTowerManager>.Release();
        }
        
        if (JengaNetworkManager.Instance != null)
        {
            PunSingleton<JengaNetworkManager>.Release();
        }
        
        if (JengaTimingManager.Instance != null)
        {
            CombinedSingleton<JengaTimingManager>.Release();
        }
        
        if (JengaUIManager.Instance != null)
        {
            CombinedSingleton<JengaUIManager>.Release();
        }
    }

    /// <summary>
    ///  네트워크 초기화 타이밍이 꼬여서 NotifyGameStart()가 끝까지 안 불릴 때
    ///  JengaGameManager.Instance 초기화가 지연되면서 게임 시작 신호가 안 갈 때
    ///  그럴 경우를 대비해서 3초 후 강제 StartGame()을 실행
    /// </summary>
    /// <returns></returns>
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

    #region 유틸리티: 매니저 준비 대기
    // 모든 플레이어가 uid 세팅될 때까지 대기
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

    // 슬롯맵이 준비될 때까지 잠깐 대기
    private IEnumerator WaitForSlotMapReady(float timeoutSec = 5f)
    {
        float end = Time.time + timeoutSec;
        while (Time.time < end)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room != null && room.CustomProperties != null &&
                room.CustomProperties.TryGetValue(ROOMKEY_SLOTS, out var obj))
            {
                // 슬롯맵 길이가 현재 PlayerList와 합리적으로 일치할 때 OK
                int[] slots = obj is int[] a ? a
                               : obj is object[] o ? o.Select(x => Convert.ToInt32(x)).ToArray()
                               : Array.Empty<int>();

                var actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(x => x).ToArray();
                var slotsSorted = slots.OrderBy(x => x).ToArray();

                if (slots.Length > 0 && actors.SequenceEqual(slotsSorted))
                    yield break; // 준비 완료
            }
            yield return new WaitForSeconds(0.05f);
        }
        Debug.LogWarning("[Init] JG_SLOTS not fully ready. Continue anyway.");
    }
    #endregion
}