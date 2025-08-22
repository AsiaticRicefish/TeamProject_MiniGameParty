using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Photon.Pun;
using ShootingScene;

[RequireComponent(typeof(PhotonView))]
[DisallowMultipleComponent]
public class ShootingSceneController : BaseGameSceneController
{
    //���� �̺�Ʈ�� ���°� ����
    public Action OnGameStarted;

    protected override string GameType => "Shooting";

    protected override IEnumerator WaitForManagersAwake()
    {
        yield return WaitForSingletonReady<ShootingNetworkManager>();
        yield return WaitForSingletonReady<ShootingGameManager>();

        Debug.Log("��� ShootingGameScene �Ŵ��� Awake�Ϸ�");
    }

    //���� �ʱ�ȭ
    protected override IEnumerator InitializeSequentialManagers()
    {
        Debug.Log("ShootingGameScene ���� �ʱ�ȭ ����");

        var sequentialComponents = new IGameComponent[]
        {
            ShootingNetworkManager.Instance,
            ShootingGameManager.Instance
        };

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));
    }

    //���� �ʱ�ȭ
    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("ShootingGameScene ���� �ʱ�ȭ ����");

        var parallelComponents = new List<ICoroutineGameComponent>();

        //parallelComponents.Add()

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
            ShootingGameManager.Instance.OnGameStart();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NotifyGameStart] {ex}\n{ex.StackTrace}");
        }
    }

}
