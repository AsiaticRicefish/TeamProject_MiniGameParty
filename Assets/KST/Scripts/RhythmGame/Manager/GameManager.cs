using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DesignPattern;
using LDH_MainGame;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;


namespace RhythmGame
{
    // public class GameManager : CombinedSingleton<GameManager>
    public class GameManager : PunSingleton<GameManager>, IGameComponent
    {
        // 게임 시간 관리
        [SerializeField] float gameTime = 180f; //게임 플레이타임
        [SerializeField] float countDown = 3f; //카운트 다운
        [SerializeField] float delayTime = 3f; //잔여 노트들이 남아있는 시간(임시)
        //TODO 김승태 게임 플레이 시간 (임시) 변경 예정
        [SerializeField] TMP_Text gameTimer;
        public bool IsGameStart = false;
        public bool IsGameOver = false;
        public event Action OnGameStart; //게임 시작 이벤트
        public event Action OnGameOver; //게임 오버 여부에 따른 이벤트
        public event Action<double, double> OnTimer;
        Coroutine _waitStartCo;
        Coroutine _waitEndCo;

        //게임 규칙
        int missScore = -1; // 미스 시 감점 점수

        // //과열 관리
        // int overHeatValue = 0;// 마스터가 유지하는 공유 과열 값
        // public bool IsOverHeat = false; //과열여부
        // public event Action OnIsOverHeat; // 과열 발생
        // [SerializeField] float overHeatingTime = 3f; //과열 유지 시간

        //플레이어 자리
        [SerializeField] Transform[] playerPoints;
        [SerializeField] Transform[] playerVerdictPoints;
        // //노트 스폰 오프셋
        // [SerializeField] float noteSpawnDist = 12f;

        [Header("플레이어 프리팹 이름")]
        [SerializeField] string playerPrefabName = "Prefabs/Rhythm/RhythmUnimo";
        [SerializeField] string backupPrefabName = "Prefabs/Rhythm/RhythmPlayer"; //테스트용
        [SerializeField] string playerVerdictPrefab = "Prefabs/Rhythm/VerdictModel"; //테스트용
        [SerializeField] Vector3 tempSpawnPos = Vector3.zero; // 임시 스폰 위치

        private Dictionary<string, RhythmPlayerData> players = new(); // UID를 key로 가지는 플레이어 데이터
        private Dictionary<string, int> playerScores = new();        // 플레이어별 점수
        private readonly Dictionary<string, int> _gridOrder = new(); // uid -> 0,1,2,...
        private Dictionary<string, int> _lastRankSnapshot;
        public Action<Dictionary<string, int>> OnRankingsUpdated; // 실시간 순위 갱신 이벤트
        Dictionary<string, int> _totalScores = new();
        Dictionary<string, int> _perfectCounts = new();
        bool _receivedRank;

        int _songIndex = -1;

        protected override void OnDestroy()
        {
            // 사운드 전체 정리
            SoundManager.Instance.StopAllSounds();
        }
        protected override void Awake()
        {
            base.Awake();
            Application.targetFrameRate = 60; // 60fps 고정
        }

        public void Initialize()
        {
            InitializePlayers();
            //테스트 환경에서 리소스 없는 것을 방지
            var prefab = Resources.Load<GameObject>(playerPrefabName);
            var verdictPrefab = Resources.Load<GameObject>(playerVerdictPrefab);
            if (prefab == null)
            {
                Debug.Log($"{playerPrefabName}가 없어서 {backupPrefabName}로 플레이어 캐릭터 모델 변경 ");
                playerPrefabName = backupPrefabName;
            }
            if (verdictPrefab == null)
                Debug.Log($"{verdictPrefab}가 없습니다.");


            // 캐릭터 생성
            PhotonNetwork.Instantiate(playerPrefabName, tempSpawnPos, Quaternion.identity);
            //판정바 생성
            PhotonNetwork.Instantiate(playerVerdictPrefab, tempSpawnPos, Quaternion.identity);
        }

        #region 게임 시작 종료 로직
        /// <summary>
        /// 마스터가 전담하여 게임 로직 실행
        /// 라인 배정 → 스폰 → 타이머 시작
        /// </summary>
        public void StartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            StartCoroutine(IE_StartGame());
        }

        private IEnumerator IE_StartGame()
        {
            // 플레이어 자리 배정
            LaneManager.Instance.SetLane();

            double startTime = PhotonNetwork.Time + countDown;

            // 곡 선택
            int songIndex = UnityEngine.Random.Range(1, 4);
            _songIndex = songIndex;

            string songName = $"RhythmBgm{songIndex}";

            //  곡 길이 비동기 조회
            var lengthTask = SoundManager.Instance.GetMusicLengthAsync(songName, gameTime);
            while (!lengthTask.IsCompleted) yield return null;

            float musicLength = Mathf.Max(0.01f, lengthTask.Result);

            double stopSpawnTime = startTime + Math.Max(0.0, musicLength);

            double endTime = startTime + musicLength + delayTime;

            // 스폰 초기화
            NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_InitStart), RpcTarget.All, startTime);

            photonView.RPC(nameof(PRC_StartGameTIme), RpcTarget.All, startTime, endTime, songIndex);

            photonView.RPC(nameof(RPC_ScheduleStopSpawn), RpcTarget.All, stopSpawnTime);
        }

        [PunRPC]
        void PRC_StartGameTIme(double startTime, double endTime, int songIndex)
        {
            _songIndex = songIndex;
            OnTimer?.Invoke(startTime, endTime);

            if (_waitStartCo != null) StopCoroutine(_waitStartCo);
            if (_waitEndCo != null) StopCoroutine(_waitEndCo);

            _waitStartCo = StartCoroutine(IE_WaitStart(startTime, endTime));
        }

        IEnumerator IE_WaitStart(double startTime, double endTime)
        {
            while (PhotonNetwork.Time < startTime) yield return null;


            if (PhotonNetwork.IsMasterClient)
            {
                // NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_InitStart), RpcTarget.All, startTime);
                photonView.RPC(nameof(GameStartSettings), RpcTarget.All);

                NoteSpawner.Instance.photonView.RPC(nameof(NoteSpawner.RPC_StartSpawn), RpcTarget.All);
            }
            _waitEndCo = StartCoroutine(IE_WaitEnd(endTime));
        }

        IEnumerator IE_WaitEnd(double endTime)
        {
            while (PhotonNetwork.Time < endTime) yield return null;

            EndGame();
        }

        [PunRPC]
        void RPC_ScheduleStopSpawn(double stopSpawnTime)
        {
            StartCoroutine(IE_WaitStopSpawn(stopSpawnTime));
        }

        IEnumerator IE_WaitStopSpawn(double stopSpawnTime)
        {
            while (PhotonNetwork.Time < stopSpawnTime) yield return null;

            NoteSpawner.Instance.StopSpawn();
            SoundManager.Instance.StopBGM();
            SoundManager.Instance.StopAllSounds();
        }

        [PunRPC]
        public void GameStartSettings()
        {
            if (IsGameStart) return;
            //게임 시작 플래그 설정
            IsGameStart = true;

            SoundManager.Instance.PlayBGM($"RhythmBgm{_songIndex}");


            OnGameStart?.Invoke();
        }

        /// <summary>
        /// 게임 종료 시
        /// </summary>
        [PunRPC]
        public void RPC_EndGame()
        {
            if (!IsGameStart) return;

            IsGameStart = false;
            IsGameOver = true;

            if (PhotonNetwork.IsMasterClient)
            {
                var rankings = CalculateRanks();
                BroadcastRankSnapshot(rankings);
                SendResultToMainGame(rankings);
            }


            //게임 종료 이벤트 호출
            OnGameOver?.Invoke();
            Debug.Log("게임 오버");

            MainGameManager.Instance.NotifyMiniGameFinish();
        }

        public void EndGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!IsGameStart || IsGameOver) return;

            photonView.RPC(nameof(RPC_EndGame), RpcTarget.All);
        }
        #endregion


        /// <summary>
        /// Good 히트 → 개인 점수 증감
        /// </summary>
        public void GoodHitScore(NoteType type, Player actor)
        {
            if (!PhotonNetwork.IsMasterClient || actor == null) return;

            int score = CalculateNote(type);
            Debug.Log($"판정 노트 타입 {type}");
            ScoreManager.Instance.photonView.
            RPC(nameof(ScoreManager.AddScore), RpcTarget.All, actor.ActorNumber, score);

        }

        //판정 관련 로직, NoteType에 따라 점수 반영 다르도록
        public int CalculateNote(NoteType type)
        {
            int score = 0;
            switch (type)
            {
                case NoteType.Fake:
                    score = -1;
                    // OverHeatCheck();
                    break;

                case NoteType.Touch:
                    score = 17;
                    // FrozenHeat();
                    break;

                case NoteType.Continue:
                    score = 31;
                    // FrozenHeat();
                    break;
            }

            return score;
        }

        // public void FrozenHeat()
        // {
        //     if (!PhotonNetwork.IsMasterClient) return;

        //     // 과열 변수 값 감소
        //     overHeatValue = Mathf.Max(0, overHeatValue - frozenPoint);

        //     //과열 값 반영
        //     ScoreManager.Instance.photonView.RPC(
        //         nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue
        //         );


        // }

        /// <summary>
        /// 과열 증가 로직
        /// 미스(Miss)시 혹은 타 조건 만족 시 해당 로직 호출
        /// </summary>
        // public void OverHeatCheck()
        // {
        //     //마스터 클라이언트만 판별하도록
        //     if (!PhotonNetwork.IsMasterClient) return;

        //     // 과열 변수 값 증가
        //     overHeatValue = Mathf.Max(0, overHeatValue + overHeatPoint);

        //     //과열 값 반영
        //     ScoreManager.Instance.photonView.RPC(
        //         nameof(ScoreManager.SetOverheat), RpcTarget.All, overHeatValue
        //         );

        //     // 과열 최대치 도달했을 경우
        //     if (overHeatValue >= overHeatMaxValue)
        //     {
        //         // ScoreManager.Instance.photonView.RPC(nameof(ScoreManager.RPC_IsOverHeat), RpcTarget.All);

        //         //과열 코루틴 실행
        //         StartCoroutine(IE_OverHeating());
        //     }
        // }

        /// <summary>
        /// 미스 시 개인점수 차감
        /// </summary>
        public void MissBlock(Player actor, NoteType type)
        {
            if (type == NoteType.Fake)
            {
                ScoreManager.Instance.photonView.
                                RPC(nameof(ScoreManager.AddScore), RpcTarget.All, actor.ActorNumber, 9);
            }
            else
            {
                //개인 점수 차감
                ScoreManager.Instance.photonView.
                RPC(nameof(ScoreManager.MinusScore), RpcTarget.All, actor.ActorNumber, missScore);
            }
        }

        public int LaneCapacity => playerPoints?.Length ?? 0;

        public void PlaceActorToLane(int actorNumber, int lane)
        {
            //플레이어 컨트롤러가 액터 넘버 기준으로 딕셔너리에 등록돼 있는지 확인
            if (!PlayerController.AvatarByActor.TryGetValue(actorNumber, out var t))
            {
                //아닐 경우 코루틴으로 지연 후 확인
                StartCoroutine(IE_DelayPlace(actorNumber, lane));
                return;
            }
            //플레이어 컨트롤러가 액터 넘버 기준으로 딕셔너리에 등록돼 있는지 확인
            if (!PlayerVerdict.VerdictByActor.TryGetValue(actorNumber, out var vt))
            {
                //아닐 경우 코루틴으로 지연 후 확인
                StartCoroutine(IE_DelayVerdictPlace(actorNumber, lane));
                return;
            }
            //lane 인덱스 초과 방지
            int idx = Mathf.Clamp(lane - 1, 0, playerPoints.Length - 1);

            //해당 인덱스의 플레이어 위치 가져오기
            var p = playerPoints[idx];
            var vp = playerVerdictPoints[idx];

            //아바타 위치 해당 위치로 이동
            t.SetPositionAndRotation(p.position, p.rotation);
            vt.SetPositionAndRotation(vp.position, vp.rotation);
        }


        IEnumerator IE_DelayPlace(int actorNumber, int lane)
        {
            //최대 10번 시도
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.1f);

                //아바타가 딕셔너리에 등록돼 있다면 배치 진행
                if (PlayerController.AvatarByActor.TryGetValue(actorNumber, out var t))
                {
                    int idx = Mathf.Clamp(lane - 1, 0, playerPoints.Length - 1);
                    var p = playerPoints[idx];
                    t.SetPositionAndRotation(p.position, p.rotation);
                    yield break;
                }
            }
            Debug.LogWarning($"액터넘버 : {actorNumber} 아바타를 찾지 못했습니다.");
        }
        IEnumerator IE_DelayVerdictPlace(int actorNumber, int lane)
        {
            //최대 10번 시도
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.1f);

                //아바타가 딕셔너리에 등록돼 있다면 배치 진행
                if (PlayerVerdict.VerdictByActor.TryGetValue(actorNumber, out var t))
                {
                    int idx = Mathf.Clamp(lane - 1, 0, playerVerdictPoints.Length - 1);
                    var p = playerVerdictPoints[idx];
                    t.SetPositionAndRotation(p.position, p.rotation);
                    yield break;
                }
            }
            Debug.LogWarning($"액터넘버 : {actorNumber} 판정바를 찾지 못했습니다.");
        }

        public Pose GetLaneSpawnPose(int lane)
        {
            int idx = Mathf.Clamp(lane - 1, 0, playerPoints.Length - 1);
            var p = playerPoints[idx];

            //위치
            // Vector3 pos = p.position + p.forward * noteSpawnDist;
            Vector3 pos = new Vector3(p.position.x, p.position.y, 0f) + p.forward * NoteSpawner.Instance.transform.position.z;
            //회전
            Quaternion rot = Quaternion.LookRotation(p.forward, Vector3.up);

            //위치, 회전
            return new Pose(pos, rot);
        }

        private void InitializePlayers()
        {
            // 현재 방에 접속해 있는 모든 Photon 플레이어 목록을 순회
            foreach (var photonPlayer in PhotonNetwork.PlayerList)
            {
                // PhotonNetwork.PlayerList에서 꺼낸 플레이어 객체의 CustomProperties에서 uid (Firebase UID)를 추출
                string uid = photonPlayer.CustomProperties["uid"] as string;

                if (string.IsNullOrEmpty(uid))
                {
                    Debug.LogWarning($"[RhythmGameManager - InitializePlayers] Player {photonPlayer.NickName} has no UID in CustomProperties");
                    continue;
                }

                // CreateOrGetPlayer를 사용하여 플레이어가 없으면 자동 생성
                var gamePlayer = PlayerManager.Instance.CreateOrGetPlayer(uid, photonPlayer.NickName);

                if (gamePlayer != null)
                {
                    // GamePlayer에 미니게임 전용 데이터인 RhythmPlayerData를 새로 만들어 할당
                    gamePlayer.RhythmPlayerData = new RhythmPlayerData
                    {
                        score = 0
                    };

                    // RhythmGameManager의 players 딕셔너리에 UID를 key로 사용해서 RhythmPlayerData를 등록
                    players[uid] = gamePlayer.RhythmPlayerData;
                    // 점수를 저장하는 playerScores 딕셔너리에도 해당 UID로 0점 등록 (초기값)
                    playerScores[uid] = 0;
                    Debug.Log($"[RhythmGameManager - InitializePlayers] Successfully initialized player: {uid} ({photonPlayer.NickName})");
                }
                else
                {
                    Debug.LogError($"[RhythmGameManager - InitializePlayers] {uid}에 해당하는 GamePlayer를 찾을 수 없음");
                }
            }
            Debug.Log($"[RhythmGameManager - InitializePlayers] Initialized {players.Count} players");

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
            BroadcastRankSnapshot(rankMap);

            // 룸 프로퍼티에도 기록해서 늦게 들어온 클라 동기화
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                var uids = rankMap.Keys.ToArray();
                var ranks = rankMap.Values.ToArray();
                var props = new Hashtable
            {
                { RhythmRoomProps.KEY_RANK_UIDS, uids },
                { RhythmRoomProps.KEY_RANK_VALS, ranks },
            };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
        }

        [PunRPC]
        public void RPC_ReceiveScore(string uid, int score, int verdictScore, int perfectCount)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (string.IsNullOrEmpty(uid)) return;

            // 합산 스코어 집계
            int total = score + verdictScore;
            _totalScores[uid] = total;
            _perfectCounts[uid] = perfectCount;

            // 옵션: 내부 플레이어 데이터에도 보관(원하면)
            if (players.TryGetValue(uid, out var rp)) rp.score = total;

            // 랭킹 계산/브로드캐스트
            BroadcastRanks();
        }
        private Dictionary<string, int> CalculateRanks()
        {
            // players 기준으로 빠진 UID는 0점으로 취급
            foreach (var uid in players.Keys)
            {
                if (!_totalScores.ContainsKey(uid)) _totalScores[uid] = 0;
                if (!_perfectCounts.ContainsKey(uid)) _perfectCounts[uid] = 0;
            }
            // 정렬: 합산점수 내림차순 → 그리드 순서(안정화)
            var ordered = _totalScores
                .OrderByDescending(kv => kv.Value)
                .ThenByDescending(kv => _perfectCounts.TryGetValue(kv.Key, out var pc) ? pc : 0)
                .ThenBy(kv => _gridOrder.TryGetValue(kv.Key, out var ord) ? ord : int.MaxValue)
                .ToList();

            var ranks = new Dictionary<string, int>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                ranks[ordered[i].Key] = i + 1;

            return ranks;
        }

        private void BroadcastRanks()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var ranks = CalculateRanks();

            BroadcastRankSnapshot(ranks);
            // OnRankingsUpdated?.Invoke(ranks);
            // _lastRankSnapshot = new Dictionary<string, int>(ranks);
        }

        public void BroadcastRankSnapshot(Dictionary<string, int> uidToRank)
        {
            if (!PhotonNetwork.IsMasterClient || uidToRank == null) return;

            var uids = uidToRank.Keys.ToArray();
            var vals = uidToRank.Values.ToArray();

            // 1) RPC 전파
            photonView.RPC(nameof(RPC_SyncRanks), RpcTarget.Others, uids, vals);
            RPC_SyncRanks(uids, vals);

            // 2) 룸 프로퍼티 저장(레이트 조인 대비)
            var table = new Hashtable
            {
                { RhythmRoomProps.KEY_RANK_UIDS, uids },
                { RhythmRoomProps.KEY_RANK_VALS, vals },
            };

            PhotonNetwork.CurrentRoom?.SetCustomProperties(table);
        }

        [PunRPC]
        public void RPC_SyncRanks(string[] uids, int[] vals)
        {
            var ranks = new Dictionary<string, int>(uids.Length);
            for (int i = 0; i < uids.Length && i < vals.Length; i++)
                ranks[uids[i]] = vals[i];

            _lastRankSnapshot = new Dictionary<string, int>(ranks);
            OnRankingsUpdated?.Invoke(ranks);
            _receivedRank = true;
        }

        // 레이트 조인용 (방 커스텀 프로퍼티 갱신 수신)
        public override void OnRoomPropertiesUpdate(Hashtable props)
        {
            if (_receivedRank) return;

            if (props.TryGetValue(RhythmRoomProps.KEY_RANK_UIDS, out var uObj) &&
                props.TryGetValue(RhythmRoomProps.KEY_RANK_VALS, out var vObj) &&
                uObj is string[] uids && vObj is int[] vals)
            {
                RPC_SyncRanks(uids, vals);
                _receivedRank = true;
            }
        }
        public bool TryGetLastRankSnapshot(out Dictionary<string, int> ranks)
        {
            if (_lastRankSnapshot != null && _lastRankSnapshot.Count > 0)
            {
                ranks = new Dictionary<string, int>(_lastRankSnapshot);
                return true;
            }
            ranks = null;
            return false;
        }

        /// <summary>
        /// 메인 게임에 최종 승패 정보를 반영
        /// (PlayerManager의 Player.WinThisMiniGame 플래그를 세팅)
        /// </summary>
        private void SendResultToMainGame(Dictionary<string, int> rankings)
        {
            MainGameManager.Instance.ReportMiniGameResult(rankings);
            // foreach (var pair in rankings)
            // {
            //     var player = PlayerManager.Instance.GetPlayer(pair.Key);
            //     if (player != null)
            //     {
            //         // player.WinThisMiniGame = (pair.Value == 1); // 1등이면 승리
            //     }
            // }
        }


    }
}