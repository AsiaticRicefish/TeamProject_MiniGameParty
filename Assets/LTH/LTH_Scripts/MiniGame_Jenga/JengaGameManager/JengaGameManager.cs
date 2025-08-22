using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using UnityEngine;
using ExitGames.Client.Photon;


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
        if (JengaNetworkManager.Instance == null)
        {
            Debug.LogError("[JengaGameManager.StartGame] NetworkManager is NULL");
            return;
        }
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

            // ������ �ر��� ���� �Ϸ� ó��
            if (!playerFinished[uid])
            {
                playerFinished[uid] = true;
                OnPlayerFinished?.Invoke(uid);
                CheckAllPlayersFinished();
            }
        }
        OnPlayerAction?.Invoke(uid, success, scoreGained);
    }

    private void CheckAllPlayersFinished()
    {
        // ���� Ż���� ���� ���� ���
        // �ƴϸ� Ÿ�Ӿ����θ� ����
        if (playerFinished.Values.All(f => f))
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

    /// <summary>
    /// �� Ÿ�� ������ Ÿ���� �������ٴ� ���� �����Ͱ� �������� �� ó���ϴ� �Լ�
    /// </summary>
    public void OnTowerCollapsed(int ownerActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return; // �����͸� ó��
        if (currentState != JengaGameState.Playing) return; // late RPC ����

        // 1) �켱 Ÿ������ UID�� ���� (Ÿ�� ���� �� ownerUid ������ �δ� ����)
        string uid = JengaTowerManager.Instance?.GetOwnerUidByActor(ownerActorNumber);

        // 2) ���� �� ������ Actor��UID ���� �õ�
        if (string.IsNullOrEmpty(uid))
            uid = TryGetUidFromActor(ownerActorNumber);

        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning($"[JengaGameManager.OnTowerCollapsed] UID not resolved. actor = {ownerActorNumber}");
            return;
        }

        if (!players.TryGetValue(uid, out var pdata))
        {
            Debug.LogWarning($"[JengaGameManager.OnTowerCollapsed] Player not registered. uid={uid}. Auto-registering as spectator.");

            return;
        }

        if (!pdata.isAlive) return;

        // Ż�� ó��
        pdata.isAlive = false;

        // ���� ���� ���Ŀ��� ������ ���� �ð��� �ִ밪���� �����Ͽ� �������� �и����� ����
        pdata.lastSuccessTime = float.MaxValue;

        // �ߺ� ���� : �̹� Finished ó���� ������ �ٽ� �̺�Ʈ�� ���� �ʵ��� ����ó��
        if (!playerFinished.TryGetValue(uid, out var finished) || !finished)
        {
            playerFinished[uid] = true;

            OnPlayerAction?.Invoke(uid, false, 0); // ���� �׼� �̺�Ʈ
            OnPlayerFinished?.Invoke(uid); // ���� ��� ��ȯ, UI ǥ�� � Ȱ���ϵ��� �̺�Ʈ ȣ��
        }

        CheckAllPlayersFinished();
    }

    /// <summary>
    /// Photon �� Firebase�� �������ִ� �������⡱ ����
    /// </summary>
    private string TryGetUidFromActor(int actorNumber)
    {
        // ���� �濡 �ִ� �÷��̾� �� ActorNumber�� ���� �÷��̾ ã��
        var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);

        // ã�� �÷��̾��� CustomProperties���� "uid" Ű ������
        if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("uid", out var uidObj))
        {
            return uidObj as string;
        }
        return null;
    }

    /// <summary>
    /// ���� ���� ������ �÷��̾� ���� ����.
    /// </summary>
    private int AliveCount()
    {
        // OnTowerCollapsed�� ���� ���� �Ǵܿ� �����
        return players.Count(kv => kv.Value.isAlive);
    }
}