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
            Debug.Log($"[PlayerManager] Registered new player: {id} ({nickname})");
        }
        else
        {
            Debug.Log($"[PlayerManager] Player {id} ({nickname}) already registered");
        }
    }

    /// <summary>
    /// �÷��̾ �����ϰų� ���� �÷��̾ ��ȯ
    /// ������ �ڵ����� �����ؼ� ��� �� ��ȯ
    /// </summary>
    public GamePlayer CreateOrGetPlayer(string id, string nickname = "Unknown")
    {
        if (players.TryGetValue(id, out var existingPlayer))
        {
            return existingPlayer;
        }

        // �÷��̾ ������ ���� ����
        RegisterPlayer(id, nickname);
        return players[id];
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
    /// ���� �濡 �ִ� ��� Photon �÷��̾ PlayerManager�� ���
    /// ���� ���� ���� ���� ȣ���Ͽ� ��� �÷��̾ ��ϵǵ��� ����
    /// </summary>
    public void EnsureAllPhotonPlayersRegistered()
    {
        if (PhotonNetwork.PlayerList == null) return;

        foreach (var photonPlayer in PhotonNetwork.PlayerList)
        {
            string uid = photonPlayer.CustomProperties["uid"] as string;
            if (!string.IsNullOrEmpty(uid))
            {
                string nickname = photonPlayer.NickName ?? "Unknown";
                CreateOrGetPlayer(uid, nickname);
            }
            else
            {
                Debug.LogWarning($"[PlayerManager] Photon player {photonPlayer.NickName} has no UID in CustomProperties");
            }
        }
    }

    /// <summary>
    /// �÷��̾� ����
    /// </summary>
    public void RemovePlayer(string id)
    {
        if (players.TryGetValue(id, out var player))
        {
            if (player != null && player.gameObject != null)
            {
                Destroy(player.gameObject);
            }
            players.Remove(id);
            Debug.Log($"[PlayerManager] Removed player: {id}");
        }
    }

    /// <summary>
    /// ��� �÷��̾� ���� (�� ��ȯ �� �ʿ��ϸ�)
    /// </summary>
    public void ClearAllPlayers()
    {
        foreach (var player in players.Values)
        {
            if (player != null && player.gameObject != null)
            {
                Destroy(player.gameObject);
            }
        }
        players.Clear();
        Debug.Log("[PlayerManager] Cleared all players");
    }

    /// <summary>
    /// Firebase UID�� �������� ���� Ŭ���̾�Ʈ�� GamePlayer �����͸� ��ȸ
    /// FirebaseAuthManager�� �����̸�, �ٸ� Ŭ�����̸� �����Ͽ� �۾��Ͻø� �˴ϴ�.
    /// </summary>
    // public GamePlayer LocalPlayer => GetPlayer(FirebaseAuthManager.UserUID);
}