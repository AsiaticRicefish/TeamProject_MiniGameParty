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
        // �� �Ŵ������� Awake���� �����Ǳ⸦ ��ٸ�
        yield return WaitForSingletonReady<JengaGameManager>();
        // yield return WaitForSingletonReady<JengaUIManager>();
        // yield return WaitForSingletonReady<JengaNetworkManager>();
        // yield return WaitForSingletonReady<JengaTowerManager>();
        // yield return WaitForSingletonReady<JengaTimingManager>();
        // yield return WaitForSingletonReady<JengaSoundManager>();

        Debug.Log("���� �Ŵ����� Awake �Ϸ�");
    }

    protected override IEnumerator InitializeSequentialManagers()
    {
        // ���������� �ʱ�ȭ�ؾ� �� �Ŵ�����
        var sequentialComponents = new IGameComponent[]
        {
          //  JengaNetworkManager.Instance,     // ��Ʈ��ũ ����
            JengaGameManager.Instance,        // ���� ����
          //  JengaTowerManager.Instance,       // Ÿ�� ����
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
        // ���� ���� ���� ����
        JengaGameManager.Instance?.StartGame();
      //  JengaUIManager.Instance?.ShowGameStartUI();
      //  JengaSoundManager.Instance?.PlayGameStartSound();
    }
}