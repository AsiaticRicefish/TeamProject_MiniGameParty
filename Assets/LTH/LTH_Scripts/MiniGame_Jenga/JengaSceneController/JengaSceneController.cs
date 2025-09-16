using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using InputBlocker;
using LDH_MainGame;
using LDH_UI;
using Managers;
using MiniGameJenga;
using Photon.Pun;
using UnityEngine;
using Cysharp.Threading.Tasks;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class JengaSceneController : BaseGameSceneController
{
    protected override string GameType => "Jenga";
    private static JengaSceneController _only;

    private bool _startNotified;

    private const string ROOMKEY_SLOTS = "JG_SLOTS";

    [Header("Loading Theme")]
    [SerializeField] private UI_LoadingTheme jengaLoadingTheme; // 젠가 테마

    private UI_Loading _uiLoading;

    private void Awake()
    {
        if (_only && _only != this)
        {
            Destroy(gameObject);
            return;
        }
        _only = this;

        // 로딩창 생성 및 테마 적용
        _uiLoading = Manager.UI.CreatePopupUI<UI_Loading>();
        if (jengaLoadingTheme)
        {
            _uiLoading.ApplyTheme(jengaLoadingTheme);
        }
        else
        {
            // 기본 텍스트라도 설정
            _uiLoading.SetTitle("JENGA GAME");
            _uiLoading.SetBigDescription("준비 중...");
            _uiLoading.SetAllPanelColors(Color.black, Color.gray, Color.white);
        }

        Manager.UI.ShowPopupUI(_uiLoading).Forget();
    }

    protected override IEnumerator WaitForManagersAwake()
    {
        EnsureInputManagerForScene();

        // 초기 진행률 설정
        if (_uiLoading) _uiLoading.SetProgress(0.1f);

        // 모든 플레이어가 uid 셋팅될 때까지 잠깐 대기
        yield return WaitForAllPlayerUids(5f);
        if (_uiLoading) _uiLoading.SetProgress(0.3f);

        // 슬롯맵이 준비될 때까지 잠깐 대기
        yield return WaitForSlotMapReady(5f);
        if (_uiLoading) _uiLoading.SetProgress(0.5f);

        // 각 매니저들이 Awake에서 생성되기를 기다림
        yield return WaitForSingletonReady<JengaGameManager>();
        yield return WaitForSingletonReady<JengaNetworkManager>();
        yield return WaitForSingletonReady<JengaTowerManager>();

        if (_uiLoading) _uiLoading.SetProgress(0.7f);
        Debug.Log("젠가 매니저들 Awake 완료");
    }

    protected override IEnumerator InitializeSequentialManagers()
    {
        // 순차적으로 초기화해야 할 매니저들
        var sequentialComponents = new IGameComponent[]
        {
            InputManager.Instance,          // 입력 시스템 먼저
            JengaNetworkManager.Instance,     // 네트워크 먼저
            JengaGameManager.Instance,        // 게임 로직
            JengaTowerManager.Instance,       // 타워 생성
            JengaUIManager.Instance,          // UI 매니저
        };

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));
        if (_uiLoading) _uiLoading.SetProgress(0.85f);
    }

    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("[Scene] InitializeParallelManagers START");

        // 병렬로 초기화해도 되는 매니저들
        var parallelComponents = new ICoroutineGameComponent[]
        {
           JengaTimingManager.Instance      // 타이밍 시스템 준비
        };

        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
        if (_uiLoading) _uiLoading.SetProgress(0.95f);

        // 페일세이프: 여기서 한 번 더 직접 시작 호출
        if (!_startNotified)
        {
            Debug.Log("[Scene] Failsafe start after parallel init - calling NotifyGameStart()");
            NotifyGameStart();
        }
        Debug.Log("[Scene] InitializeParallelManagers END");
    }

    protected override void NotifyGameStart()
    {
        Debug.Log($"=== [Scene] NotifyGameStart START ===");

        if (_startNotified)
        {
            Debug.Log("[Scene] NotifyGameStart skipped (already started)");
            return;
        }
        _startNotified = true;

        try
        {
            // 완료 진행률 설정
            if (_uiLoading)
            {
                _uiLoading.SetProgress(1.0f);
                _uiLoading.SetBigDescription("READY!");
            }

            // 로딩창 닫기
            if (_uiLoading)
            {
                Manager.UI.ClosePopupUI(_uiLoading).Forget();
                _uiLoading = null;
            }

            if (!Camera.main)
                Debug.LogWarning("[Scene] MainCamera가 아직 준비되지 않았습니다.");

            if (PhotonNetwork.IsMasterClient)
            {
                JengaGameManager.Instance.StartGame();
                MainGameManager.Instance?.NotifyMiniGameStart();
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

        Debug.Log($"=== [Scene] NotifyGameStart END ===");
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

    private void EnsureInputManagerForScene()
    {
        if (InputManager.Instance == null)
        {
            var go = new GameObject("@InputManager_Jenga");
            go.AddComponent<InputManager>();
        }
        // 씬 진입 시 잠금 초기화(안전장치)
        InputManager.Instance.ResetAllLocks();
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