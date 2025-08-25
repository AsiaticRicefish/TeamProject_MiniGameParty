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
        loading?.Show("�ʱ�ȭ �غ� ��...", 0.05f);
    }

    protected override IEnumerator WaitForManagersAwake()
    {
        // ��� �÷��̾ uid ���õ� ������ ��� ���
        loading?.Set("�÷��̾� ����ȭ Ȯ�� ��...", 0.10f);
        yield return WaitForAllPlayerUids(5f);

        // �� �Ŵ������� Awake���� �����Ǳ⸦ ��ٸ�
        loading?.Set("�Ŵ��� �غ� ��...", 0.20f);
        yield return WaitForSingletonReady<JengaGameManager>();
        loading?.Set(progress: 0.30f);
        yield return WaitForSingletonReady<JengaNetworkManager>();
        loading?.Set(progress: 0.40f);
        yield return WaitForSingletonReady<JengaTowerManager>();
        loading?.Set(progress: 0.50f);

        Debug.Log("���� �Ŵ����� Awake �Ϸ�");
    }

    protected override IEnumerator InitializeSequentialManagers()
    {
        // ���� �ʱ�ȭ
        loading?.Set("�ٽ� �ý��� �ʱ�ȭ (1/2)...", 0.55f);

        // ���������� �ʱ�ȭ�ؾ� �� �Ŵ�����
        var sequentialComponents = new IGameComponent[]
        {
            JengaNetworkManager.Instance,     // ��Ʈ��ũ ����
            JengaGameManager.Instance,        // ���� ����
            JengaTowerManager.Instance,       // Ÿ�� ����
        };

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));

        loading?.Set(progress: 0.80f);
    }

    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("[Scene] InitializeParallelManagers START");

        // ���� �ʱ�ȭ
        loading?.Set("���� �ý��� �ʱ�ȭ (2/2)...", 0.85f);

        // ���ķ� �ʱ�ȭ�ص� �Ǵ� �Ŵ�����
        var parallelComponents = new ICoroutineGameComponent[]
        {
           JengaTimingManager.Instance      // Ÿ�̹� �ý��� �غ�
        };

        Debug.Log("[Scene] About to call InitializeCoroutineComponentsSafely");
        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
        Debug.Log("[Scene] InitializeCoroutineComponentsSafely returned - proceeding to step 6");

        loading?.Set(progress: 0.95f);
        Debug.Log($"[Scene] Before failsafe check - _startNotified: {_startNotified}");

        // ���ϼ�����: ���⼭ �� �� �� ���� ���� ȣ��
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
            loading?.Set("���� �غ� �Ϸ�!", 1.0f);

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

    // --- ��ƿ: ��� �÷��̾ uid ���õ� ������ ��� ---
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