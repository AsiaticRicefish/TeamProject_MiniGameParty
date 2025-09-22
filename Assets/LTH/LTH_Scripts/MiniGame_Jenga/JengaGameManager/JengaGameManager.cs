using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using ExitGames.Client.Photon;
using InputBlocker;
using LDH_MainGame;
using LDH_Util;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// === [추가] Room Properties 키 묶음 ===
public static class JengaRoomProps
{
    public const string KEY_PREFIX = "jg_";
    public const string KEY_STATE = KEY_PREFIX + "state";           // JengaGameState
    public const string KEY_START_TIME = KEY_PREFIX + "start";      // PhotonNetwork.Time
    public const string KEY_DURATION = KEY_PREFIX + "dur";          // int/float (seconds)
    public const string KEY_RANK_UIDS = KEY_PREFIX + "rank_uids";   // string[]
    public const string KEY_RANK_VALS = KEY_PREFIX + "rank_vals";   // int[]

    public static bool TryGet<T>(Hashtable table, string key, out T value)
    {
        if (table != null && table.ContainsKey(key) && table[key] is T t) { value = t; return true; }
        value = default; return false;
    }
}


/// <summary>
/// JengaGameManager는 젠가 미니게임의 전체 진행을 통제하는 중앙 컨트롤러 역할을 수행하며,
/// 게임 상태 관리, 타이머, 플레이어 정보 초기화, 점수 처리, 순위 계산, 게임 종료 후 
/// 메인 씬 복귀까지 포함한다.
/// </summary>

public class JengaGameManager : CombinedSingleton<JengaGameManager>, IGameComponent
{
    [Header("게임 설정")]
    [SerializeField] private float gameTime = 180f; // 전체 게임 시간 (기본 180초)

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
    public Action<Dictionary<string, int>> OnRankingsUpdated; // 실시간 순위 갱신 이벤트

    private Dictionary<string, JengaPlayerData> players = new(); // UID를 key로 가지는 플레이어 데이터
    private Dictionary<string, int> playerScores = new();        // 플레이어별 점수
    private Dictionary<string, bool> playerFinished = new();     // 플레이어별 게임 완료 여부
    public Dictionary<string, int> GetCurrentRanks() => CalculateRankings();

    private readonly Dictionary<string, int> _gridOrder = new(); // uid -> 0,1,2,...
    private int GridOrderOf(string uid) => _gridOrder.TryGetValue(uid, out var v) ? v : int.MaxValue;

    private static bool IsActive(JengaPlayerData d)
    => d.removedCount > 0 || d.score > 0 || d.lastSuccessTime > 0f || !d.isAlive;

    private Dictionary<string, int> _lastRankSnapshot;

    private double? _roomStartTime;
    private double? _roomDuration;
    private Coroutine _timerCo;
    private Coroutine _timerWatchdog;

    // 게임 종료 후 입력 차단
    private InputLockToken _endGameLock;

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
        OnTimeUpdated?.Invoke(remainingTime); // UI 초기값 3:00 표시

        JengaUIManager.Instance?.HideRotateButton();

        Debug.Log("[JengaGameManager - Initialize] 초기화 완료");

        // 이미 기록된 ROOM PROPS가 있으면 즉시 적용(레이트 조인/타이밍 보정)
        // 다른 클라이언트의 접속 순서에 상관없이 동기화 보장
        if (PhotonNetwork.InRoom)
        {
            var table = PhotonNetwork.CurrentRoom.CustomProperties;
            if (JengaRoomProps.TryGet<double>(table, JengaRoomProps.KEY_START_TIME, out var st) &&
                JengaRoomProps.TryGet<double>(table, JengaRoomProps.KEY_DURATION, out var du))
            {
                ApplySyncedTimerFromRoomProps(st, du); // 내부에서 TryStartRoomPropTimer() 호출됨
            }
            if (JengaRoomProps.TryGet<int>(table, JengaRoomProps.KEY_STATE, out var stateInt))
            {
                ApplyGameStateChange((JengaGameState)stateInt);
            }
        }

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

        if (PhotonNetwork.IsMasterClient)
        {
            var uidList = PhotonNetwork.PlayerList
                .OrderBy(p => p.ActorNumber)
                .Select(p => p.CustomProperties["uid"] as string)
                .Where(uid => !string.IsNullOrEmpty(uid))
                .ToList();

            SetGridOrder(uidList);

            // 시작 직후 보이는 초기 순위
            SeedInitialRanksFromGrid();
        }
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

        if (useCountdown && _roomStartTime.HasValue && PhotonNetwork.Time < _roomStartTime.Value)
        {
            return;
        }

        if (!useCountdown)
        {
            var props = new Hashtable {
            { JengaRoomProps.KEY_START_TIME, PhotonNetwork.Time },
            { JengaRoomProps.KEY_DURATION,   (double)gameTime }
        };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        // 1) 마스터 로컬 먼저 Playing 세팅
        Debug.Log("[JengaGameManager] Step 1: ApplyGameStateChange(Playing)");
        ApplyGameStateChange(JengaGameState.Playing);

        // 2) 전체에 전파
        Debug.Log("[JengaGameManager] Step 2: BroadcastGameState(Playing)");
        JengaNetworkManager.Instance.BroadcastGameState(JengaGameState.Playing);

        //    // 3) 타이머는 카운트다운이 완전히 끝난 후에만 시작
        //    Debug.Log("[JengaGameManager] Step 3: StartCoroutine(GameTimer)");
        //    if (!useCountdown && PhotonNetwork.IsMasterClient)
        //    {
        //        var props = new Hashtable
        //{
        //    { JengaRoomProps.KEY_START_TIME, PhotonNetwork.Time },
        //    { JengaRoomProps.KEY_DURATION,   (double)gameTime }
        //};
        //        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        //    }
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
        if (!PhotonNetwork.IsMasterClient) return;
        if (currentState == JengaGameState.Finished) return;


        if (useCountdown)
        {
            // 카운트다운 시작 전에, 모두가 공유할 "미래 시각"을 기록 (START_TIME 고정)
            double startAt = PhotonNetwork.Time + countdownDuration + 1.0; // "START!" 1초 표시 포함
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
            { JengaRoomProps.KEY_START_TIME, startAt },
            { JengaRoomProps.KEY_DURATION,   (double)gameTime }
        });

            // 네트워크 매니저를 통해 모든 클라이언트에게 카운트다운 시작 신호
            JengaNetworkManager.Instance?.BroadcastStartCountdown(countdownDuration);

            // 카운트다운 완료 후 게임 시작을 위한 코루틴
            StartCoroutine(CountdownToGameStart());
        }
        else
        {
            // 카운트다운 없이 바로 시작할 땐 지금 시각 기준
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
            { JengaRoomProps.KEY_START_TIME, PhotonNetwork.Time },
            { JengaRoomProps.KEY_DURATION,   (double)gameTime }
        });

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

        ApplyGameStateChange(JengaGameState.Playing);
        JengaNetworkManager.Instance?.BroadcastGameState(JengaGameState.Playing);
    }

    // 룸 프로퍼티(START_TIME, DURATION)로부터 남은 시간을 재계산해 UI에 반영
    public void ApplySyncedTimerFromRoomProps(double startTime, double durationSec)
    {
        // 현재 동기 시각
        _roomStartTime = startTime;
        _roomDuration = durationSec;

        double now = PhotonNetwork.Time;
        double elapsed = Math.Max(0.0, now - startTime); // 시작 전이면 0으로 고정
        remainingTime = Mathf.Max(0f, (float)(_roomDuration.Value - elapsed));

        OnTimeUpdated?.Invoke(remainingTime);
        TryStartRoomPropTimer();
    }

    // ROOM PROPS 미수신 시 재조회
    private IEnumerator GameTimerWatchdog()
    {
        float probe = 0f;
        while (currentState == JengaGameState.Playing && (!_roomStartTime.HasValue || !_roomDuration.HasValue))
        {
            probe += Time.unscaledDeltaTime;
            if (probe > 3f)
            {
                var table = PhotonNetwork.CurrentRoom?.CustomProperties;
                if (table != null &&
                    JengaRoomProps.TryGet<double>(table, JengaRoomProps.KEY_START_TIME, out var st) &&
                    JengaRoomProps.TryGet<double>(table, JengaRoomProps.KEY_DURATION, out var du))
                {
                    ApplySyncedTimerFromRoomProps(st, du);
                    TryStartRoomPropTimer();
                    yield break;
                }
                Debug.LogWarning("[JengaTimer] start/dur not set yet. Waiting...");
                probe = 0f;
            }
            yield return null;
        }
    }


    #endregion

    /// <summary>
    /// 동기화된 게임 상태를 내부에 적용하고 이벤트로 알림
    /// </summary>
    public void ApplyGameStateChange(JengaGameState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        OnGameStateChanged?.Invoke(currentState);

        // 마스터: 게임 상태를 룸 프로퍼티로도 기록
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
        {
            var props = new Hashtable { { JengaRoomProps.KEY_STATE, (int)currentState } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        // 상태에 따른 입력 잠금 제어
        switch (newState)
        {
            case JengaGameState.Playing:
                // 새 라운드 시작 시 혹시 남아있을 수 있는 잠금 해제
                _endGameLock?.Dispose();
                _endGameLock = null;
                TryStartRoomPropTimer();

                if (_timerWatchdog != null) StopCoroutine(_timerWatchdog);
                _timerWatchdog = StartCoroutine(GameTimerWatchdog());
                break;

            case JengaGameState.Finished:
                // 게임 종료: 젠가 상호작용 차단
                if (_endGameLock == null && InputManager.Instance != null)
                    _endGameLock = InputManager.Instance.Acquire(
                        InputType.Interaction,
                        "Jenga finished"
                    );

                StopRoomPropTimer();
                if (_timerWatchdog != null) { StopCoroutine(_timerWatchdog); _timerWatchdog = null; }
                JengaUIManager.Instance.HideRotateButton();
                break;
        }
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

            float elapsed;
            if (_roomStartTime.HasValue)
                elapsed = (float)(PhotonNetwork.Time - _roomStartTime.Value);
            else
                elapsed = Time.time - player.gameStartTime;

            player.lastSuccessTime = elapsed;

            Debug.Log($"[JengaRanking] SUCCESS uid={uid}, +{scoreGained} → score={player.score}, last={elapsed:0.00}s");
        }
        else
        {
            player.isAlive = false;

            // 탈락자는 자동 뒤로 밀리도록
            player.lastSuccessTime = float.MaxValue;

            Debug.Log($"[JengaRanking] FAIL uid={uid}, eliminated");

            // 젠가가 붕괴할 때만 완료 처리
            if (!playerFinished[uid])
            {
                playerFinished[uid] = true;
                OnPlayerFinished?.Invoke(uid);

                CheckAllPlayersFinished();
            }
        }
        OnPlayerAction?.Invoke(uid, success, scoreGained);

        if (PhotonNetwork.IsMasterClient)
        {
            var ranks = GetCurrentRanks();
            JengaNetworkManager.Instance?.BroadcastRankSnapshot(ranks);
        }
    }

    public void ApplyBlockRemovalSuccess(string uid, int scoreGained = 0)
    {
        if (!players.TryGetValue(uid, out var player)) return;

        // 점수/시간 처리
        ApplyPlayerActionResult(uid, true, scoreGained);

        // 제거 개수는 여기서만 증가 (타이밍 성공은 개수 아님)
        player.removedCount++;
        Debug.Log($"[JengaRanking] REMOVE++ uid={uid}, removedCount={player.removedCount}");
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
        // 1) 타이머 0으로 고정 & 즉시 UI 반영
        remainingTime = 0f;
        OnTimeUpdated?.Invoke(remainingTime);

        // 2) 상태 전환 (ApplyGameStateChange 내부에서 KEY_STATE를 룸 프로퍼티로 기록)
        ApplyGameStateChange(JengaGameState.Finished);

        // 3) 네트워크로 상태 브로드캐스트 (RPC)
        JengaNetworkManager.Instance.BroadcastGameState(JengaGameState.Finished); // 게임 상태를 "Finished"로 변경
        JengaNetworkManager.Instance.BroadcastTimeSync(0f); // 전 클라 타이머 0 표시

        // 4) 순위 계산 & UI/룸프로퍼티 반영(랭킹만 기록; KEY_STATE는 위에서 이미 기록됨)
        var rankings = CalculateRankings();
        JengaNetworkManager.Instance?.BroadcastRankSnapshot(rankings);
        OnGameFinished?.Invoke(rankings); // OnGameFinished로 외부에 알림

        // 마스터: 랭킹을 룸 프로퍼티에 기록
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
        {
            string[] uids = rankings.Keys.ToArray();
            int[] rks = rankings.Values.ToArray();

            var props = new Hashtable
        {
            { JengaRoomProps.KEY_RANK_UIDS, uids },
            { JengaRoomProps.KEY_RANK_VALS, rks  },
        };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        // 5) 메인 게임에 결과 전달
        SendResultToMainGame(rankings);
    }

    // 어디서든 랭킹이 확정/수신될 때 캐싱
    public void ApplyRankSnapshot(Dictionary<string, int> ranks)
    {
        _lastRankSnapshot = new Dictionary<string, int>(ranks);
        OnRankingsUpdated?.Invoke(ranks);
    }

    private Dictionary<string, int> CalculateRankings()
    {
        // 점수순으로 정렬, 동점일 경우 생존 여부로 판단
        var sortedPlayers = players
        .OrderByDescending(p => IsActive(p.Value))      // 활동 여부: 활동한 사람(true) 먼저 (혹시나 전부 잠수타서 움직이지 않는 경우 대비)
        .ThenByDescending(p => p.Value.removedCount)    // 1순위 : 젠가 블록을 더 많이 뺀 사람
        .ThenBy(p => p.Value.lastSuccessTime)           // 2순위 : 동점일 때 제거를 더 빨리 성공(작을수록 유리)
        .ThenByDescending(p => p.Value.score)           // 3순위 : 점수 큰 사람
        .ThenBy(p => GridOrderOf(p.Key))                // 4순위 : 초기 그리드
        .ToList();

        // 딕셔너리 형태로 UID별 순위를 저장
        // { "playerA": 1, "playerB": 2, "playerC": 3, "playerD": 4 }
        var rankings = new Dictionary<string, int>();
        for (int i = 0; i < sortedPlayers.Count; i++)
            rankings[sortedPlayers[i].Key] = i + 1;

        Debug.Log("[JengaRanking] RANKINGS: " + string.Join(", ",
            sortedPlayers.Select(p =>
                $"{p.Key}: rank={rankings[p.Key]}, removed={p.Value.removedCount}, score={p.Value.score}, last={p.Value.lastSuccessTime:0.00}, alive={p.Value.isAlive}"
            )));

        return rankings;
    }



    /// <summary>
    /// 점수 순위를 메인 게임 시스템에 전달하고,
    /// 플레이어의 승리 여부를 업데이트한 뒤 메인 씬으로 복귀 준비
    /// </summary>
    private void SendResultToMainGame(Dictionary<string, int> rankings)
    {
        // 메인 게임에 결과 전달 ("Jenga"라는 키로 결과 저장)
        MainGameManager.Instance.ReportMiniGameResult(rankings);

        // 메인 게임의 PlayerManager를 통한 순위 업데이트
        foreach (var pair in rankings)
        {
            // PlayerManager를 통해 실제 플레이어 오브젝트를 찾기
            var player = PlayerManager.Instance.GetPlayer(pair.Key);
            if (player != null)
            {
                // gamePlayer.WinThisMiniGame = (1등인지 여부) 설정
                //player.WinThisMiniGame = pair.Value == 1;
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
            JengaTowerManager.Instance?.CleanupAllProxies();

            yield return new WaitForSeconds(1.0f);

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
        // 룸 프로퍼티가 들어올 때까지(마스터가 기록하기 전 상황) 잠시 대기
        while (!_roomStartTime.HasValue || !_roomDuration.HasValue)
            yield return null;


        while (currentState == JengaGameState.Playing)
        {
            double end = _roomStartTime.Value + _roomDuration.Value;
            remainingTime = Mathf.Max(0f, (float)(end - PhotonNetwork.Time));
            OnTimeUpdated?.Invoke(remainingTime);

            if (remainingTime <= 0f) break;
            yield return new WaitForSeconds(1f);
        }

        // 시간이 다 되면 EndGame() 호출
        if (currentState == JengaGameState.Playing)
        {
            EndGame();
        }
    }

    public void TryStartRoomPropTimer()
    {
        if (currentState != JengaGameState.Playing) return;
        if (!_roomStartTime.HasValue || !_roomDuration.HasValue) return;
        if (_timerCo != null) return;               // 이미 돌고 있으면 스킵


        if (PhotonNetwork.Time < _roomStartTime.Value)
        {
            // 아직 시작시각 전 -> 먼저 기다렸다가 시작
            _timerCo = StartCoroutine(CoWaitUntilStartThenRun());
            return;
        }

        _timerCo = StartCoroutine(GameTimer());
    }

    private IEnumerator CoWaitUntilStartThenRun()
    {
        while (PhotonNetwork.Time < _roomStartTime.Value)
            yield return null;
        _timerCo = StartCoroutine(GameTimer());
    }

    public void StopRoomPropTimer()
    {
        if (_timerCo != null) { StopCoroutine(_timerCo); _timerCo = null; }
    }

    // UI에서 현재 남은 시간을 가져올 수 있는 퍼블릭 메서드
    public float GetRemainingTime() => remainingTime;
    public string GetFormattedTime()
    {
        // 반올림으로 더 정확한 시간 표시
        int totalSeconds = Mathf.RoundToInt(remainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:00}";
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

    #region 게임 시작 시 랭킹 그리드 순서 설정

    /// <summary>
    /// 마스터가 게임 시작 시 그리드 순서 확정
    /// </summary>
    /// <param name="uidList"></param>
    public void SetGridOrder(IList<string> uidList)
    {
        _gridOrder.Clear();
        for (int i = 0; i < uidList.Count; i++) _gridOrder[uidList[i]] = i;
    }

    /// <summary>
    /// 그리드 순서에 따라 초기 랭킹 설정
    /// </summary>
    public void SeedInitialRanksFromGrid()
    {
        if (_gridOrder.Count == 0) return;

        var rankMap = _gridOrder
            .OrderBy(kv => kv.Value)
            .Select((kv, idx) => new { kv.Key, Rank = idx + 1 })
            .ToDictionary(x => x.Key, x => x.Rank);

        _lastRankSnapshot = new Dictionary<string, int>(rankMap);
        // UI/네트워크에 즉시 반영
        OnRankingsUpdated?.Invoke(rankMap);
        JengaNetworkManager.Instance?.BroadcastRankSnapshot(rankMap);

        // 룸 프로퍼티에도 기록해서 늦게 들어온 클라 동기화
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
        {
            var uids = rankMap.Keys.ToArray();
            var ranks = rankMap.Values.ToArray();
            var props = new Hashtable
            {
                { JengaRoomProps.KEY_RANK_UIDS, uids },
                { JengaRoomProps.KEY_RANK_VALS, ranks },
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
    }

    // 필요 시 UI가 꺼내갈 수 있게 getter 제공
    public bool TryGetLastRankSnapshot(out Dictionary<string, int> ranks)
    {
        if (_lastRankSnapshot != null)
        {
            ranks = new Dictionary<string, int>(_lastRankSnapshot);
            return true;
        }
        ranks = null;
        return false;
    }

    #endregion 


    #region 강제 정리

    protected override void OnDestroy()
    {
        Debug.Log("[JengaGameManager] OnDestroy - cleaning up resources");

        // 1. 이벤트 해제 (메모리 누수 방지)
        OnTimeUpdated = null;
        OnGameStateChanged = null;
        OnPlayerAction = null;
        OnPlayerFinished = null;
        OnGameFinished = null;

        // 2. 전역 플레이어 데이터에서 젠가 관련 데이터만 정리
        if (PlayerManager.Instance != null)
        {
            foreach (var player in PlayerManager.Instance.Players.Values)
            {
                if (player != null)
                {
                    player.JengaData = null;
                   // player.WinThisMiniGame = false;
                }
            }
        }

        // 3. 룸 프로퍼티에서 젠가 관련 데이터 제거 (다음 게임을 위해)
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom && currentState == JengaGameState.Finished)
        {
            var clearProps = new Hashtable
            {
                { JengaRoomProps.KEY_STATE, null },
                { JengaRoomProps.KEY_START_TIME, null },
                { JengaRoomProps.KEY_DURATION, null },
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(clearProps);
        }

        base.OnDestroy();
    }

    #endregion
}