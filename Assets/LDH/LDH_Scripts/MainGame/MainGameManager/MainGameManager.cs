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
        public MainGame_RoomPropController RoomPropsCtrl;
        public MainGame_MaskController MaskCtrl;
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

            RoomPropsCtrl = new(
                () => PhotonNetwork.InRoom,
                () => PhotonNetwork.NetworkClientState == ClientState.Joined,
                () => !_isLeavingRoom);

            MaskCtrl = new(RoomPropsCtrl);
            UI = new(registry, (s => _localSlot = s), (OnClickReady));
            FSM = new(
                RoomPropsCtrl, MaskCtrl, UI, registry,
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
            if (RoomPropsCtrl == null || FSM == null || UI == null)
            {
                Debug.LogError("[MainGameManager] StartGame() called before Initialize() — abort.");
                return; // 또는 Initialize() 호출 후 재시도 로직을 넣어도 됨
            }
            
            
            OnGameStart?.Invoke();

            if (IsMaster)
                RoomPropsCtrl.Set(new Dictionary<string, object>
                    {
                        { RoomProps.Round, 1 },
                        { RoomProps.MiniGameId, "" },
                        { RoomProps.ReadyMask, 0 },
                        { RoomProps.DoneMask, 0 },
                        { RoomProps.State, MainState.Picking.ToString() }
                    }
                );

            OnRoundChanged?.Invoke(1);
        }

        public void NotifyMiniGameFinish()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                RoomPropsCtrl.Set(RoomProps.State, MainState.ApplyingResult.ToString());
            }
        }


        #region Photon callbacks

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            // //로그 출력용
            // Util_LDH.ConsoleLog(this,
            //     $"룸 프로퍼티 변경 - (State : {PhotonNetwork.CurrentRoom.CustomProperties[RoomProps.State]}), (MiniGameId : {PhotonNetwork.CurrentRoom.CustomProperties[RoomProps.MiniGameId]}), (ReadyMask : {(int)PhotonNetwork.CurrentRoom.CustomProperties[RoomProps.ReadyMask]:b})");

            if (changed.ContainsKey(RoomProps.ReadyMask))
            {
                Debug.Log("ready mask 변경됨");
                UI.UpdateReady(RoomPropsCtrl.Get(RoomProps.ReadyMask, 0));
            }

            if (changed.ContainsKey(RoomProps.DoneMask) && IsMaster)
            {
                FSM.CheckAllPlayerDone();
            }

            if (changed.ContainsKey(RoomProps.Round))
            {
                OnRoundChanged?.Invoke(RoomPropsCtrl.Get(RoomProps.Round, 1));
            }

            SyncGameState();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState != ClientState.Joined ||
                _isLeavingRoom) return;
            if (!IsMaster) return;
            if (FSM.Get() != MainState.Ready) return;

            // Ready 단계일 때만 ReadyMask 조정 로직 허용 (그 외 상태에서는 건드리지 않기)
            //마스터는 나간 플레이어 대해 room properties 조정을 해준다.
            //ready mask에서 해당 비트 제거
            if (otherPlayer.CustomProperties.TryGetValue(PlayerProps.SlotIndex, out var value) &&
                value is int slot)
            {
                int newMask = MaskCtrl.ToggleReady(slot, false);
                RoomPropsCtrl.Set(RoomProps.ReadyMask, newMask);

                if (MaskCtrl.IsAllDone(newMask))
                {
                    RoomPropsCtrl.Set(new Dictionary<string, object>
                    {
                        { RoomProps.State, MainState.LoadingMiniGame.ToString() }, { RoomProps.ReadyMask, 0 }
                    });
                }
            }
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (!IsMaster) return;

            if (FSM.Get() == MainState.Ready)
            {
                int mask = RoomPropsCtrl.Get(RoomProps.ReadyMask, 0);
                if (MaskCtrl.AreAllPresentOn(mask))
                {
                    RoomPropsCtrl.Set(new Dictionary<string, object>
                    {
                        { RoomProps.State, MainState.LoadingMiniGame.ToString() }, { RoomProps.ReadyMask, 0 }
                    });
                }
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

            Debug.Log(FSM.Get());
            if (FSM.Get() != MainState.Ready) return;
            
            Debug.Log(_localSlot);
            if (_localSlot != slot)
            {
                Debug.Log("!!!! 내 슬롯 아님 !!!");
                return; // 내 슬롯 아니면 무시
            }

            if (IsMaster)
            {
                int mask = RoomPropsCtrl.Get(RoomProps.ReadyMask, 0);
                bool toOn = ((mask >> slot) & 1) == 0;
                
                Debug.Log($"{mask} => {toOn}");
                RoomPropsCtrl.Set(RoomProps.ReadyMask, MaskCtrl.ToggleReady(slot, toOn));
            }
            else
            {
                bool now = ((RoomPropsCtrl.Get(RoomProps.ReadyMask, 0) >> slot) & 1) == 1;
                Debug.Log($"{now} => {!now}");
                photonView.RPC(nameof(RPC_SetReadyBySlot), RpcTarget.MasterClient, slot, !now);
            }
        }

        #endregion

        #region RPC

        [PunRPC]
        public void RPC_SetReadyBySlot(int slotIndex, bool isReady)
        {
            if (!IsMaster) return;
            RoomPropsCtrl.Set(RoomProps.ReadyMask, MaskCtrl.ToggleReady(slotIndex, isReady));
        }

        [PunRPC]
        public void RPC_SetDoneBySlot(int slotIndex, bool isDone)
        {
            if (!IsMaster) return;
            Debug.Log("RPC_SetDoneBySlot 호출");
            RoomPropsCtrl.Set(RoomProps.DoneMask, MaskCtrl.ToggleDone(slotIndex, isDone));
           FSM.CheckAllPlayerDone(); 
        }


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