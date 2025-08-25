using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public abstract class BaseGameSceneController : MonoBehaviourPun
{
    [Header("�ʱ�ȭ ����")]
    [SerializeField] protected float initTimeout = 30f; // WaitForAllPlayersLoaded()���� ����ϴ� ������ġ
    [SerializeField] protected bool showDebugLogs = true;

    [Header("����ȭ ����")]
    [SerializeField] protected float syncCheckInterval = 0.1f;

    // ���� Ŭ�������� �����ؾ� �� �߻� �Ӽ�/�޼����
    protected abstract string GameType { get; }
    protected abstract IEnumerator WaitForManagersAwake();
    protected abstract IEnumerator InitializeSequentialManagers();
    protected abstract IEnumerator InitializeParallelManagers();

    // ���� ���� �� ȣ���. UI ǥ��, �÷��̾� Ȱ��ȭ �� ���� ���� ���� �غ� ���⼭ ����
    protected abstract void NotifyGameStart();

    // ����ȭ�� ����
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
        // ���� & ��������� ���
        yield return new WaitUntil(() => PhotonNetwork.IsConnected && PhotonNetwork.InRoom);

        // PhotonView �غ� ���� üũ (Scene�� �̸� �ִ� ���� ���� �ڵ� �Ҵ������, 0�̸� RPC ����)
        if (photonView == null || photonView.ViewID == 0)
        {
#if PHOTON_UNITY_NETWORKING_2_OR_NEWER
        if (!PhotonNetwork.AllocateViewID(photonView))
        {
            Debug.LogError("[Jenga] PhotonView ViewID=0 �� AllocateViewID ����. �� ��ġ �Ǵ� ��Ʈ��ũ �ν��Ͻ��� �����ؾ� �մϴ�.");
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
            Debug.LogError($"[{GameType}Controller] RPC ȣ�� ����: {methodName} �� {e.Message}");
        }
    }

    // ��ü �ʱ�ȭ ������ ����
    private IEnumerator SafeInitialize()
    {
        if (isInitializing) yield break;
        isInitializing = true;

        Debug.Log($"[{GameType}] === SafeInitialize START ===");

        // 1�ܰ�: ���� �� �ε� �Ϸ��ߴٰ� �˸�
        Debug.Log($"[{GameType}] Step 1: Sending OnPlayerSceneLoaded");
        SendRPCSafely(nameof(OnPlayerSceneLoaded), PhotonNetwork.LocalPlayer.ActorNumber);

        // 2�ܰ�: ��� �÷��̾� �� �ε� �Ϸ� ���
        Debug.Log($"[{GameType}] Step 2: WaitForAllPlayersLoaded");
        yield return StartCoroutine(WaitForAllPlayersLoaded());

        // 3�ܰ�: �Ŵ����� Awake �Ϸ� ���
        Debug.Log($"[{GameType}] Step 3: WaitForManagersAwake");
        yield return StartCoroutine(WaitForManagersAwake());

        // 4�ܰ�: ���� �ʱ�ȭ (������ �ִ� �͵�)
        Debug.Log($"[{GameType}] Step 4: InitializeSequentialManagers");
        yield return StartCoroutine(InitializeSequentialManagers());

        // 5�ܰ�: ���� �ʱ�ȭ (�������� �͵�)
        Debug.Log($"[{GameType}] Step 5: InitializeParallelManagers");
        yield return StartCoroutine(InitializeParallelManagers());

        // 6�ܰ�: ���� �ʱ�ȭ �Ϸ��ߴٰ� �˸�
        Debug.Log($"[{GameType}] Step 6: Sending OnPlayerInitialized");
        SendRPCSafely(nameof(OnPlayerInitialized), PhotonNetwork.LocalPlayer.ActorNumber);

        // 7�ܰ�: ��� �÷��̾� �ʱ�ȭ �Ϸ� ���
        Debug.Log($"[{GameType}] Step 7: WaitForAllPlayersInitialized - WAITING...");
        yield return StartCoroutine(WaitForAllPlayersInitialized());
        Debug.Log($"[{GameType}] Step 7: WaitForAllPlayersInitialized - COMPLETED");

        // 8�ܰ�: ���� ����
        Debug.Log($"[{GameType}] Step 8: Game Start (isMaster: {PhotonNetwork.IsMasterClient})");
        if (PhotonNetwork.IsMasterClient)
        {
            SendRPCSafely(nameof(StartGame));
        }

        Debug.Log($"[{GameType}] === SafeInitialize END ===");
        isInitializing = false;
    }

    #region �÷��̾� ����ȭ RPC
    // �� �÷��̾ �� �ε� �Ϸ������� ��� �÷��̾�� �˸�
    // ȣ�� ����: SafeInitialize() 1�ܰ迡�� �ڵ� ȣ��
    // loadedPlayers ���տ� �÷��̾� ID �߰�
    [PunRPC]
    public void OnPlayerSceneLoaded(int playerId)
    {
        loadedPlayers.Add(playerId);
        Debug.Log($"�÷��̾� {playerId} �ε� �Ϸ� ({loadedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
    }

    // �� �÷��̾ �Ŵ��� �ʱ�ȭ �Ϸ������� �˸�
    // ȣ�� ����: ����/���� �ʱ�ȭ �Ϸ� ��
    // InitializedPlayers ���տ� �÷��̾� ID �߰�
    [PunRPC]
    public void OnPlayerInitialized(int playerId)
    {
        initializedPlayers.Add(playerId);
        Debug.Log($"�÷��̾� {playerId} �ʱ�ȭ �Ϸ� ({initializedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
    }

    // ��� �÷��̾ �غ�Ǹ� ���� ���� ���� ��ȣ
    // ȣ����: ������ Ŭ���̾�Ʈ
    // NotifyGameStart() ȣ���Ͽ� �� �Ŵ����鿡�� ���� ���� �˸�
    [PunRPC]
    public void StartGame()
    {
        NotifyGameStart();
    }
    #endregion

    #region ��� ����
    private IEnumerator WaitForAllPlayersLoaded() // ��� �÷��̾ �� �ε� �Ϸ��� ������ ���
    {
        Debug.Log("��� �÷��̾� �ε� ��� ��...");

        while (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            yield return null;

        float timer = 0f;
        while (loadedPlayers.Count < PhotonNetwork.CurrentRoom.PlayerCount)
        {
            timer += syncCheckInterval;
            if (timer > initTimeout)
            {
                Debug.LogError($"[{GameType}Controller] �÷��̾� �ε� Ÿ�Ӿƿ�!");
                yield break;
            }
            yield return new WaitForSeconds(syncCheckInterval);
        }
        Debug.Log("��� �÷��̾� �ε� �Ϸ�");
    }

    private IEnumerator WaitForAllPlayersInitialized() // ��� �÷��̾ �Ŵ��� �ʱ�ȭ �Ϸ��� ������ ���
    {
        Debug.Log("��� �÷��̾� �ʱ�ȭ ��� ��...");

        float timer = 0f;
        while (initializedPlayers.Count < PhotonNetwork.CurrentRoom.PlayerCount)
        {
            timer += syncCheckInterval;
            if (timer > initTimeout)
            {
                Debug.LogError($"[{GameType}Controller] �÷��̾� �ʱ�ȭ Ÿ�Ӿƿ�!");
                yield break;
            }
            yield return new WaitForSeconds(syncCheckInterval);
        }

        Debug.Log("��� �÷��̾� �ʱ�ȭ �Ϸ�");
    }
    #endregion

    #region ��ƿ��Ƽ
    // Ư�� �Ŵ���(�̱���)�� ������ ������ ���   
    protected IEnumerator WaitForSingletonReady<T>() where T : MonoBehaviour
    {
        yield return new WaitUntil(() => FindObjectOfType<T>() != null);
    }

    // IGameComponent���� ���������� �����ϰ� �ʱ�ȭ
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
                Debug.LogError($"[{GameType}Controller] �ʱ�ȭ ���� {component.GetType().Name}: {e.Message}");
            }

            yield return null;

            if (!failed)
            {
                Debug.Log($"���� �ʱ�ȭ �Ϸ�: {component.GetType().Name}");
            }
        }
    }

    // ICoroutineGameComponent���� ���ķ� �����ϰ� �ʱ�ȭ
    protected IEnumerator InitializeCoroutineComponentsSafely(IEnumerable<ICoroutineGameComponent> components)
    {
        var componentList = components.Where(c => c != null).ToList();
        if (componentList.Count == 0)
        {
            Debug.Log("���� �ʱ�ȭ�� ������Ʈ�� �����ϴ�.");
            yield break;
        }

        Debug.Log($"���� �ʱ�ȭ ����: {componentList.Count}�� ������Ʈ");

        var completionTracker = new Dictionary<string, bool>();

        foreach (var component in componentList)
        {
            var componentName = component.GetType().Name;
            completionTracker[componentName] = false;
            StartCoroutine(InitializeCoroutineComponentSafelyWithTracker(component, componentName, completionTracker));
        }

        // ��� ������Ʈ �Ϸ���� ���
        while (completionTracker.Values.Any(completed => !completed))
        {
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("��� ���� �ʱ�ȭ �Ϸ�");
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
            Debug.LogError($"[{GameType}Controller] ���� �ʱ�ȭ �غ� ���� {componentName}: {e.Message}");
            tracker[componentName] = true; // �����ص� �Ϸ�� ó��
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
            Debug.LogError($"[{GameType}Controller] ���� �ʱ�ȭ ���� {componentName}: {error.Message}");
        }
        else
        {
            Debug.Log($"���� �ʱ�ȭ �Ϸ�: {componentName}");
        }

        tracker[componentName] = true; // ����/���� ������� �Ϸ� ǥ��
    }
    #endregion
}