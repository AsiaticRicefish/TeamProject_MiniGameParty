using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class JengaSceneController : BaseGameSceneController
{
    protected override string GameType => "Jenga";
    private static JengaSceneController _only;

    private void Awake()
    {
        if (_only && _only != this)
        {
            Debug.LogWarning("[Jenga] Duplicate JengaSceneController destroyed.");
            Destroy(gameObject);
            return;
        }
        _only = this;
    }

    protected override IEnumerator WaitForManagersAwake()
    {
        // �� �Ŵ������� Awake���� �����Ǳ⸦ ��ٸ�
        yield return WaitForSingletonReady<JengaGameManager>();
        yield return WaitForSingletonReady<JengaNetworkManager>();
        yield return WaitForSingletonReady<JengaTowerManager>();

        Debug.Log("���� �Ŵ����� Awake �Ϸ�");
    }

    protected override IEnumerator InitializeSequentialManagers()
    {
        // ���������� �ʱ�ȭ�ؾ� �� �Ŵ�����
        var sequentialComponents = new IGameComponent[]
        {
            JengaNetworkManager.Instance,     // ��Ʈ��ũ ����
            JengaGameManager.Instance,        // ���� ����
            JengaTowerManager.Instance,       // Ÿ�� ����
        };

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));
    }

    protected override IEnumerator InitializeParallelManagers()
    {
        // ���ķ� �ʱ�ȭ�ص� �Ǵ� �Ŵ�����
        var parallelComponents = new ICoroutineGameComponent[]
        {
          //  JengaUIManager.Instance,          // UI �غ�
          //  JengaTimingManager.Instance,      // Ÿ�̹� �ý��� �غ�
          //  JengaSoundManager.Instance        // ���� �غ�
        };

        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
    }

    protected override void NotifyGameStart()
    {
        if (JengaGameManager.Instance == null)
        {
            Debug.LogError("[NotifyGameStart] JengaGameManager is NULL");
            return;
        }
        try
        {
            JengaGameManager.Instance.StartGame();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NotifyGameStart] {ex}\n{ex.StackTrace}");
        }
    }
}