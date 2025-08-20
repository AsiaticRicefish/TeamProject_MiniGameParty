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
            players.Add(id, new GamePlayer(id, nickname));
        }
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
    /// Firebase UID를 기준으로 현재 클라이언트의 GamePlayer 데이터를 조회
    /// FirebaseAuthManager는 예시이며, 다른 클래스이면 수정하여 작업하시면 됩니다.
    /// <summary>
    // public GamePlayer LocalPlayer => GetPlayer(FirebaseAuthManager.UserUID);
}