using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using UnityEngine;


/// <summary>
/// JengaGameManager�� ���� �̴ϰ����� ��ü ������ �����ϴ� �߾� ��Ʈ�ѷ� ������ �����ϸ�,
/// ���� ���� ����, Ÿ�̸�, �÷��̾� ���� �ʱ�ȭ, ���� ó��, ���� ���, ���� ���� �� 
/// ���� �� ���ͱ��� �����Ѵ�.
/// </summary>

public class JengaGameManager : CombinedSingleton<JengaGameManager>, IGameComponent
{
    [Header("���� ����")]
    [SerializeField] private float gameTime = 180f; // ��ü ���� �ð� (�⺻ 180��)
    [SerializeField] private string mainMapSceneName; // ���� �� �̸� (��: "MainGameScene")

    [Header("���� ����")]
    public JengaGameState currentState = JengaGameState.Waiting;
    public float remainingTime; // ���� �ð�

    // �̺�Ʈ
    public Action<JengaGameState> OnGameStateChanged;       // ���� ���� ���� �̺�Ʈ
    public Action<string, bool, int> OnPlayerAction;        // �÷��̾�ID, ��������, ����
    public Action<string> OnPlayerFinished;                 // �÷��̾ ���� �Ϸ�
    public Action<Dictionary<string, int>> OnGameFinished;  // ���� ����

    private Dictionary<string, JengaPlayerData> players = new(); // UID�� key�� ������ �÷��̾� ������
    private Dictionary<string, int> playerScores = new();        // �÷��̾ ����
    private Dictionary<string, bool> playerFinished = new();     // �÷��̾ ���� �Ϸ� ����

    protected override void OnAwake()
    {
        base.isPersistent = false;
    }

    public void Initialize()
    {
        InitializePlayers(); // �÷��̾� ���� ����
        currentState = JengaGameState.Waiting;
        remainingTime = gameTime;

        Debug.Log("[JengaGameManager - Initialize] �ʱ�ȭ �Ϸ�");
    }

    private void InitializePlayers()
    {
        // ���� �濡 ������ �ִ� ��� Photon �÷��̾� ����� ��ȸ
        foreach (var photonPlayer in PhotonNetwork.PlayerList)
        {
            // PhotonNetwork.PlayerList���� ���� �÷��̾� ��ü�� CustomProperties���� uid (Firebase UID)�� ����
            string uid = photonPlayer.CustomProperties["uid"] as string;

            // UID�� ������� PlayerManager���� �ش� �÷��̾��� GamePlayer ��ü�� ������
            var gamePlayer = PlayerManager.Instance.GetPlayer(uid);
            if (gamePlayer != null)
            {
                // GamePlayer�� �̴ϰ��� ���� �������� JengaPlayerData�� ���� ����� �Ҵ�
                gamePlayer.JengaData = new JengaPlayerData
                {
                    towerPosition = GetPlayerTowerPosition(uid),
                    gameStartTime = Time.time,
                };

                // JengaGameManager�� players ��ųʸ��� UID�� key�� ����ؼ� JengaPlayerData�� ���
                players[uid] = gamePlayer.JengaData;
                // ������ �����ϴ� playerScores ��ųʸ����� �ش� UID�� 0�� ��� (�ʱⰪ)
                playerScores[uid] = 0;
                // ���� ������ ������ �ʾҴٴ� �ǹ̷� playerFinished �÷��׸� false�� ����
                playerFinished[uid] = false;
            }
            else
            {
                Debug.LogError($"[JengaGameManager - InitializePlayers] {uid}�� �ش��ϴ� GamePlayer�� ã�� �� ����");
            }
        }
    }

    public void StartGame()
    {
        // ���� ������ ��Ʈ��ũ �Ŵ����� ���� ����
        JengaNetworkManager.Instance.BroadcastGameState(JengaGameState.Playing);

        // ��� �÷��̾ ���ÿ� ���� ����
        StartCoroutine(GameTimer());

        Debug.Log(" [JengaGameManager - StartGame] ��� �÷��̾� ���� ���� ���� ����!");
    }

    /// <summary>
    /// ����ȭ�� ���� ���¸� ���ο� �����ϰ� �̺�Ʈ�� �˸�
    /// </summary>
    public void ApplyGameStateChange(JengaGameState newState)
    {
        currentState = newState;
        OnGameStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// ��Ʈ��ũ�� ���� ���ŵ� �÷��̾� �ൿ ����� ���� ���¿� �ݿ�
    /// </summary>
    public void ApplyPlayerActionResult(string uid, bool success, int scoreGained = 0)
    {
        // �ش� UID�� players ��ųʸ��� ������ (��, ��ϵ��� ���� �÷��̾��) �ƹ� ���� �� �ϰ� ����.
        if (!players.TryGetValue(uid, out var player)) return;

        if (success)
        {
            player.score += scoreGained; // JengaPlayerData ��ü�� ����� ���� ������Ʈ
            playerScores[uid] += scoreGained; // ���� ����� ���� ����

            // ������ ���� �ð� ���� (��� �ð�) => ������ ��� ���� ������ �÷��̾ �켱 ���� ��ġ
            player.lastSuccessTime = Time.time - player.gameStartTime;
        }
        else
        {
            player.isAlive = false;
        }

        // �÷��̾ ���� ���� �Ϸ� ó���� ���� �ʾҴٸ�
        if (!playerFinished[uid])
        {
            playerFinished[uid] = true;
            OnPlayerFinished?.Invoke(uid);
            CheckAllPlayersFinished();
        }
        OnPlayerAction?.Invoke(uid, success, scoreGained);
    }

    private void CheckAllPlayersFinished()
    {
        // ��� �÷��̾ ������ ���ưų�, �ð��� �� �Ǿ����� EndGame() ȣ��
        if (playerFinished.Values.All(f => f) || remainingTime <= 0)
        {
            EndGame();
        }
    }

    /// <summary>
    /// ���� ���� �� ���� ������ ����ϰ� ���� ���ӿ� ����� ����
    /// </summary>
    private void EndGame()
    {
        JengaNetworkManager.Instance.BroadcastGameState(JengaGameState.Finished); // ���� ���¸� "Finished"�� ����

        // ���� ��� (���� ����, �Ϸ� �ð��� ���)
        var rankings = CalculateRankings();
        OnGameFinished?.Invoke(rankings); // OnGameFinished�� �ܺο� �˸�

        // ���� ���ӿ� ��� ����
        SendResultToMainGame(rankings);
    }

    private Dictionary<string, int> CalculateRankings()
    {
        // ���������� ����, ������ ��� ���� ���η� �Ǵ�
        var sortedPlayers = players
            .OrderByDescending(pair => pair.Value.score) // ���� ��� ����
            .ThenBy(pair => pair.Value.lastSuccessTime) // ���� �� ���� ���
            .ToList();

        // ��ųʸ� ���·� UID�� ������ ����
        // { "playerA": 1, "playerB": 2, "playerC": 3, "playerD": 4 }
        var rankings = new Dictionary<string, int>();
        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            string uid = sortedPlayers[i].Key; // Key�� UID
            rankings[uid] = i + 1; // 1��, 2��, 3��, 4�� ������ ��ȣ �ű�
        }
        return rankings;
    }

    /// <summary>
    /// ���� ������ ���� ���� �ý��ۿ� �����ϰ�,
    /// �÷��̾��� �¸� ���θ� ������Ʈ�� �� ���� ������ ���� �غ�
    /// </summary>
    private void SendResultToMainGame(Dictionary<string, int> rankings)
    {
        // ���� ���ӿ� ��� ���� ("Jenga"��� Ű�� ��� ����)
        GameResultData.SetMinigameResult("Jenga", rankings);

        // ���� ������ PlayerManager�� ���� ���� ������Ʈ
        foreach (var pair in rankings)
        {
            // PlayerManager�� ���� ���� �÷��̾� ������Ʈ�� ã��
            var player = PlayerManager.Instance.GetPlayer(pair.Key);
            if (player != null)
            {
                // gamePlayer.WinThisMiniGame = (1������ ����) ����
                player.WinThisMiniGame = pair.Value == 1;
            }
        }
        // ���� �ð� �� ���� ������ ����
        StartCoroutine(ReturnToMainGameAfterDelay(3f));
    }

    /// <summary>
    /// ���� �ð� �� ���� ������ ��ȯ (������ Ŭ���̾�Ʈ�� ȣ��)
    /// </summary>
    private IEnumerator ReturnToMainGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // �� ��ȯ
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(mainMapSceneName); // �� �̸��� ���� ����
        }
    }

    /// <summary>
    /// ���� Ÿ�̸�
    /// </summary>
    private IEnumerator GameTimer()
    {
        while (remainingTime > 0 && currentState == JengaGameState.Playing)
        {
            // �� �������� �ƴ� 1�ʸ��� remainingTime-- ����
            yield return new WaitForSeconds(1f);
            remainingTime--;
        }

        // �ð��� �� �Ǹ� EndGame() ȣ��
        if (currentState == JengaGameState.Playing)
        {
            EndGame();
        }
    }

    private Vector3 GetPlayerTowerPosition(string playerId)
    {
        // 4���� �÷��̾ ���� �ٸ� ��ġ�� Ÿ�� ��ġ
        Vector3[] towerPositions = {
            new Vector3(-5, 0, 5),   // �÷��̾� 1
            new Vector3(5, 0, 5),    // �÷��̾� 2  
            new Vector3(-5, 0, -5),  // �÷��̾� 3
            new Vector3(5, 0, -5)    // �÷��̾� 4
        };
        // �÷��̾� ID�� �ؽ��ڵ带 �̿��� ��ġ �ε��� ����
        int index = Math.Abs(playerId.GetHashCode()) % 4;
        return towerPositions[index];
    }

    #region �ܺο��� ��ȸ�ϴ� ������ (UI�� ����ó���� ���)
    // �ܺο��� Ư�� �÷��̾��� ���� ���� ��ȸ
    public int GetPlayerScore(string uid)
    {
        // playerScores ��ųʸ����� �� �������� (TryGetValue)
        return playerScores.TryGetValue(uid, out var score) ? score : 0;
    }

    // �ܺο��� Ư�� �÷��̾��� ���� �Ϸ� ���� ��ȸ
    public bool IsPlayerFinished(string uid)
    {
        return playerFinished.TryGetValue(uid, out var finished) && finished;
    }
    #endregion
}