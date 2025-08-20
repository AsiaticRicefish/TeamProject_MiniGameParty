using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using ShootingScene;

public class ShootingSceneController : BaseGameSceneController
{
    //���� �̺�Ʈ�� ���°� ����
    public Action OnGameStarted;

    protected override string GameType => "Shooting";

    protected override IEnumerator WaitForManagersAwake()
    {
        yield return new WaitUntil(() =>
            ShootingGameManager.Instance != null &&
            TurnManager.Instance != null);

        Debug.Log("��� ShootingGameScene �Ŵ��� Awake�Ϸ�");
    }

    //���� �ʱ�ȭ
    protected override IEnumerator InitializeSequentialManagers()
    {
        Debug.Log("ShootingGameScene ���� �ʱ�ȭ ����");

        var sequentialComponents = new List<IGameComponent>();

        // ������ ������� �߰�
        /* if (ShootingGameManager.Instance is IGameComponent gameManagerComp)
            sequentialComponents.Add(gameManagerComp);*/

        /* if (TurnManager.Instance is IGameComponent turnManagerComp)
            sequentialComponents.Add(turnManagerComp);*/

        // �ʿ��ϴٸ� �ٸ� ���� �ʱ�ȭ ������Ʈ�� �߰�

        yield return StartCoroutine(InitializeComponentsSafely(sequentialComponents));
    }

    //���� �ʱ�ȭ
    protected override IEnumerator InitializeParallelManagers()
    {
        Debug.Log("ShootingGameScene ���� �ʱ�ȭ ����");

        var parallelComponents = new List<ICoroutineGameComponent>();

        // ���������� �ʱ�ȭ ������ ������Ʈ�� �߰�
        /*if (ShootingGameManager.Instance is ICoroutineGameComponent gameComp)
            parallelComponents.Add(gameComp);*/

        yield return StartCoroutine(InitializeCoroutineComponentsSafely(parallelComponents));
    }

    
    protected override void NotifyGameStart()
    {
        //OnGameStarted?.Invoke();

        if (ShootingGameManager.Instance is IGameStartHandler gameStart)
            gameStart.OnGameStart();

        Debug.Log("Shooting ���� ���� �˸� �Ϸ�");
    }

}
