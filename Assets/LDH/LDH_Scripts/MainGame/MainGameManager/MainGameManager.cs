using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;
using static LDH_Util.Define_LDH;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace LDH_MainGame
{
    [RequireComponent(typeof(PhotonView))]
    public class MainGameManager : PunSingleton<MainGameManager>, IGameComponent
    {
        [Header("Mini Games")] public MiniGameRegistry registry;

        //------- Controllers -----------//
        public MainGame_PropertiesController PropertiesCtrl;
        public MainGame_UIBinder UI;
        public MainGame_StateMachine FSM;
        public MiniGameLoader Loader;


        //--------- Events ------------//
        public Action OnGameStart;
        public Action<int> OnRoundChanged;
        public Action OnPicking;
        public Action OnPicked;
        public Action OnWaitAllReady;
        public Action OnLoadingMiniGame;
        public Action OnEndMiniGame;
        public Action OnEndGame;


        //------ Local variables --------//
        private bool IsMaster => PhotonNetwork.IsMasterClient;

        // flag
        private bool _isLeavingRoom = false;
        private bool _finalRewardsDistributed = false; // 중복 방지

        private int _localSlot = -1;
        private Coroutine _stateRoutine;


        protected override void OnAwake()
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            MainGameSceneController.Instance.Register(gameObject);


            //변수 초기화
            InitPlayerScores();


            base.OnAwake();
        }

        public void Initialize()
        {
            Util_LDH.ConsoleLog(this, "MainGameManager 초기화 로직 실행");

            PropertiesCtrl = new(
                () => PhotonNetwork.InRoom,
                () => PhotonNetwork.NetworkClientState == ClientState.Joined,
                () => !_isLeavingRoom);

            UI = new(registry, (s => _localSlot = s), (OnClickReady));
            FSM = new(
                PropertiesCtrl, UI, registry,
                () => IsMaster,
                () => _localSlot,
                round => OnRoundChanged?.Invoke(round),
                miniGame => registry.GetSceneName(miniGame.id),
                (ien) => StartCoroutine(ien),
                c =>
                {
                    if (c != null) StopCoroutine(c);
                },
                CheckEndCondition,
                photonView
            );


            //플레이어 매니저에 플레이어 등록
            Debug.Log("[MainGameManager] PlayerManager에 플레이어를 등록합니다.");
            Manager.Player.ClearAllPlayers();
            Manager.Player.EnsureAllPhotonPlayersRegistered();
        }

        private void InitPlayerScores()
        {
            foreach (var kv in PlayerManager.Instance.Players)
            {
                kv.Value.Score = 0;
            }
        }

        private bool CheckEndCondition()
        {
            return PlayerManager.Instance.Players.Values.Any(p => p.Score >= 2);
        }

        private void ResetRoundWinnerFlag()
        {
            foreach (GamePlayer gp in PlayerManager.Instance.Players.Values)
            {
                gp.WonThisRound = false;
            }
        }

        public void StartGame()
        {
            // 필수 서비스 준비 확인
            if (PropertiesCtrl == null || FSM == null || UI == null)
            {
                Debug.LogError("[MainGameManager] StartGame() called before Initialize() — abort.");
                return; // 또는 Initialize() 호출 후 재시도 로직을 넣어도 됨
            }

            Util_LDH.ConsoleLog(this, "게임을 시작합니다. (Enter 'Intro' State)");
            OnGameStart?.Invoke();

            if (IsMaster)
                PropertiesCtrl.SetRoomProps(new Dictionary<string, object>
                    {
                        { RoomProps.Round, 1 },
                        { RoomProps.MiniGameId, "" },
                        { RoomProps.State, MainState.Intro.ToString() }
                    }
                );
            OnRoundChanged?.Invoke(1);
        }

        #region MiniGame이 사용하는 API

        public void NotifyMiniGameStart()
        {
            if (PhotonNetwork.IsMasterClient)
                PropertiesCtrl.SetRoomProps(RoomProps.State, MainState.PlayingMiniGame.ToString());
        }

        public async void NotifyMiniGameFinish()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_ShowEndGame), RpcTarget.AllViaServer);
            await UniTask.Delay(TimeSpan.FromSeconds(2.8f));
            PropertiesCtrl.SetRoomProps(RoomProps.State, MainState.UnloadingMiniGame.ToString());
        }

        /// <summary>
        /// GameResultData에 미니게임 결과를 저장
        /// 모든 클라이언트에서 호출 가능
        /// </summary>
        /// <param name="rankings"></param>
        public void ReportMiniGameResult(Dictionary<string, int> rankings)
        {
            if (rankings == null || rankings.Count == 0) return;

            int round = PropertiesCtrl.GetRoomProps(Define_LDH.RoomProps.Round, 1);
            string gameId = PropertiesCtrl.GetRoomProps(Define_LDH.RoomProps.MiniGameId, ""); // or registry로 표시명 변환
            string gameName = registry.GetGameName(gameId);
            GameResultData.SetRoundResult(round, gameId, gameName, rankings);
        }

        #endregion


        #region Photon callbacks

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed == null || changed.Count == 0) return;

            if (changed.ContainsKey(RoomProps.Round))
            {
                OnRoundChanged?.Invoke(PropertiesCtrl.GetRoomProps(RoomProps.Round, 1));
            }

            // 상태 변경 반영
            SyncGameState();
        }

        public override void OnPlayerPropertiesUpdate(Player target, Hashtable changedProps)
        {
            // UI Ready 표시 갱신: PlayerProps 기반으로 계산해서 UI에만 전달
            int readyMask = PropertiesCtrl.BuildReadyMaskFromPlayers();
            UI.UpdateReady(readyMask);

            if (IsMaster && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 1)
            {
                if (changedProps.ContainsKey(PlayerProps.InGameReady) &&
                    FSM.Get() == MainState.Ready &&
                    PropertiesCtrl.AllPlayersReady())
                {
                    PropertiesCtrl.SetRoomProps(RoomProps.State, MainState.LoadingMiniGame.ToString());
                }

                if (changedProps.ContainsKey(PlayerProps.InGameDone) )
                {
                    if(FSM.Get() == MainState.UnloadingMiniGame)
                        FSM.CheckAllPlayerUnloadingDone();
                    else if(FSM.Get() == MainState.Intro)
                        FSM.CheckAllPlayerIntroDone();
                    else if (FSM.Get() == MainState.Picking)
                        FSM.CheckAllPlayerPickingDone();
                }

                if (changedProps.ContainsKey(PlayerProps.InGameResultDone) && FSM.Get() == MainState.ApplyingResult)
                {
                    FSM.CheckNextState();
                }
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            //현재 room이 아니거나, joined 상태가 아니거나, leaving room 중이라면 패스
            if (!PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState != ClientState.Joined ||
                _isLeavingRoom) return;

            // 현재 방 가져오기
            var room = PhotonNetwork.CurrentRoom;
            if (room == null) return; // 방이 없다면 패스

            //누구든 나갔을 때 
            UI.ShowQuitPopup();

            // 2) 마스터 클라이언트이고, 메인 게임 상태가 ready(모든 플레이어의 ready를 기다리고 있는 상태)라면 재조정
            if (!IsMaster) return;
            if (FSM.Get() != MainState.Ready) return;

            // 준비 단계에서 누가 나가도, 남은 인원 기준 AllPlayersReady면 진행
            if (FSM.Get() == MainState.Ready && PropertiesCtrl.AllPlayersReady() &&
                PhotonNetwork.CurrentRoom.PlayerCount > 1)
            {
                PropertiesCtrl.SetRoomProps(RoomProps.State, MainState.LoadingMiniGame.ToString());
            }
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (!IsMaster) return;

            //새로 마스터가 된 플레이어의 슬롯을 재배정한다.(0번으로)
            if (newMasterClient.IsLocal)
            {
                Debug.Log("[MainGameManager] 새롭게 마스터가 된 클라이언트의 슬롯 인덱스를 갱신합니다. : 0번 슬롯으로");
                PropertiesCtrl.SetSlotIndex(0);
                _localSlot = 0;
            }


            if (FSM.Get() == MainState.Ready && PropertiesCtrl.AllPlayersReady() &&
                PhotonNetwork.CurrentRoom.PlayerCount > 1)
            {
                PropertiesCtrl.SetRoomProps(RoomProps.State, MainState.LoadingMiniGame.ToString());
            }
        }

        #endregion


        #region Main Logic - FSM

        private void SyncGameState()
        {
            var nextState = FSM.ReadOrDefault();

            if (FSM.Changed(nextState))
            {
                SwitchState(nextState);
            }
        }

        /// <summary>
        /// 룸 프로퍼티 변경에 대해 스테이트가 바뀔 때만 호출해야 한다.
        /// </summary>
        private void SwitchState(MainState nextState)
        {
            Debug.Log($"{nextState.ToString()}으로 상태 변경");
            if (_stateRoutine != null)
            {
                StopCoroutine(_stateRoutine);
                _stateRoutine = null;
            }

            Util_LDH.ConsoleLog(this, $"State가 변경되었습니다. (Enter '{nextState.ToString()}' State)");

            FSM.Set(nextState);
            switch (nextState)
            {
                case MainState.Intro:
                    _stateRoutine = StartCoroutine(FSM.Co_Intro());
                    break;
                case MainState.Picking:
                    PropertiesCtrl.ClearLocalInGameProperties();
                    ResetRoundWinnerFlag();
                    UI.CloseIntroScreen().Forget();
                    _stateRoutine = StartCoroutine(FSM.Co_Picking());
                    break;
                case MainState.Ready:
                    UI.CloseSlotMachine().Forget();
                    _stateRoutine = StartCoroutine(FSM.Co_Ready());
                    break;
                case MainState.LoadingMiniGame:
                    _stateRoutine = StartCoroutine(FSM.Co_LoadingMini());
                    break;
                case MainState.PlayingMiniGame:
                    _stateRoutine = StartCoroutine(FSM.Co_PlayingMini());
                    break;
                case MainState.UnloadingMiniGame:
                    _stateRoutine = StartCoroutine(FSM.Co_UnloadingMini());
                    break;
                case MainState.ApplyingResult:
                    _stateRoutine = StartCoroutine(FSM.Co_ApplyingResult());
                    break;
                case MainState.End:
                    _stateRoutine = StartCoroutine(FSM.Co_End());
                    break;
            }
        }


        public async UniTask EndGameAsync(bool force = false, CancellationToken ct = default)
        {
            _stateRoutine = null;
            _isLeavingRoom = true;

            await Manager.UI.CloseAllPopupUI();
            UI.ShowLoadingToLobby();

            // 병렬 실행
            var unloadTask = Loader.UnloadAdditive().ToUniTask(cancellationToken: ct);
            await UniTask.WhenAll(unloadTask);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);

            Debug.Log("[MainGameManager] After close all popup ui, leave room");
            UI.SetLoadingProgress(0.4f);

            PhotonNetwork.LeaveRoom();
        }

        #endregion


        #region Score

        /// <summary>
        /// (마스터 전용) 현재 라운드 결과를 소비하고 Top1(동점 포함) +1 후, 스코어보드를 전원에게 브로드캐스트
        /// </summary>
        public void ApplyAndBroadcastScoreForCurrentRound()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 1) 라운드 정보 가져오기
            int round = PropertiesCtrl.GetRoomProps(RoomProps.Round, 1);
            // Debug.Log($"<color=green> 1. round 정보 가져오기 : {round}</color>");

            // 2) 라운드 결과 가져오기
            if (!GameResultData.TryConsumeRound(round, out var gameName, out var rankings))
            {
                Debug.LogError("Error! Can't Consume Round Result");
                return;
            }

            // 2-1) 라운드 승자만 가져오기
            // Debug.Log($"<color=green> 2. 라운드 승자 가져오기 </color>");
            HashSet<string> roundWinners = new();
            var winners = GameResultData.GetTop1FromRound(round); // 공동 1등 포함
            foreach (var uid in winners)
            {
                roundWinners.Add(uid);
                // Debug.Log($"<color=green> 2. 라운드 승자 : {uid}</color>");
            }

            // 3) 미니게임 랭크를 기준으로 정렬
            //진행한 미니게임의 랭킹을 기준으로 1등부터 순서대로 UI에 보여주기 위해 랭킹 순서로 정렬
            // 2) 표시 순서(미니게임 랭크 asc)
            // Debug.Log($"<color=green> 3. 미니게임 랭크 기준으로 정렬</color>");
            var order = (rankings != null && rankings.Count > 0)
                ? rankings.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key).ToList()
                : PlayerManager.Instance.Players.Keys.OrderBy(uid => uid).ToList();


            //----- 점수 반영 -----//
            // 최신 점수 계산
            // Debug.Log($"<color=green> 4. 최신 점수 계산</color>");
            var newScoreMap = new Dictionary<string, int>(order.Count);
            foreach (var uid in order)
            {
                var cur = PlayerManager.Instance.Players.TryGetValue(uid, out var gp) ? gp.Score : 0;
                newScoreMap[uid] = cur + (roundWinners.Contains(uid) ? 1 : 0);
            }

            // 전체 등수 계산
            // Debug.Log($"<color=green> 5. 전체 등수 계산</color>");
            var totalRankMap = Util_LDH.CalcTotalRank(newScoreMap);

            //------- 직렬화하여 보내기
            // Debug.Log($"<color=green> 6. 직렬화해서 보내기</color>");
            BroadcastScoreJson(round, gameName, order, rankings, newScoreMap, totalRankMap, roundWinners);
        }


        private void BroadcastScoreJson(
            int round, string gameName,
            List<string> orderUids,
            Dictionary<string, int> rankings,
            Dictionary<string, int> newScoreMap,
            Dictionary<string, int> totalRankMap,
            HashSet<string> roundWinnerSet)
        {
            var payload = new ScoreboardPayload
            {
                round = round,
                gameName = gameName,
                players = orderUids.Select(uid => new PlayerEntryDto()
                {
                    playerId = uid,
                    nickname = PlayerManager.Instance.GetPlayer(uid).Nickname,
                    score = newScoreMap[uid],
                    lastMiniGameRank =
                        rankings != null && rankings.TryGetValue(uid, out var r) ? r : int.MaxValue,
                    totalRank = totalRankMap.TryGetValue(uid, out var tr) ? tr : 0,
                    wonThisRound = roundWinnerSet.Contains(uid)
                }).ToArray()
            };

            Debug.Log($"<color=green> BroadcastScoreJson - 직렬화해서 rpc로 다 보냅니다.</color>");

            string json = JsonUtility.ToJson(payload);
            Debug.Log($"<color=green>sending data : {json}</color>");
            photonView.RPC(nameof(RPC_ShowScoreboardJson), RpcTarget.All, json);
        }


        public void DistributeFinalRewards()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_finalRewardsDistributed) return;
            _finalRewardsDistributed = true;

            // 1) 최종 순위로 정렬하기 (uid만)
            var ordered = PlayerManager.Instance.Players.Values.OrderBy(p => p.TotalRank).ThenBy(p => p.Nickname)
                .Select(p => p.PlayerId)
                .ToArray();

            photonView.RPC(nameof(RPC_ShowFinalRewards), RpcTarget.All, ordered);
        }

        #endregion

        #region UI Interaction

        private void OnClickReady(int slot)
        {
            Debug.Log("========= on click ready 호출 ================");
            if (FSM.Get() != MainState.Ready) return;

            if (_localSlot != slot)
            {
                Debug.Log("!!!! 내 슬롯 아님 !!!");
                return;
            }

            // 내 Ready 토글 (PlayerProps)
            bool now = PhotonNetwork.LocalPlayer.CustomProperties != null
                       && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerProps.InGameReady, out var v)
                       && v is bool b && b;

            PropertiesCtrl.SetLocalReady(!now);
        }

        #endregion


        #region RPC

        [PunRPC]
        private void RPC_ShowScoreboardJson(string json)
        {
            Debug.Log($"<color=green> receiving data : {json} </color>");

            var payload = JsonUtility.FromJson<ScoreboardPayload>(json);
            if (payload == null || payload.players == null) return;
            Debug.Log($"<color=green> json 변환 성공 </color>");

            // 1) 전원 플래그 초기화
            foreach (var kv in PlayerManager.Instance.Players)
                kv.Value.WonThisRound = false;

            // 2) PlayerManager 반영
            foreach (var p in payload.players)
            {
                if (!PlayerManager.Instance.Players.TryGetValue(p.playerId, out var gp))
                {
                    Debug.LogError("Player가 PlayerManager에 등록되지 않아서 오류 발생");
                    return;
                }

                gp.Score = p.score;
                gp.WonThisRound = p.wonThisRound;
                gp.LastMiniGameRank = p.lastMiniGameRank; // 필요 시 GamePlayer에 필드 이미 있음
                gp.TotalRank = p.totalRank;
            }


            Debug.Log($"<color=green> 점수 패널 활성화 합니다. </color>");

            // 3) UI 렌더 (표시 순서 = payload.players 순서)
            var orderedPlayers = payload.players
                .Select(p => PlayerManager.Instance.Players.TryGetValue(p.playerId, out var gp) ? gp : null)
                .Where(gp => gp != null)
                .ToArray();

            UniTask.Void(async () =>
            {
                try
                {
                    await UI.BuildScorePanel(payload.round, payload.gameName, orderedPlayers);
                    PropertiesCtrl.SetLocalResultDone(true);
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                    PropertiesCtrl.SetLocalResultDone(true); // 실패해도 안전하게 True 올림
                }
            });
        }


        [PunRPC]
        private void RPC_ShowFinalRewards(string[] ordered)
        {
            //정렬된 uid 리스트를 기준으로 ui 렌더 표시 순서 결정
            var orderedPlayers = new GamePlayer[ordered.Length];

            for (int i = 0; i < ordered.Length; i++)
            {
                orderedPlayers[i] = PlayerManager.Instance.GetPlayer(ordered[i]);
            }


            UniTask.Void(async () =>
            {
                try
                {
                    await UI.BuildFinalRewardPanel(orderedPlayers);
                    await UI.ShowRewardPopup(PlayerManager.Instance.GetPlayer(PhotonNetwork.LocalPlayer.UserId));
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            });
        }


        [PunRPC]
        public void RPC_BuildSlotMachine(string[] candidateIds, int targetIndex)
        {
            var list = candidateIds.ToList();
            
            UniTask.Void(async () =>
            {
                try
                {
                    await UI.BuildSlotMachine(list, targetIndex, PropertiesCtrl.GetRoomProps(RoomProps.Round, 1));
                    Debug.Log($"<color=green> Is master? {IsMaster} / 마스터가 아니면 끝, 마스터면 handle pull하는 rpc 호출</color>");
                    if (IsMaster)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(2f));
                        photonView.RPC(nameof(RPC_PullSlotHandle), RpcTarget.All);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            });
            
           
        }

        [PunRPC]
        public void RPC_PullSlotHandle()
        {
            Debug.Log($"<color=green> 핸들을 당깁니다. </color>");

            UniTask.Void(async () =>
            {
                try
                {
                    // 슬롯 돌리고 멈출 때까지 기다림
                    await UI.PullHandle();
                    await UniTask.Delay(TimeSpan.FromSeconds(2f));
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
                finally
                {
                    // 연출 끝났음을 알리기
                    PropertiesCtrl.SetLocalDone(true);
                }
            });
        }

        [PunRPC]
        public void RPC_ShowEndGame()
        {
            UI.ShowGameEnd().Forget();
        }

        #endregion
    }


    #region DTO

    [System.Serializable]
    public class PlayerEntryDto
    {
        public string playerId; // Firebase UID
        public string nickname; // Photon 닉네임
        public int score; // 누적 점수(이번 라운드 반영 후)
        public int lastMiniGameRank; // 최근 라운드 랭크
        public int totalRank; // 누적 등수(동순위)
        public bool wonThisRound; // 이번 라운드 +1 여부
    }

    [System.Serializable]
    public class ScoreboardPayload
    {
        public int round;
        public string gameName;
        public PlayerEntryDto[] players; // 표시 순서대로
    }

    public class FinalScoreboardPayload
    {
        public PlayerEntryDto[] players; // 표시 순서대로
    }

    #endregion
}