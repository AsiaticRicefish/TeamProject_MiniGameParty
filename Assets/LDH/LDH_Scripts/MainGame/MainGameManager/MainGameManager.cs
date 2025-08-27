using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DesignPattern;
using JetBrains.Annotations;
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
    public class MainGameManager : PunSingleton<MainGameManager>, ICoroutineGameComponent
    {
        [Header("Mini Games")] public MiniGameRegistry registry;
        [Header("Scene")] [SerializeField] private string lobbySceneName = "MainScene";
        [Header("Config")] [SerializeField] private int totalRound = 3;

        //Controllers
        public MainGame_PropertiesController PropertiesesCtrl;
        public MainGame_UIBinder UI;
        public MainGame_StateMachine FSM;

        // Local
        private bool _isLeavingRoom = false;
        private int _localSlot = -1;
        private Coroutine _stateRoutine;


        //Events
        public event Action OnGameStart;
        public event Action<int> OnRoundChanged;
        public event Action OnPicking;
        public event Action OnPicked;
        public event Action OnWaitAllReady;


        private bool IsMaster => PhotonNetwork.IsMasterClient;


        protected override void OnAwake()
        {
            isPersistent = false;

            PhotonNetwork.AutomaticallySyncScene = false;
            MainGameSceneController.Instance.Register(gameObject);

            base.OnAwake();
        }

        public IEnumerator InitializeCoroutine()
        {
            Util_LDH.ConsoleLog(this, "MainGameManager 초기화 로직 실행");

            PropertiesesCtrl = new(
                () => PhotonNetwork.InRoom,
                () => PhotonNetwork.NetworkClientState == ClientState.Joined,
                () => !_isLeavingRoom);

            UI = new(registry, (s => _localSlot = s), (OnClickReady));
            FSM = new(
                PropertiesesCtrl, UI, registry,
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
            yield return null;
        }

        public void StartGame()
        {
            Util_LDH.ConsoleLog(this, "게임을 시작합니다. (Enter 'Picking' State)");

            // 필수 서비스 준비 확인
            if (PropertiesesCtrl == null || FSM == null || UI == null)
            {
                Debug.LogError("[MainGameManager] StartGame() called before Initialize() — abort.");
                return; // 또는 Initialize() 호출 후 재시도 로직을 넣어도 됨
            }


            OnGameStart?.Invoke();

            if (IsMaster)
                PropertiesesCtrl.SetRoomProps(new Dictionary<string, object>
                    {
                        { RoomProps.Round, 1 },
                        { RoomProps.MiniGameId, "" },
                        { RoomProps.State, MainState.Picking.ToString() }
                    }
                );

            OnRoundChanged?.Invoke(1);
        }

        public void NotifyMiniGameFinish()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PropertiesesCtrl.SetRoomProps(RoomProps.State, MainState.ApplyingResult.ToString());
            }
        }


        #region Photon callbacks

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed == null || changed.Count == 0) return;

            if (changed.ContainsKey(RoomProps.Round))
            {
                OnRoundChanged?.Invoke(PropertiesesCtrl.GetRoomProps(RoomProps.Round, 1));
            }

            // 상태 변경 반영
            SyncGameState();
        }

        public override void OnPlayerPropertiesUpdate(Player target, Hashtable changedProps)
        {
            // UI Ready 표시 갱신: PlayerProps 기반으로 계산해서 UI에만 전달
            int readyMask = PropertiesesCtrl.BuildReadyMaskFromPlayers();
            UI.UpdateReady(readyMask);

            if (IsMaster)
            {
                if (changedProps.ContainsKey(PlayerProps.InGameReady) &&
                    FSM.Get() == MainState.Ready &&
                    PropertiesesCtrl.AllPlayersReady())
                {
                    PropertiesesCtrl.SetRoomProps(RoomProps.State, MainState.LoadingMiniGame.ToString());
                }

                if (changedProps.ContainsKey(PlayerProps.InGameDone) && FSM.Get() == MainState.ApplyingResult)
                {
                    FSM.CheckAllPlayerDone();
                }
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState != ClientState.Joined ||
                _isLeavingRoom) return;
            if (!IsMaster) return;
            if (FSM.Get() != MainState.Ready) return;

            // 준비 단계에서 누가 나가도, 남은 인원 기준 AllPlayersReady면 진행
            if (FSM.Get() == MainState.Ready && PropertiesesCtrl.AllPlayersReady())
            {
                PropertiesesCtrl.SetRoomProps(RoomProps.State, MainState.LoadingMiniGame.ToString());
            }

            // UI 갱신
            UI.UpdateReady(PropertiesesCtrl.BuildReadyMaskFromPlayers());
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (!IsMaster) return;

            if (FSM.Get() == MainState.Ready && PropertiesesCtrl.AllPlayersReady())
            {
                PropertiesesCtrl.SetRoomProps(RoomProps.State, MainState.LoadingMiniGame.ToString());
            }
        }


        public override void OnLeftRoom()
        {
            if (!string.IsNullOrEmpty(lobbySceneName))
            {
                Debug.Log("로비로 이동합니다.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
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
                    _stateRoutine = StartCoroutine(FSM.Co_Picking(OnPicking, OnPicked));
                    break;
                case MainState.Ready:
                    _stateRoutine = StartCoroutine(FSM.Co_Ready(OnWaitAllReady));
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
                    _stateRoutine = StartCoroutine(Co_EndGame());
                    break;
            }
        }


        private IEnumerator Co_EndGame()
        {
            yield return FSM.Co_End();
            _isLeavingRoom = true;
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