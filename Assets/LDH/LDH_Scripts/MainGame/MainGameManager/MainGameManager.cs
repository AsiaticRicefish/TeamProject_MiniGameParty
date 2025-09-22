using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using static LDH_Util.Define_LDH;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace LDH_MainGame
{
    [RequireComponent(typeof(PhotonView))]
    public class MainGameManager : PunSingleton<MainGameManager>, IGameComponent
    {
        [Header("Mini Games")] public MiniGameRegistry registry;
        [Header("Config")] [SerializeField] private int totalRound = 3;
        public int TotalRound => totalRound;

        //Controllers
        public MainGame_PropertiesController PropertiesCtrl;
        public MainGame_UIBinder UI;
        public MainGame_StateMachine FSM;
        public MiniGameLoader Loader;

        // Local
        private bool _isLeavingRoom = false;
        private int _localSlot = -1;
        private Coroutine _stateRoutine;
        
        
        //Events
        public Action OnGameStart;
        public Action<int> OnRoundChanged;
        public Action OnPicking;
        public Action OnPicked;
        public Action OnWaitAllReady;
        public Action OnLoadingMiniGame;
        public Action OnEndMiniGame;
        public Action OnEndGame;


        private bool IsMaster => PhotonNetwork.IsMasterClient;


        protected override void OnAwake()
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            MainGameSceneController.Instance.Register(gameObject);
            
            
            
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
                totalRound,
                photonView
            );


            //플레이어 매니저에 플레이어 등록
            Debug.Log("[MainGameManager] PlayerManager에 플레이어를 등록합니다.");
            Manager.Player.ClearAllPlayers();
            Manager.Player.EnsureAllPhotonPlayersRegistered();
        }

        public void StartGame()
        {
            UI.SetDebugUI();
            
            // 필수 서비스 준비 확인
            if (PropertiesCtrl == null || FSM == null || UI == null)
            {
                Debug.LogError("[MainGameManager] StartGame() called before Initialize() — abort.");
                return; // 또는 Initialize() 호출 후 재시도 로직을 넣어도 됨
            }

            Util_LDH.ConsoleLog(this, "게임을 시작합니다. (Enter 'Picking' State)");
            OnGameStart?.Invoke();

            if (IsMaster)
                PropertiesCtrl.SetRoomProps(new Dictionary<string, object>
                    {
                        { RoomProps.Round, 1 },
                        { RoomProps.MiniGameId, "" },
                        { RoomProps.State, MainState.Picking.ToString() }
                    }
                );
            OnRoundChanged?.Invoke(1);
        }

        public void NotifyMiniGameStart()
        {
            if (PhotonNetwork.IsMasterClient)
                PropertiesCtrl.SetRoomProps(RoomProps.State, MainState.PlayingMiniGame.ToString());
        }

        public void NotifyMiniGameFinish()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PropertiesCtrl.SetRoomProps(RoomProps.State, MainState.ApplyingResult.ToString());
            }
        }


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
            if (target.IsLocal && changedProps.ContainsKey(PlayerProps.InGameDone))
            {
                Debug.Log($"[PlayerProps chagned] my done : {changedProps[PlayerProps.InGameDone]}");
            }
            
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

                if (changedProps.ContainsKey(PlayerProps.InGameDone) && FSM.Get() == MainState.ApplyingResult)
                {
                    FSM.CheckAllPlayerDone();
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
            if (FSM.Get() == MainState.Ready && PropertiesCtrl.AllPlayersReady() && PhotonNetwork.CurrentRoom.PlayerCount>1)
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
                MainGame_PropertiesController.SetSlotIndex(0);
                _localSlot = 0;
            }


            if (FSM.Get() == MainState.Ready && PropertiesCtrl.AllPlayersReady() && PhotonNetwork.CurrentRoom.PlayerCount>1)
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
            if (_stateRoutine != null)
            {
                StopCoroutine(_stateRoutine);
                _stateRoutine = null;
            }

            Util_LDH.ConsoleLog(this, $"State가 변경되었습니다. (Enter '{nextState.ToString()}' State)");

            FSM.Set(nextState);
            switch (nextState)
            {
                case MainState.Picking:
                    MainGame_PropertiesController.ClearLocalInGameProperties();
                    _stateRoutine = StartCoroutine(FSM.Co_Picking());
                    break;
                case MainState.Ready:
                    _stateRoutine = StartCoroutine(FSM.Co_Ready());
                    break;
                case MainState.LoadingMiniGame:
                    _stateRoutine = StartCoroutine(FSM.Co_LoadingMini());
                    break;
                case MainState.PlayingMiniGame:
                    _stateRoutine = StartCoroutine(FSM.Co_PlayingMini());
                    break;
                case MainState.ApplyingResult:
                    _stateRoutine = StartCoroutine(FSM.Co_ApplyingResult());
                    break;
                case MainState.End:
                    _stateRoutine = null;
                    EndGameAsync().Forget();
                    break;
            }
        }


        public async UniTask EndGameAsync(bool force = false, CancellationToken ct = default)
        {
            _isLeavingRoom = true;
            
            if (!force)
                await FSM.Co_End().ToUniTask(cancellationToken: ct);
            
            await Manager.UI.CloseAllPopupUI();
            UI.ShowLoading();
            
            // 병렬 실행
            var unloadTask = Loader.UnloadAdditive().ToUniTask(cancellationToken: ct);
            var closeAllScreenUITask = UI.CloseAllScreenUI();
            await UniTask.WhenAll(unloadTask,closeAllScreenUITask);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);

            Debug.Log("[MainGameManager] After close all popup ui, leave room");
            UI.SetLoadingProgress(0.4f);

            PhotonNetwork.LeaveRoom();
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

            MainGame_PropertiesController.SetLocalReady(!now);
        }

        #endregion

        #region RPC

        [PunRPC]
        public void RPC_CompletePicking()
        {
            OnPicked?.Invoke();
        }

        #endregion


        // private void ApplyMiniResult(MiniGameResult result)
        // {
        //     foreach (var kv in result.playerScore)
        //     {
        //         int actor = kv.Key;
        //         int score = kv.Value;
        //
        //         var uid = PhotonNetwork.PlayerList
        //             .FirstOrDefault(p => p.ActorNumber == actor)?
        //             .CustomProperties?["uid"] as string;
        //
        //         if (!string.IsNullOrEmpty(uid))
        //             PlayerManager.Instance.GetPlayer(uid)?.ApplyMiniScore(score);
        //     }
        // }


        // private bool TryConsumeMiniResult(out MiniGameResult result)
        // {
        //     // result = default;
        //     // var json = GetRoomProperty<string>(RoomProps.MiniGameResult, null);
        //     // if (string.IsNullOrEmpty(json)) return false;
        //     //
        //     // try
        //     // {
        //     //     result = JsonUtility.FromJson<MiniGameResult>(json);
        //     // }
        //     //
        //     // catch (Exception e)
        //     // {
        //     //     Util_LDH.ConsoleLogWarning(this, $"MiniGameResult parse failed: {e}");
        //     //     return false;
        //     // }
        //     return true;
        // }
        //
    }
}