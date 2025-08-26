using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public abstract class BaseGameSceneController : MonoBehaviourPun
{
    [Header("초기화 설정")]
    [SerializeField] protected float initTimeout = 30f; // WaitForAllPlayersLoaded()에서 사용하는 안전장치
    [SerializeField] protected bool showDebugLogs = true;

    [Header("동기화 설정")]
    [SerializeField] protected float syncCheckInterval = 0.1f;

    // 하위 클래스에서 구현해야 할 추상 속성/메서드들
    protected abstract string GameType { get; }
    protected abstract IEnumerator WaitForManagersAwake();
    protected abstract IEnumerator InitializeSequentialManagers();
    protected abstract IEnumerator InitializeParallelManagers();

    // 게임 시작 시 호출됨. UI 표시, 플레이어 활성화 등 실제 게임 시작 준비를 여기서 수행
    protected abstract void NotifyGameStart();

    // 동기화용 변수
    private HashSet<int> loadedPlayers = new();
    private HashSet<int> initializedPlayers = new();
    private bool isInitializing = false;

    private void OnEnable()
    {
        loadedPlayers.Clear();
        initializedPlayers.Clear();
    }

    private void Start()
    {
        Debug.Log("[JengaScene] Start() called - about to call SafeInitialize()");
        StartCoroutine(Co_StartWhenInRoom());
    }

    private IEnumerator Co_StartWhenInRoom()
    {
        // 연결 & 룸입장까지 대기
        yield return new WaitUntil(() => PhotonNetwork.IsConnected && PhotonNetwork.InRoom);

        // PhotonView 준비 여부 체크 (Scene에 미리 있는 뷰라면 보통 자동 할당되지만, 0이면 RPC 금지)
        if (photonView == null || photonView.ViewID == 0)
        {
#if PHOTON_UNITY_NETWORKING_2_OR_NEWER
        if (!PhotonNetwork.AllocateViewID(photonView))
        {
            Debug.LogError("[Jenga] PhotonView ViewID=0 → AllocateViewID 실패. 씬 배치 또는 네트워크 인스턴스로 생성해야 합니다.");
            yield break;
        }
#else
            int id = PhotonNetwork.AllocateViewID(PhotonNetwork.LocalPlayer.ActorNumber);
            photonView.ViewID = id;
            PhotonNetwork.RegisterPhotonView(photonView);
#endif
        }

        StartCoroutine(SafeInitialize());
    }

    private void SendRPCSafely(string methodName, params object[] parameters)
    {
        try
        {
            photonView.RPC(methodName, RpcTarget.All, parameters);
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GameType}Controller] RPC 호출 실패: {methodName} → {e.Message}");
        }
    }

    // 전체 초기화 과정을 관리
    private IEnumerator SafeInitialize()
    {
        if (isInitializing) yield break;
        isInitializing = true;

        Debug.Log($"[{GameType}] === SafeInitialize START ===");

        // 1단계: 내가 씬 로딩 완료했다고 알림
        Debug.Log($"[{GameType}] Step 1: Sending OnPlayerSceneLoaded");
        SendRPCSafely(nameof(OnPlayerSceneLoaded), PhotonNetwork.LocalPlayer.ActorNumber);

        // 2단계: 모든 플레이어 씬 로딩 완료 대기
        Debug.Log($"[{GameType}] Step 2: WaitForAllPlayersLoaded");
        yield return StartCoroutine(WaitForAllPlayersLoaded());

        // 3단계: 매니저들 Awake 완료 대기
        Debug.Log($"[{GameType}] Step 3: WaitForManagersAwake");
        yield return StartCoroutine(WaitForManagersAwake());

        // 4단계: 순차 초기화 (의존성 있는 것들)
        Debug.Log($"[{GameType}] Step 4: InitializeSequentialManagers");
        yield return StartCoroutine(InitializeSequentialManagers());

        // 5단계: 병렬 초기화 (독립적인 것들)
        Debug.Log($"[{GameType}] Step 5: InitializeParallelManagers");
        yield return StartCoroutine(InitializeParallelManagers());

        // 6단계: 내가 초기화 완료했다고 알림
        Debug.Log($"[{GameType}] Step 6: Sending OnPlayerInitialized");
        SendRPCSafely(nameof(OnPlayerInitialized), PhotonNetwork.LocalPlayer.ActorNumber);

        // 7단계: 모든 플레이어 초기화 완료 대기
        Debug.Log($"[{GameType}] Step 7: WaitForAllPlayersInitialized - WAITING...");
        yield return StartCoroutine(WaitForAllPlayersInitialized());
        Debug.Log($"[{GameType}] Step 7: WaitForAllPlayersInitialized - COMPLETED");

        // 8단계: 게임 시작
        Debug.Log($"[{GameType}] Step 8: Game Start (isMaster: {PhotonNetwork.IsMasterClient})");
        if (PhotonNetwork.IsMasterClient)
        {
            SendRPCSafely(nameof(StartGame));
        }

        Debug.Log($"[{GameType}] === SafeInitialize END ===");
        isInitializing = false;
    }

    #region 플레이어 동기화 RPC
    // 각 플레이어가 씬 로딩 완료했음을 모든 플레이어에게 알림
    // 호출 시점: SafeInitialize() 1단계에서 자동 호출
    // loadedPlayers 집합에 플레이어 ID 추가
    [PunRPC]
    public void OnPlayerSceneLoaded(int playerId)
    {
        loadedPlayers.Add(playerId);
        Debug.Log($"플레이어 {playerId} 로딩 완료 ({loadedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
    }

    // 각 플레이어가 매니저 초기화 완료했음을 알림
    // 호출 시점: 순차/병렬 초기화 완료 후
    // InitializedPlayers 집합에 플레이어 ID 추가
    [PunRPC]
    public void OnPlayerInitialized(int playerId)
    {
        initializedPlayers.Add(playerId);
        Debug.Log($"플레이어 {playerId} 초기화 완료 ({initializedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
    }

    // 모든 플레이어가 준비되면 실제 게임 시작 신호
    // 호출자: 마스터 클라이언트
    // NotifyGameStart() 호출하여 각 매니저들에게 게임 시작 알림
    [PunRPC]
    public void StartGame()
    {
        NotifyGameStart();
    }
    #endregion

    #region 대기 로직
    private IEnumerator WaitForAllPlayersLoaded() // 모든 플레이어가 씬 로딩 완료할 때까지 대기
    {
        Debug.Log("모든 플레이어 로딩 대기 중...");

        while (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            yield return null;

        float timer = 0f;
        while (loadedPlayers.Count < PhotonNetwork.CurrentRoom.PlayerCount)
        {
            timer += syncCheckInterval;
            if (timer > initTimeout)
            {
                Debug.LogError($"[{GameType}Controller] 플레이어 로딩 타임아웃!");
                yield break;
            }
            yield return new WaitForSeconds(syncCheckInterval);
        }
        Debug.Log("모든 플레이어 로딩 완료");
    }

    private IEnumerator WaitForAllPlayersInitialized() // 모든 플레이어가 매니저 초기화 완료할 때까지 대기
    {
        Debug.Log("모든 플레이어 초기화 대기 중...");

        float timer = 0f;
        while (initializedPlayers.Count < PhotonNetwork.CurrentRoom.PlayerCount)
        {
            timer += syncCheckInterval;
            if (timer > initTimeout)
            {
                Debug.LogError($"[{GameType}Controller] 플레이어 초기화 타임아웃!");
                yield break;
            }
            yield return new WaitForSeconds(syncCheckInterval);
        }

        Debug.Log("모든 플레이어 초기화 완료");
    }
    #endregion

    #region 유틸리티
    // 특정 매니저(싱글톤)이 생성될 때까지 대기   
    protected IEnumerator WaitForSingletonReady<T>() where T : MonoBehaviour
    {
        yield return new WaitUntil(() => FindObjectOfType<T>() != null);
    }
    
    // 타입을 매개변수로 받는 오버로드 메서드 추가
    protected IEnumerator WaitForSingletonReady(Type type)
    {
        yield return new WaitUntil(() => FindObjectOfType(type) != null);
    }


    // IGameComponent들을 순차적으로 안전하게 초기화
    protected IEnumerator InitializeComponentsSafely(IEnumerable<IGameComponent> components)
    {
        foreach (var component in components)
        {
            bool failed = false;

            try
            {
                component.Initialize();
            }
            catch (System.Exception e)
            {
                failed = true;
                Debug.LogError($"[{GameType}Controller] 초기화 실패 {component.GetType().Name}: {e.Message}");
            }

            yield return null;

            if (!failed)
            {
                Debug.Log($"순차 초기화 완료: {component.GetType().Name}");
            }
        }
    }

    // ICoroutineGameComponent들을 병렬로 안전하게 초기화
    protected IEnumerator InitializeCoroutineComponentsSafely(IEnumerable<ICoroutineGameComponent> components)
    {
        var componentList = components.Where(c => c != null).ToList();
        if (componentList.Count == 0)
        {
            Debug.Log("병렬 초기화할 컴포넌트가 없습니다.");
            yield break;
        }

        Debug.Log($"병렬 초기화 시작: {componentList.Count}개 컴포넌트");

        var completionTracker = new Dictionary<string, bool>();

        foreach (var component in componentList)
        {
            var componentName = component.GetType().Name;
            completionTracker[componentName] = false;
            StartCoroutine(InitializeCoroutineComponentSafelyWithTracker(component, componentName, completionTracker));
        }

        // 모든 컴포넌트 완료까지 대기
        while (completionTracker.Values.Any(completed => !completed))
        {
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("모든 병렬 초기화 완료");
    }

    private IEnumerator InitializeCoroutineComponentSafelyWithTracker(ICoroutineGameComponent component, string componentName, Dictionary<string, bool> tracker)
    {
        IEnumerator routine = null;

        try
        {
            routine = component.InitializeCoroutine();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{GameType}Controller] 병렬 초기화 준비 실패 {componentName}: {e.Message}");
            tracker[componentName] = true; // 실패해도 완료로 처리
            yield break;
        }

        bool errorOccurred = false;
        System.Exception error = null;

        while (true)
        {
            object current = null;
            try
            {
                if (!routine.MoveNext()) break;
                current = routine.Current;
            }
            catch (System.Exception e)
            {
                error = e;
                errorOccurred = true;
                break;
            }
            yield return current;
        }

        if (errorOccurred)
        {
            Debug.LogError($"[{GameType}Controller] 병렬 초기화 실패 {componentName}: {error.Message}");
        }
        else
        {
            Debug.Log($"병렬 초기화 완료: {componentName}");
        }

        tracker[componentName] = true; // 성공/실패 관계없이 완료 표시
    }
    #endregion
}