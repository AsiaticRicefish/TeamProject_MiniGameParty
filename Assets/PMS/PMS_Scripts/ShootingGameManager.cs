using UnityEngine;
using DesignPattern;

public class ShootingGameManager : CombinedSingleton<ShootingGameManager>, IGameComponent , IGameStartHandler
{
    public void Initialize()
    {
        Debug.Log("[ShootingGameManager] - ���� ���� �ʱ�ȭ");
    }

    public void OnGameStart()
    {
        Debug.Log("[ShootingGameManager] - ���� ���� ����!");
    }
}
