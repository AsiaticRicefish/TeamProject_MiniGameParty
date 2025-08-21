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

    //게임 상태 변경 (로컬 호출 금지!!!!!!!!!!!!!!!!!!!!!!, RPC 통해 호출)
    public void ChangeState(ShootingGameState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void Initialize()
    {
        Debug.Log("[ShootingGameManager] - 슈팅 게임 초기화");
    }

    public void OnGameStart()
    {
        Debug.Log("[ShootingGameManager] - 슈팅 게임 시작!");
    }

    [PunRPC]
    public void RPC_ChangeState(string stateName)
    {
        switch (stateName)
        {
            case "InitState": ChangeState(new InitState()); break;
            case "CardSelectState": ChangeState(new CardSelectState()); break;
            default:
                Debug.Log($"[ShootingGameManager - RPC_ChangeState] - {stateName}에 해당되는 상태가 존재 하지 않습니다"); break;
        }
    }
}
