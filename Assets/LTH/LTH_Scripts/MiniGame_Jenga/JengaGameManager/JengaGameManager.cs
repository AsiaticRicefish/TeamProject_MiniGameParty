using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using UnityEngine;
using ExitGames.Client.Photon;
using LDH_MainGame;


/// <summary>
/// JengaGameManager는 젠가 미니게임의 전체 진행을 통제하는 중앙 컨트롤러 역할을 수행하며,
/// 게임 상태 관리, 타이머, 플레이어 정보 초기화, 점수 처리, 순위 계산, 게임 종료 후 
/// 메인 씬 복귀까지 포함한다.
/// </summary>

public class JengaGameManager : CombinedSingleton<JengaGameManager>, IGameComponent
{
    [Header("게임 설정")]
    [SerializeField] private float gameTime = 180f; // 전체 게임 시간 (기본 180초)
    [SerializeField] private string mainMapSceneName; // 메인 씬 이름 (예: "MainGameScene")

    [Header("게임 상태")]
    public JengaGameState currentState = JengaGameState.Waiting;
    public float remainingTime; // 남은 시간
    [SerializeField] private float returnToLobbyDelay = 5f; // 지연시간 노출

    [Header("카운트다운 설정")]
    [SerializeField] private float countdownDuration = 3f; // 카운트다운 시간
    [SerializeField] private bool useCountdown = true; // 카운트다운 사용 여부

    [Header("UI 이벤트")]
    public Action<float> OnTimeUpdated;

    // 이벤트
    public Action<JengaGameState> OnGameStateChanged;       // 게임 상태 변경 이벤트
    public Action<string, bool, int> OnPlayerAction;        // 플레이어ID, 성공여부, 점수
    public Action<string> OnPlayerFinished;                 // 플레이어가 게임 완료
    public Action<Dictionary<string, int>> OnGameFinished;  // 최종 순위

    private Dictionary<string, JengaPlayerData> players = new(); // UID를 key로 가지는 플레이어 데이터
    private Dictionary<string, int> playerScores = new();        // 플레이어별 점수
    private Dictionary<string, bool> playerFinished = new();     // 플레이어별 게임 완료 여부

    protected override void OnAwake()
    {
        base.isPersistent = false;
        base.OnAwake();
    }

    public void Initialize()
    {
        // 먼저 모든 Photon 플레이어가 PlayerManager에 등록되도록 보장
        PlayerManager.Instance.EnsureAllPhotonPlayersRegistered();

        InitializePlayers(); // 플레이어 정보 세팅
        currentState = JengaGameState.Waiting;
        remainingTime = gameTime;

        Debug.Log("[JengaGameManager - Initialize] 초기화 완료");

        // 초기화 완료 후 카운트다운 시작 (마스터만)
        if (PhotonNetwork.IsMasterClient)
        {
            // 약간의 지연 후 카운트다운 시작 (다른 매니저들 초기화 완료 대기)
            StartCoroutine(DelayedCountdownStart());
        }
    }

    #region 플레이어 초기화
    private void InitializePlayers()
    {
        // 현재 방에 접속해 있는 모든 Photon 플레이어 목록을 순회
        foreach (var photonPlayer in PhotonNetwork.PlayerList)
        {
            // PhotonNetwork.PlayerList에서 꺼낸 플레이어 객체의 CustomProperties에서 uid (Firebase UID)를 추출
            string uid = photonPlayer.CustomProperties["uid"] as string;

            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning($"[JengaGameManager - InitializePlayers] Player {photonPlayer.NickName} has no UID in CustomProperties");
                continue;
            }

            //// UID를 기반으로 PlayerManager에서 해당 플레이어의 GamePlayer 객체를 가져옴
            //var gamePlayer = PlayerManager.Instance.GetPlayer(uid);

            // CreateOrGetPlayer를 사용하여 플레이어가 없으면 자동 생성
            var gamePlayer = PlayerManager.Instance.CreateOrGetPlayer(uid, photonPlayer.NickName);

            if (gamePlayer != null)
            {
                // GamePlayer에 미니게임 전용 데이터인 JengaPlayerData를 새로 만들어 할당
                gamePlayer.JengaData = new JengaPlayerData
                {
                    towerPosition = GetPlayerTowerPosition(uid),
                    gameStartTime = Time.time,
                };

                // JengaGameManager의 players 딕셔너리에 UID를 key로 사용해서 JengaPlayerData를 등록
                players[uid] = gamePlayer.JengaData;
                // 점수를 저장하는 playerScores 딕셔너리에도 해당 UID로 0점 등록 (초기값)
                playerScores[uid] = 0;
                // 아직 게임을 끝내지 않았다는 의미로 playerFinished 플래그를 false로 설정
                playerFinished[uid] = false;
                Debug.Log($"[JengaGameManager - InitializePlayers] Successfully initialized player: {uid} ({photonPlayer.NickName})");
            }
            else
            {
                Debug.LogError($"[JengaGameManager - InitializePlayers] {uid}에 해당하는 GamePlayer를 찾을 수 없음");
            }
        }
        Debug.Log($"[JengaGameManager - InitializePlayers] Initialized {players.Count} players");
    }
    #endregion

    public void StartGame()
    {
        if (JengaNetworkManager.Instance == null)
        {
            Debug.LogError("[JengaGameManager.StartGame] NetworkManager is NULL");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[JengaGameManager.StartGame] Not master client - waiting for state broadcast");
            return;
        }

        if (currentState == JengaGameState.Playing || currentState == JengaGameState.Finished)
        {
            Debug.Log("[JengaGameManager] Skip: already started/finished");
            return;
        }

        // 1) 마스터 로컬 먼저 Playing 세팅
        Debug.Log("[JengaGameManager] Step 1: ApplyGameStateChange(Playing)");
        ApplyGameStateChange(JengaGameState.Playing);

        // 2) 전체에 전파
        Debug.Log("[JengaGameManager] Step 2: BroadcastGameState(Playing)");
        JengaNetworkManager.Instance.BroadcastGameState(JengaGameState.Playing);

        // 3) 타이머는 카운트다운이 완전히 끝난 후에만 시작
        Debug.Log("[JengaGameManager] Step 3: StartCoroutine(GameTimer)");
        if (!useCountdown)
        {
            StartCoroutine(GameTimer());
        }
    }

    #region 카운트다운 관련
    /// <summary>
    /// 약간의 지연 후 카운트다운 시작
    /// </summary>
    private IEnumerator DelayedCountdownStart()
    {
        yield return new WaitForSeconds(0.5f); // 0.5초 대기
        StartGameWithCountdown();
    }


    /// <summary>
    /// 게임 초기화 완료 후 카운트다운 시작 (마스터만 호출)
    /// </summary>
    public void StartGameWithCountdown()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[JengaGameManager] Not master client - waiting for countdown broadcast");
            return;
        }

        if (currentState == JengaGameState.Finished)
        {
            Debug.Log("[JengaGameManager] Skip countdown: game already finished");
            return;
        }

        // 바로 Playing 상태로 변경
        ApplyGameStateChange(JengaGameState.Playing);
        JengaNetworkManager.Instance?.BroadcastGameState(JengaGameState.Playing);

        if (useCountdown)
        {
            Debug.Log("[JengaGameManager] Starting countdown...");

            // 네트워크 매니저를 통해 모든 클라이언트에게 카운트다운 시작 신호
            JengaNetworkManager.Instance?.BroadcastStartCountdown(countdownDuration);

            // 카운트다운 완료 후 게임 시작을 위한 코루틴
            StartCoroutine(CountdownToGameStart());
        }
        else
        {
            // 카운트다운 없이 바로 게임 시작
            StartGame();
        }
    }

    /// <summary>
    /// 카운트다운 완료를 기다린 후 게임 시작
    /// </summary>
    private IEnumerator CountdownToGameStart()
    {
        // 카운트다운 시간만큼 대기
        yield return new WaitForSeconds(countdownDuration + 1f); // +1초는 "START!" 표시 시간

        // 모든 클라이언트에게 카운트다운 완료 알림
        JengaNetworkManager.Instance?.BroadcastCountdownComplete();

        // 타이머 시작
        StartCoroutine(GameTimer());
    }

    #endregion

    /// <summary>
    /// 동기화된 게임 상태를 내부에 적용하고 이벤트로 알림
    /// </summary>
    public void ApplyGameStateChange(JengaGameState newState)
    {
        currentState = newState;
        OnGameStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// 네트워크를 통해 수신된 플레이어 행동 결과를 게임 상태에 반영
    /// </summary>
    public void ApplyPlayerActionResult(string uid, bool success, int scoreGained = 0)
    {
        // 해당 UID가 players 딕셔너리에 없으면 (즉, 등록되지 않은 플레이어면) 아무 동작 안 하고 종료.
        if (!players.TryGetValue(uid, out var player)) return;

        if (success)
        {
            player.score += scoreGained; // JengaPlayerData 자체에 저장된 점수 업데이트
            playerScores[uid] += scoreGained; // 순위 계산을 위한 점수

            // 마지막 성공 시각 저장 (상대 시간) => 동점일 경우 먼저 성공한 플레이어가 우선 순위 배치
            player.lastSuccessTime = Time.time - player.gameStartTime;
        }
        else
        {
            player.isAlive = false;

            // 젠가가 붕괴할 때만 완료 처리
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
        // 전원 탈락시 조기 종료 허용
        // 아니면 타임업으로만 종료
        if (playerFinished.Values.All(f => f))
        {
            Debug.Log("[JengaGameManager] All players eliminated - ending game early");
            EndGame();
        }
    }

    /// <summary>
    /// 게임 종료 후 최종 순위를 계산하고 메인 게임에 결과를 전달
    /// </summary>
    private void EndGame()
    {
        JengaNetworkManager.Instance.BroadcastGameState(JengaGameState.Finished); // 게임 상태를 "Finished"로 변경

        // 순위 계산 (점수 기준, 완료 시간도 고려)
        var rankings = CalculateRankings();
        OnGameFinished?.Invoke(rankings); // OnGameFinished로 외부에 알림

        // 메인 게임에 결과 전달
        SendResultToMainGame(rankings);
    }

    private Dictionary<string, int> CalculateRankings()
    {
        // 점수순으로 정렬, 동점일 경우 생존 여부로 판단
        var sortedPlayers = players
            .OrderByDescending(pair => pair.Value.score) // 먼저 블록 개수
            .ThenBy(pair => pair.Value.lastSuccessTime) // 동점 시 빠른 사람
            .ToList();

        // 딕셔너리 형태로 UID별 순위를 저장
        // { "playerA": 1, "playerB": 2, "playerC": 3, "playerD": 4 }
        var rankings = new Dictionary<string, int>();
        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            string uid = sortedPlayers[i].Key; // Key는 UID
            rankings[uid] = i + 1; // 1등, 2등, 3등, 4등 순으로 번호 매김
        }
        return rankings;
    }

    /// <summary>
    /// 점수 순위를 메인 게임 시스템에 전달하고,
    /// 플레이어의 승리 여부를 업데이트한 뒤 메인 씬으로 복귀 준비
    /// </summary>
    private void SendResultToMainGame(Dictionary<string, int> rankings)
    {
        // 메인 게임에 결과 전달 ("Jenga"라는 키로 결과 저장)
        GameResultData.SetMinigameResult("Jenga", rankings);

        // 메인 게임의 PlayerManager를 통한 순위 업데이트
        foreach (var pair in rankings)
        {
            // PlayerManager를 통해 실제 플레이어 오브젝트를 찾기
            var player = PlayerManager.Instance.GetPlayer(pair.Key);
            if (player != null)
            {
                // gamePlayer.WinThisMiniGame = (1등인지 여부) 설정
                player.WinThisMiniGame = pair.Value == 1;
            }
        }
        // 일정 시간 후 메인 씬으로 복귀
        StartCoroutine(ReturnToMainGameAfterDelay(returnToLobbyDelay));
    }

    /// <summary>
    /// 일정 시간 후 메인 씬으로 전환 (마스터 클라이언트만 호출)
    /// </summary>
    private IEnumerator ReturnToMainGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 씬 전환
        if (PhotonNetwork.IsMasterClient)
        {
            // PhotonNetwork.LoadLevel(mainMapSceneName); // 씬 이름은 변경 가능
            MainGameManager.Instance?.NotifyMiniGameFinish();
        }
    }

    /// <summary>
    /// 네트워크를 통해 동기화된 시간을 적용하고 UI 업데이트
    /// </summary>
    public void SyncRemainingTime(float syncedTime)
    {
        remainingTime = syncedTime;
        OnTimeUpdated?.Invoke(remainingTime);
    }

    /// <summary>
    /// 게임 타이머
    /// </summary>
    public IEnumerator GameTimer()
    {
        while (remainingTime > 0 && currentState == JengaGameState.Playing)
        {
            // 매 프레임이 아닌 1초마다 remainingTime-- 감소
            yield return new WaitForSeconds(1f);
            remainingTime--;

            // 마스터에서만 네트워크 동기화 실행 (시간이 서로 다르면 안됨)
            if (PhotonNetwork.IsMasterClient)
            {
                JengaNetworkManager.Instance?.BroadcastTimeSync(remainingTime);
            }
        }

        // 시간이 다 되면 EndGame() 호출
        if (currentState == JengaGameState.Playing)
        {
            EndGame();
        }
    }

    // UI에서 현재 남은 시간을 가져올 수 있는 퍼블릭 메서드
    public float GetRemainingTime() => remainingTime;
    public string GetFormattedTime()
    {
        // 반올림으로 더 정확한 시간 표시
        int totalSeconds = Mathf.RoundToInt(remainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private Vector3 GetPlayerTowerPosition(string playerId)
    {
        // 4명의 플레이어가 각자 다른 위치에 타워 배치
        Vector3[] towerPositions = {
            new Vector3(-5, 0, 5),   // 플레이어 1
            new Vector3(5, 0, 5),    // 플레이어 2  
            new Vector3(-5, 0, -5),  // 플레이어 3
            new Vector3(5, 0, -5)    // 플레이어 4
        };
        // 플레이어 ID의 해시코드를 이용해 위치 인덱스 결정
        int index = Math.Abs(playerId.GetHashCode()) % 4;
        return towerPositions[index];
    }

    #region 외부에서 조회하는 데이터 (UI나 조건처리에 사용)
    // 외부에서 특정 플레이어의 현재 점수 조회
    public int GetPlayerScore(string uid)
    {
        // playerScores 딕셔너리에서 값 가져오기 (TryGetValue)
        return playerScores.TryGetValue(uid, out var score) ? score : 0;
    }

    // 외부에서 특정 플레이어의 게임 완료 여부 조회
    public bool IsPlayerFinished(string uid)
    {
        return playerFinished.TryGetValue(uid, out var finished) && finished;
    }
    #endregion

    /// <summary>
    /// 이 타워 주인의 타워가 무너졌다는 것을 마스터가 수신했을 때 처리하는 함수
    /// </summary>
    public void OnTowerCollapsed(int ownerActorNumber)
    {
        if (!PhotonNetwork.IsMasterClient) return; // 마스터만 처리
        if (currentState != JengaGameState.Playing) return; // late RPC 방지

        // 1) 우선 타워에서 UID를 얻어본다 (타워 생성 시 ownerUid 저장해 두는 전제)
        string uid = JengaTowerManager.Instance?.GetOwnerUidByActor(ownerActorNumber);

        // 2) 실패 시 보조로 Actor→UID 매핑 시도
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

        // 탈락 처리
        pdata.isAlive = false;

        // 동점 순위 정렬에서 마지막 성공 시각을 최대값으로 설정하여 순위에서 밀리도록 설정
        pdata.lastSuccessTime = float.MaxValue;

        // 중복 방지 : 이미 Finished 처리된 유저면 다시 이벤트를 쏘지 않도록 예외처리
        if (!playerFinished.TryGetValue(uid, out var finished) || !finished)
        {
            playerFinished[uid] = true;

            OnPlayerAction?.Invoke(uid, false, 0); // 실패 액션 이벤트
            OnPlayerFinished?.Invoke(uid); // 관전 모드 전환, UI 표시 등에 활용하도록 이벤트 호출
        }

        CheckAllPlayersFinished();
    }

    /// <summary>
    /// Photon ↔ Firebase를 연결해주는 “번역기” 역할
    /// </summary>
    private string TryGetUidFromActor(int actorNumber)
    {
        // 현재 방에 있는 플레이어 중 ActorNumber가 같은 플레이어를 찾음
        var p = PhotonNetwork.PlayerList.FirstOrDefault(x => x.ActorNumber == actorNumber);

        // 찾은 플레이어의 CustomProperties에서 "uid" 키 꺼내기
        if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue("uid", out var uidObj))
        {
            return uidObj as string;
        }
        return null;
    }

    /// <summary>
    /// 현재 생존 상태인 플레이어 수를 센다.
    /// </summary>
    private int AliveCount()
    {
        // OnTowerCollapsed의 종료 조건 판단에 사용함
        return players.Count(kv => kv.Value.isAlive);
    }
}