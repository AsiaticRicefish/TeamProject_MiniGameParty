using UnityEngine;
using DesignPattern;

public class ShootingGameManager : CombinedSingleton<ShootingGameManager>, IGameComponent , IGameStartHandler
{
    public void Initialize()
    {
        Debug.Log("[ShootingGameManager] - 슈팅 게임 초기화");
    }

    public void OnGameStart()
    {
        Debug.Log("[ShootingGameManager] - 슈팅 게임 시작!");
    }
}
