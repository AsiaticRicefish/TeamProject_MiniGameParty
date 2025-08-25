using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Firebase UID 기준으로 각 플레이어의 GamePlayer 인스턴스를 등록/조회하는 전역 관리 싱글톤
/// </summary>

public class PlayerManager : CombinedSingleton<PlayerManager>
{
    // Key: string 형태의 Firebase UID
    // Value: GamePlayer 인스턴스
    private Dictionary<string, GamePlayer> players = new();

    public IReadOnlyDictionary<string, GamePlayer> Players => players;


    /// <summary>
    /// 플레이어 등록 메서드
    /// id는 UID, nickname은 표시용 Photon 닉네임
    /// </summary>
    public void RegisterPlayer(string id, string nickname)
    {
        if (!players.ContainsKey(id)) // 중복 등록 방지
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
    /// 플레이어를 생성하거나 기존 플레이어를 반환
    /// 없으면 자동으로 생성해서 등록 후 반환
    /// </summary>
    public GamePlayer CreateOrGetPlayer(string id, string nickname = "Unknown")
    {
        if (players.TryGetValue(id, out var existingPlayer))
        {
            return existingPlayer;
        }

        // 플레이어가 없으면 새로 생성
        RegisterPlayer(id, nickname);
        return players[id];
    }


    /// <summary>
    /// 특정 플레이어의 데이터를 UID 기준으로 가져옴
    /// </summary>
    public GamePlayer GetPlayer(string id)
    {
        players.TryGetValue(id, out var player);
        return player;
    }

    /// <summary>
    /// 현재 방에 있는 모든 Photon 플레이어를 PlayerManager에 등록
    /// 젠가 게임 시작 전에 호출하여 모든 플레이어가 등록되도록 보장
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
    /// 플레이어 제거
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
    /// 모든 플레이어 정리 (씬 전환 시 필요하면)
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
    /// Firebase UID를 기준으로 현재 클라이언트의 GamePlayer 데이터를 조회
    /// FirebaseAuthManager는 예시이며, 다른 클래스이면 수정하여 작업하시면 됩니다.
    /// </summary>
    // public GamePlayer LocalPlayer => GetPlayer(FirebaseAuthManager.UserUID);
}