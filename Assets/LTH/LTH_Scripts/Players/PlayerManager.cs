using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Firebase UID �������� �� �÷��̾��� GamePlayer �ν��Ͻ��� ���/��ȸ�ϴ� ���� ���� �̱���
/// </summary>

public class PlayerManager : CombinedSingleton<PlayerManager>
{
    // Key: string ������ Firebase UID
    // Value: GamePlayer �ν��Ͻ�
    private Dictionary<string, GamePlayer> players = new();

    public IReadOnlyDictionary<string, GamePlayer> Players => players;


    /// <summary>
    /// �÷��̾� ��� �޼���
    /// id�� UID, nickname�� ǥ�ÿ� Photon �г���
    /// </summary>
    public void RegisterPlayer(string id, string nickname)
    {
        if (!players.ContainsKey(id)) // �ߺ� ��� ����
        {
            GameObject playerObj = new GameObject($"GamePlayer_{nickname}");
            var gamePlayer = playerObj.AddComponent<GamePlayer>();
            gamePlayer.Init(id, nickname);

            players.Add(id, gamePlayer);
        }
    }

    /// <summary>
    /// Ư�� �÷��̾��� �����͸� UID �������� ������
    /// </summary>
    public GamePlayer GetPlayer(string id)
    {
        players.TryGetValue(id, out var player);
        return player;
    }

    /// <summary>
    /// Firebase UID�� �������� ���� Ŭ���̾�Ʈ�� GamePlayer �����͸� ��ȸ
    /// FirebaseAuthManager�� �����̸�, �ٸ� Ŭ�����̸� �����Ͽ� �۾��Ͻø� �˴ϴ�.
    /// <summary>
    // public GamePlayer LocalPlayer => GetPlayer(FirebaseAuthManager.UserUID);
}