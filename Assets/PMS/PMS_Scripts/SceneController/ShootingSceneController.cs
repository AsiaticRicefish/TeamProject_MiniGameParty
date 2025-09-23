using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using LDH_MainGame;
using Photon.Pun;
using ShootingScene;
using ShootingScene.ShootingGame;
using LDH_UI;
using Managers;
using PMS_Util;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class ShootingSceneController : BaseGameSceneController
{
    [Header("Loading Theme")]
    [SerializeField] private UI_LoadingTheme ShootingLoadingTheme; // 테마

    private UI_Loading _uiLoading; // 꼭 추가!

    [SerializeField] private GameObject[] iGameComponents;

    //따로 이벤트는 없는거 같음
    public Action OnGameStarted;

    protected override string GameType => "Shooting";

    protected override void Awake()
    {
        // 1. 로딩창 생성
        _uiLoading = Manager.UI.CreatePopupUI<UI_Loading>();

        // 2. 테마 적용 (있는 경우)
        if (ShootingLoadingTheme)
        {
            _uiLoading.ApplyTheme(ShootingLoadingTheme);
        }
        // 3. 테마 없을 때 -> 적용안함.

        // 4. 로딩창 표시
        Manager.UI.ShowPopupUI(_uiLoading).Forget();
    }
    protected override IEnumerator WaitForManagersAwake()
    {
        _uiLoading?.SetProgress(0.1f);  // 초기 설정 완료
        yield return WaitForSingletonReady<ShootingNetworkManager>();
        yield return WaitForSingletonReady<ShootingGameManager>();
        yield return WaitForSingletonReady<RoomPropertyObserver>();
        yield return WaitForSingletonReady<PlayerInputManager>();
        _uiLoading?.SetProgress(0.3f);  // 매니저 중간 초기화 완료
        yield return WaitForSingletonReady<TurnManager>();
        yield return WaitForSingletonReady<CardManager>();
        yield return WaitForSingletonReady<ShootingCameraManager>();
        yield return WaitForSingletonReady<EggManager>();
        yield return WaitForSingletonReady<ShootingUIManager>();
        yield return WaitForSingletonReady<WindSystem>();
        Debug.Log("모든 ShootingGameScene 매니저 Awake완료");
    }

    //순차 초기화
    protected override IEnumerator InitializeSequentialManagers()
    {
        Debug.Log("ShootingGameScene 순차 초기화 시작");

        //-- 민성님 아래 오브젝트들을 인스펙터 창에 차례로 넣어주시고 마지막에 camera swipe controller를 넣어주세요
        // var sequentialComponents = new IGameComponent[]
        // {
        //     RoomPropertyObserver.Instance,
        //     ShootingNetworkManager.Instance,
        //     ShootingGameManager.Instance,
        //     PlayerInputManager.Instance,
        //     TurnManager.Instance,
        //     EggManager.Instance,
        //     ShootingUIManager.Instance,
        //     WindSystem.Instance,
        // };
        _uiLoading?.SetProgress(0.5f);  // 매니저 생성 완료 및 순차 초기화 시작
        var seqHashSet = new HashSet<object>();
        List<IGameComponent> sequentialComponents = new();

        foreach (var go in iGameComponents)
        {
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb is IGameComponent component && seqHashSet.Add(component))
                {
                    Debug.Log($"{go.name} is IGameComponent ");
                    sequentialComponents.Add(component);
                }
            }

        }
        _uiLoading?.SetProgress(0.85f); // 순차 초기화 완료

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));

        #region DI 주입
        // CameraSwipeController DI
        //var swipeController = FindObjectOfType<CameraSwipeController>();
        //if (swipeController != null)
        //{
        //    swipeController.Initialize(PlayerInputManager.Instance);
        //    Debug.Log("CameraSwipeController Initialize 완료");
        //}

        //FindObjectOfType <- 효율 씬 
        #endregion
    }

    //벙렬 초기화
    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("ShootingGameScene 병렬 초기화 시작");

        var parallelComponents = new List<ICoroutineGameComponent>();

        //parallelComponents.Add()
        _uiLoading?.SetProgress(0.95f); // 병렬 초기화 완료

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
            // 완료 표시
            if (_uiLoading)
            {
                _uiLoading.SetProgress(1.0f);
            }

            // 로딩창 닫기
            if (_uiLoading)
            {
                Manager.UI.ClosePopupUI(_uiLoading).Forget();
                _uiLoading = null;
            }

            //BGM 스타트
            SoundManager.Instance.PlayBGM(Define_PMS.SoundKeys.ShootingBGM);

            //모든 Scene Controller의 작업 처리 완료를 알림
            //TaskSyncManager.Instance.SetTaskDone(ShootingGamePlayerPropertyKeys.TaskType.Initialized);

            if (PhotonNetwork.IsMasterClient)
            {
                MainGameManager.Instance?.NotifyMiniGameStart();
                RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "CardSelectState");   
            }

            //else if(RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.State).ToString() == "CardSelectState")
            //{
            //     Debug.Log("호출?");
            //     //이미 변경되어 룸프로퍼티가 callback을 못받았을 때
            //     //지연보상
            //     //늦게 들어와서 따로 RoomCallBack 못받은 상황에서는 자신의 State 변경 요청해야한다. 클라이언트 -> 마스터 클라이언트
            //     string state = (string)PhotonNetwork.CurrentRoom.CustomProperties[ShootingGamePropertyKeys.State];
            //     ShootingGameManager.Instance.ChangeStateByName("CardSelectState");//(state);
            //}
            //나머지 클라이어트도 룸프로퍼티 변경으로 인한 콜백함수로 ChangeState 실행되겠지?
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NotifyGameStart] {ex}\n{ex.StackTrace}");
        }
    }
}
