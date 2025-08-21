using UnityEngine;
using Photon.Pun;
using DesignPattern;

public class ShootingGameManager : PunSingleton<ShootingGameManager>, IGameComponent , IGameStartHandler
{
    private ShootingGameState currentState;

    public int CurrentRound { get; private set; } = 0;
    public int MaxRounds { get; private set; } = 3;

    private void Start()
    {
        ChangeState(new InitState());
    }

    private void Update()
    {
        currentState?.Update();
    }

    //���� ���� ���� (���� ȣ�� ����!!!!!!!!!!!!!!!!!!!!!!, RPC ���� ȣ��)
    public void ChangeState(ShootingGameState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void Initialize()
    {
        Debug.Log("[ShootingGameManager] - ���� ���� �ʱ�ȭ");
    }

    public void OnGameStart()
    {
        Debug.Log("[ShootingGameManager] - ���� ���� ����!");
    }

    [PunRPC]
    public void RPC_ChangeState(string stateName)
    {
        switch (stateName)
        {
            case "InitState": ChangeState(new InitState()); break;
            case "CardSelectState": ChangeState(new CardSelectState()); break;
            default:
                Debug.Log($"[ShootingGameManager - RPC_ChangeState] - {stateName}�� �ش�Ǵ� ���°� ���� ���� �ʽ��ϴ�"); break;
        }
    }
}
