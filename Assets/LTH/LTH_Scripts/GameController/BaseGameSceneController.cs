using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

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
        if (PhotonNetwork.IsConnected)
        {
            StartCoroutine(SafeInitialize());
        }
        else
        {
            Debug.LogError($"[{GameType}Controller] Photon ������� ����!");
        }
    }

    private void SendRPCSafely(string methodName, params object[] parameters)
    {
        try
        {
            photonView.RPC(methodName, RpcTarget.All, parameters);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{GameType}Controller] RPC ȣ�� ����: {methodName} �� {e.Message}");
        }
    }

    // ��ü �ʱ�ȭ ������ ����
    private IEnumerator SafeInitialize()
    {
        if (isInitializing) yield break;
        isInitializing = true;

        Debug.Log("�ʱ�ȭ ����");

        // 1�ܰ�: ���� �� �ε� �Ϸ��ߴٰ� �˸�
        SendRPCSafely(nameof(OnPlayerSceneLoaded), PhotonNetwork.LocalPlayer.ActorNumber);

        // 2�ܰ�: ��� �÷��̾� �� �ε� �Ϸ� ���
        yield return StartCoroutine(WaitForAllPlayersLoaded());

        // 3�ܰ�: �Ŵ����� Awake �Ϸ� ���
        yield return StartCoroutine(WaitForManagersAwake());

        // 4�ܰ�: ���� �ʱ�ȭ (������ �ִ� �͵�)
        yield return StartCoroutine(InitializeSequentialManagers());

        // 5�ܰ�: ���� �ʱ�ȭ (�������� �͵�)
        yield return StartCoroutine(InitializeParallelManagers());

        // 6�ܰ�: ���� �ʱ�ȭ �Ϸ��ߴٰ� �˸�
        SendRPCSafely(nameof(OnPlayerInitialized), PhotonNetwork.LocalPlayer.ActorNumber);

        // 7�ܰ�: ��� �÷��̾� �ʱ�ȭ �Ϸ� ���
        yield return StartCoroutine(WaitForAllPlayersInitialized());

        // 8�ܰ�: ���� ����
        if (PhotonNetwork.IsMasterClient)
        {
            SendRPCSafely(nameof(StartGame));
        }

        isInitializing = false;
    }

    #region �÷��̾� ����ȭ RPC
    // �� �÷��̾ �� �ε� �Ϸ������� ��� �÷��̾�� �˸�
    // ȣ�� ����: SafeInitialize() 1�ܰ迡�� �ڵ� ȣ��
    // loadedPlayers ���տ� �÷��̾� ID �߰�
    [PunRPC]
    void OnPlayerSceneLoaded(int playerId)
    {
        loadedPlayers.Add(playerId);
        Debug.Log($"�÷��̾� {playerId} �ε� �Ϸ� ({loadedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
    }

    // �� �÷��̾ �Ŵ��� �ʱ�ȭ �Ϸ������� �˸�
    // ȣ�� ����: ����/���� �ʱ�ȭ �Ϸ� ��
    // InitializedPlayers ���տ� �÷��̾� ID �߰�
    [PunRPC]
    void OnPlayerInitialized(int playerId)
    {
        initializedPlayers.Add(playerId);
        Debug.Log($"�÷��̾� {playerId} �ʱ�ȭ �Ϸ� ({initializedPlayers.Count}/{PhotonNetwork.CurrentRoom.PlayerCount})");
    }

    // ��� �÷��̾ �غ�Ǹ� ���� ���� ���� ��ȣ
    // ȣ����: ������ Ŭ���̾�Ʈ
    // NotifyGameStart() ȣ���Ͽ� �� �Ŵ����鿡�� ���� ���� �˸�
    [PunRPC]
    void StartGame()
    {
        Debug.Log($"{GameType} ���� ����!");
        NotifyGameStart();
    }
    #endregion

    #region ��� ����
    private IEnumerator WaitForAllPlayersLoaded() // ��� �÷��̾ �� �ε� �Ϸ��� ������ ���
    {
        Debug.Log("��� �÷��̾� �ε� ��� ��...");

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
        var coroutines = new List<Coroutine>();

        foreach (var component in components)
        {
            var coroutine = StartCoroutine(InitializeCoroutineComponentSafely(component));
            coroutines.Add(coroutine);
        }

        // ��� ���� �ʱ�ȭ �Ϸ� ���
        while (coroutines.Any(c => c != null))
        {
            coroutines.RemoveAll(c => c == null);
            yield return null;
        }

        Debug.Log("��� ���� �ʱ�ȭ �Ϸ�");
    }

    private IEnumerator InitializeCoroutineComponentSafely(ICoroutineGameComponent component)
    {
        IEnumerator routine = null;
        
        try
        {
            routine = component.InitializeCoroutine();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{GameType}Controller] ���� �ʱ�ȭ �غ� ���� {component.GetType().Name}: {e.Message}");
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
            Debug.LogError($"[{GameType}Controller] ���� �ʱ�ȭ ���� {component.GetType().Name}: {error.Message}");
        }
        else
        {
            Debug.Log($"���� �ʱ�ȭ �Ϸ�: {component.GetType().Name}");
        }
    }
    #endregion
}