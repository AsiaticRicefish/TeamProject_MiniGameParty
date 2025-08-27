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
    public class MainGameManager : PunSingleton<MainGameManager>, IGameComponent
    {
        [Header("Mini Games")] public MiniGameRegistry registry;

        // public List<MiniGameResultsStore> resultDB;
        private UI_Popup_PrivateRoom _readyPanel;
        private UI_GameInfo _gameInfoPanel;
        private int _localSlot = -1;
        private bool _readyChangeRequest = false;

        [Header("Player")] private GamePlayer[] _gamePlayers;


        [Header("씬 이동")] [SerializeField] private string lobbySceneName = "MainScene";

        [Header("Game State")] private Coroutine _stateRoutine;

        private MiniGameInfo _currentMiniGame;

        // private MiniGameResultsStore _lasResult;
        [field: SerializeField] public MainState State { get; private set; } = MainState.Init;

        //게임 종료 조건 : 플레이어 중 아무나 맵 골인 지점에 도착했을 때
        // 현재 맵 구현이 안됐기 때문에 임시로 total round를 정해서 프로토타입 구현
        [field: SerializeField] public int CurrentRound { get; private set; }
        [field: SerializeField] public int TotalRound { get; private set; } // 임시 추가



        private bool IsMaster => PhotonNetwork.IsMasterClient;
        private bool _isLeavingRoom;

        private bool CanChangeRoomProps => PhotonNetwork.InRoom &&
                                           PhotonNetwork.NetworkClientState == Photon.Realtime.ClientState.Joined &&
                                           !_isLeavingRoom;

        #region Action

        public event Action OnGameStart;
        public event Action OnPikcing;
        public event Action OnPicked;
        public event Action OnWaitAllReady;
        public event Action<int> OnRoundChanged;

        #endregion


        protected override void OnAwake()
        {
            isPersistent = false;
            MainGameSceneController.Instance.Register(gameObject);
            PhotonNetwork.AutomaticallySyncScene = false;
            base.OnAwake();
        }

        public void Initialize()
        {
            Util_LDH.ConsoleLog(this, "MainGameManager 초기화 로직 실행");
            // 커스텀 프로퍼티 초기화

            SetRoomProperties(new Dictionary<string, object>
            {
                { RoomProps.Round, 1 },     
                { RoomProps.MiniGameId, "" },
                { RoomProps.ReadyMask, 0 },
                { RoomProps.DoneMask, 0 },
                { RoomProps.State, MainState.Init.ToString() }
            });
        }

        public void StartGame()
        {
            Util_LDH.ConsoleLog(this, "게임을 시작합니다. (Enter 'Picking' State)");

            
            OnGameStart?.Invoke();
            OnRoundChanged?.Invoke(CurrentRound);


            if (IsMaster)
                SetRoomProperties(new Dictionary<string, object>
                {
                    { RoomProps.MiniGameId, "" },
                    { RoomProps.ReadyMask, 0 },
                    { RoomProps.DoneMask, 0 },
                    { RoomProps.State, MainState.Picking.ToString() }
                });
        }


        #region Set Properties / Get Properties

        private void SetRoomProperties(Dictionary<string, object> kv)
        {
            if (!CanChangeRoomProps) return;
            var ht = new Hashtable();
            foreach (var (k, v) in kv)
                if (v != null)
                    ht[k] = v;
            PhotonNetwork.CurrentRoom.SetCustomProperties(ht);
        }

        public void SetRoomProperties(string key, object value)
        {
            if (!CanChangeRoomProps) return;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { key, value } });
        }


        private T GetRoomProperty<T>(string key, T fallback)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room == null || room.CustomProperties == null)
            {
                Util_LDH.ConsoleLogWarning(this, "current room is null or current room properties are null");
                return fallback;
            }

            if (!room.CustomProperties.ContainsKey(key))
            {
                Util_LDH.ConsoleLogWarning(this, $"{key} property가 존재하지 않습니다.");
                return fallback;
            }

            try
            {
                return (T)room.CustomProperties[key];
            }
            catch (Exception e)
            {
                Util_LDH.ConsoleLogWarning(this, $"{key} property를 반환하는 중에 오류가 발생했습니다. - {e}");
                return fallback;
            }
        }

        #endregion


        #region Photon callbacks

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            //로그 출력용
            Util_LDH.ConsoleLog(this,
                $"룸 프로퍼티 변경 - (State : {PhotonNetwork.CurrentRoom.CustomProperties[RoomProps.State]}), (MiniGameId : {PhotonNetwork.CurrentRoom.CustomProperties[RoomProps.MiniGameId]}), (ReadyMask : {(int)PhotonNetwork.CurrentRoom.CustomProperties[RoomProps.ReadyMask]:b})");

            if (changed.ContainsKey(RoomProps.ReadyMask))
            {
                int ready = GetRoomProperty(RoomProps.ReadyMask, 0);
                _readyPanel?.UpdateReadyByMask(ready);
                // 로컬 버튼 재활성 등
            }

            if (changed.ContainsKey(RoomProps.DoneMask))
            {
                if (IsMaster) CheckAllPlayerDone();
            }

            if (changed.ContainsKey(RoomProps.Round))
            {
                CurrentRound = GetRoomProperty(RoomProps.Round, 1);
                OnRoundChanged?.Invoke(CurrentRound);
            }

            SyncGameState(); // 상태 전이 핸들링
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if(!CanChangeRoomProps) return;
            if (!IsMaster) return;

            // Ready 단계일 때만 ReadyMask 조정 로직 허용 (그 외 상태에서는 건드리지 않기)
            if (State != MainState.Ready) return;


            //마스터는 나간 플레이어 대해 room properties 조정을 해준다.
            //ready mask에서 해당 비트 제거
            if (otherPlayer.CustomProperties.TryGetValue(PlayerProps.SlotIndex, out var value) &&
                value is int slot)
            {
                int readyMask = GetRoomProperty(RoomProps.ReadyMask, 0);
                readyMask = SetBit(readyMask, slot, false);
                SetRoomProperties(new Dictionary<string, object> { { RoomProps.ReadyMask, readyMask } });


                //만약 ready 단계에서 전원 준비 상태를 체크 중이었다면 다시 체크한다.
                if (AreAllPlayerOn(readyMask))
                {
                    SetRoomProperties(new Dictionary<string, object>
                    {
                        { RoomProps.State, MainState.LoadingMiniGame.ToString() }, { RoomProps.ReadyMask, 0 }
                    });
                }
            }
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (IsMaster) MasterReconcile();
        }


        public override void OnLeftRoom()
        {
            // ★ 로비 씬 이동
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
            var stateStr = GetRoomProperty<string>(RoomProps.State, MainState.Init.ToString());

            if (!Enum.TryParse(stateStr, out MainState next)) next = MainState.Init;

            if (next != State)
                SwitchState(next);
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

            State = nextState;
            Util_LDH.ConsoleLog(this, $"State가 변경되었습니다. (Enter '{nextState.ToString()}' State)");

            switch (State)
            {
                case MainState.Picking:
                    _stateRoutine = StartCoroutine(Co_Picking());
                    break;
                case MainState.Ready:
                    _stateRoutine = StartCoroutine(Co_Ready());
                    break;
                case MainState.LoadingMiniGame:
                    _stateRoutine = StartCoroutine(Co_LoadingMini());
                    break;
                case MainState.PlayingMiniGame:
                    _stateRoutine = StartCoroutine(Co_PlayingMini());
                    break;
                case MainState.ApplyingResult:
                    _stateRoutine = StartCoroutine(Co_ApplyingResult());
                    break;
                case MainState.End:
                    _stateRoutine = StartCoroutine(Co_EndGame());
                    break;
            }
        }

        #endregion


        #region State

        // 0. Init : 초기화 로직 (메인 게임 진입하면서 처리 & MainGameSceneController에서 MainGameManager 초기화하면서 처리함)

        // 1. Picking : 마스터가 미니게임을 랜덤으로 뽑음
        private IEnumerator Co_Picking()
        {
            yield return new WaitForSeconds(1.5f); //디버그용으로 추가

            OnPikcing?.Invoke();

            yield return new WaitForSeconds(3f); //디버그용으로 추가

            if (IsMaster)
            {
                _currentMiniGame = registry.PickRandomGame();
                string id = _currentMiniGame.id;
                SetRoomProperties(new Dictionary<string, object>
                {
                    { RoomProps.MiniGameId, _currentMiniGame.id },
                    { RoomProps.ReadyMask, 0 },
                    { RoomProps.DoneMask, 0 },
                    { RoomProps.State, MainState.Ready.ToString() }
                });

                //todo : 가챠 연출(마스터가 rpc로 실행..? 아니면 어떻게 할지 고민)
                //일정시간 가차 연출 후? 결과 보여주기(임시로 텍스트로)
                //yield return StartCoroutine()

                photonView.RPC(nameof(RPC_CompletePicking), RpcTarget.All);
            }

            yield break;
        }

        // 2. Ready : Ready 패널과 게임 정보 패널 띄우기
        private IEnumerator Co_Ready()
        {
            yield return new WaitForSeconds(1.5f);
            OnWaitAllReady?.Invoke();

            string miniGameId = GetRoomProperty<string>(RoomProps.MiniGameId, null);
            _currentMiniGame = registry.Get(miniGameId);

            if (!string.IsNullOrEmpty(miniGameId) && _currentMiniGame != null)
            {
                _readyPanel = Manager.UI.CreatePopupUI<UI_Popup_PrivateRoom>("UI_Popup_ReadyPanel");
                _gameInfoPanel = _readyPanel.GetComponent<UI_GameInfo>();

                RebuildPanel();
                BindAllPanelEvents();

                //todo: 게임 정보 띄우는 패널도 만들고 띄우기

                Manager.UI.ShowPopupUI(_readyPanel).Forget();
            }


            //이걸 여기서 기다리는게 아니라.. customproperty 감지에서 처리해야하나
            while (State == MainState.Ready)
            {
                if (IsMaster)
                {
                    int mask = GetRoomProperty<int>(RoomProps.ReadyMask, 0);
                    if (AreAllPlayerOn(mask))
                    {
                        SetRoomProperties(new Dictionary<string, object>
                        {
                            { RoomProps.State, MainState.LoadingMiniGame.ToString() },
                            { RoomProps.ReadyMask, 0 } //초기화(다음 라운드를 위해)
                        });
                        break;
                    }
                }

                yield return new WaitForSeconds(0.1f);
            }
        }


        // 3. 
        private IEnumerator Co_LoadingMini()
        {
            if (_currentMiniGame == null || string.IsNullOrEmpty(_currentMiniGame.id))
            {
                Util_LDH.ConsoleLogWarning(this, "MiniGame is null at Loading.");
                if (IsMaster) 
                    SetRoomProperties(new Dictionary<string, object>
                    {
                        { RoomProps.State, MainState.Picking.ToString() },
                        { RoomProps.ReadyMask, GetRoomProperty(RoomProps.ReadyMask, 0) }
                    });
                yield break;
            }

            yield return MiniGameLoader.LoadAdditive(registry.GetSceneName(_currentMiniGame.id), null);


            if (_readyPanel != null)
            {
                UnbindAllPanelEvents();

                Manager.UI.ClosePopupUI(_readyPanel).Forget();
                _readyPanel = null;
            }
            //todo : 게임 정보 패널 닫기


            if (IsMaster)
            {
                SetRoomProperties(new Dictionary<string, object>
                {
                    { RoomProps.State, MainState.PlayingMiniGame.ToString() }
                });
            }
        }

        /// <summary>
        /// 4) PlayingMiniGame: Begin(seed) 호출, 결과 수신 대기
        /// </summary>
        private IEnumerator Co_PlayingMini()
        {
            //초기화
            //_lasResult = null;
            yield break;
        }

        /// <summary>
        /// 5) ApplyingResult: 언로드 후 다음 라운드(Picking) 전이
        /// </summary>
        private IEnumerator Co_ApplyingResult()
        {
            //-> 미니게임 쪽에서 상태를 applying result로 바꾸면 아래 호출됨.
            // 최종 결과를 넘겨주고 applying result로 룸 프로퍼티를 바꿔줘야 함
            yield return MiniGameLoader.UnloadAdditive();


            Util_LDH.ConsoleLog(this, "각 클라이언트에서 게임 결과를 적용합니다.");
            // if (TryConsumeMiniResult(out var final))
            // {
            //     // ApplyMiniResult(final);
            //     // _resultsStore?.AddRound(CurrentRound, final, ActorToUid);
            //     // CurrentRound++;
            // }
            // else
            // {


            // }


            //보상 받는게 끝나면 Done 이라고 알리기
            if (IsMaster)
            {
                int mask = GetRoomProperty(RoomProps.DoneMask, 0);
                int bit = 1 << _localSlot;
                if ((mask & bit) == 0)
                    SetRoomProperties(new Dictionary<string, object> { { RoomProps.DoneMask, mask | bit } });

            }

            else
            {
                photonView.RPC(nameof(RPC_SetDoneBySlot), RpcTarget.MasterClient, _localSlot, true);
            }
        }

        private IEnumerator Co_EndGame()
        {
            Util_LDH.ConsoleLog(this, "게임 종료. 보상 처리 & 로비 복귀");

            //todo : 플레이어 보상 획득 안내, 보상 획득 처리
            yield return new WaitForSeconds(1.5f);

            // 방 떠나기
            _isLeavingRoom = true;
            PhotonNetwork.LeaveRoom();
        }

        #endregion

        #region Panel

        private void BindAllPanelEvents()
        {
            if (_readyPanel?.PlayerPanels == null) return;

            foreach (var panel in _readyPanel.PlayerPanels)
            {
                if (panel == null) continue;
                panel.ReadyClicked += OnClickReady;
            }
        }

        private void UnbindAllPanelEvents()
        {
            if (_readyPanel?.PlayerPanels == null) return;

            foreach (var panel in _readyPanel.PlayerPanels)
            {
                if (panel == null) continue;
                panel.ReadyClicked -= OnClickReady;
            }
        }


        private void RebuildPanel()
        {
            _readyPanel?.ResetAllSlots(PhotonNetwork.IsMasterClient);
            _gameInfoPanel?.SetGameName(_currentMiniGame.gameName);
            _gameInfoPanel?.SetPlayerCount(PhotonNetwork.PlayerList.Length);


            int mask = GetRoomProperty(RoomProps.ReadyMask, 0);

            foreach (var pl in PhotonNetwork.PlayerList)
            {
                int slotIdx = (int)pl.CustomProperties[PlayerProps.SlotIndex];
                bool isReady = GetPlayerReadyState(slotIdx);
                _readyPanel.SetPlayerPanel(slotIdx, isReady, pl.IsLocal, pl.IsMasterClient);

                //초대 기능은 모두 막기
                _readyPanel[slotIdx].SetInviteActive(false);

                if (pl.IsLocal) _localSlot = slotIdx;
            }
        }

        private void OnClickReady(int slot)
        {
            Debug.Log("========= on click ready 호출 ================");
            if (_localSlot != slot)
            {
                Debug.Log("!!!! 내 슬롯 아님 !!!");
                return; // 내 슬롯 아니면 무시
            }

            if (_readyChangeRequest)
            {
                Debug.Log("!!!! ready 변경 요청중 !!!");
                return; // 연타 방지
            }


            bool now = GetPlayerReadyState(slot);
            bool isReady = !now;

            // 연타 방지 처리 --------
            _readyChangeRequest = true;
            _readyPanel[slot]?.SetReadyVisual(false);
            _readyPanel[slot].SetReadyButtonInteractable(false); // 로컬에서만 자기 버튼 잠금
            if (IsMaster)
            {
                int mask = GetRoomProperty(RoomProps.ReadyMask, 0);
                int bit = 1 << slot;
                int newMask = isReady ? (mask | bit) : (mask & ~bit);

                if (newMask == mask) // 변화 없으면 해제
                {
                    _readyChangeRequest = false;
                    _readyPanel[slot]?.SetReadyVisual(true);
                    _readyPanel[slot].SetReadyButtonInteractable(true);
                    return;
                }

                SetRoomProperties(RoomProps.ReadyMask, newMask);
            }

            else
            {
                photonView.RPC(nameof(RPC_SetReadyBySlot), RpcTarget.MasterClient, slot, isReady);
            }
        }


        [PunRPC]
        private void RPC_SetReadyBySlot(int slotIndex, bool isReady)
        {
            if (!IsMaster) return;
            int mask = GetRoomProperty(RoomProps.ReadyMask, 0);
            int bit = 1 << slotIndex;
            mask = isReady ? (mask | bit) : (mask & ~bit);
            SetRoomProperties(RoomProps.ReadyMask, mask);
        }
        [PunRPC]
        private void RPC_SetDoneBySlot(int slotIndex, bool isReady)
        {
            if (!IsMaster) return;
            int mask = GetRoomProperty(RoomProps.DoneMask, 0);
            int bit = 1 << slotIndex;
            mask = isReady ? (mask | bit) : (mask & ~bit);

            SetRoomProperties(new Dictionary<string, object> { { RoomProps.DoneMask, mask } });

        }

        private void CheckAllPlayerDone()
        {
            if (!IsMaster) return;
            int done = GetRoomProperty(RoomProps.DoneMask, 0);
            if (!AreAllPlayerOn(done)) return;

            int round = GetRoomProperty(RoomProps.Round, 1);
            round++;


            var table = new Dictionary<string, object>
            {
                { RoomProps.Round, round },
                { RoomProps.DoneMask, 0 }, // 다음 라운드를 위해 초기화
                { RoomProps.MiniGameId, "_" } // 다음 Picking에서 다시 정해질 예정
            };

            bool gameEnd = (round > TotalRound); // 네 로직에 맞게 조건 사용
            table[RoomProps.State] = gameEnd ? MainState.End.ToString() : MainState.Picking.ToString();

            SetRoomProperties(table);
        }

        #endregion

        private int SetBit(int mask, int bitIndex, bool on)
        {
            int bit = 1 << bitIndex;
            return on ? (mask | bit) : (mask & ~bit);
        }

        private bool AreAllPlayerOn(int mask)
        {
            int presentMask = GetPresentPlayerMask();
            return (mask & presentMask) == presentMask && presentMask != 0;
        }

        private bool GetPlayerReadyState(int slotIdx)
        {
            int readyMask = GetRoomProperty(RoomProps.ReadyMask, 0);
            int playerMask = 1 << slotIdx;
            return (readyMask & playerMask) == playerMask && playerMask != 0;
        }

        private int GetPresentPlayerMask()
        {
            int m = 0;
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                try
                {
                    int slotIdx = (int)p.CustomProperties[PlayerProps.SlotIndex];
                    m |= (1 << slotIdx);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            return m;
        }

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
        private void MasterReconcile()
        {
            // 새 마스터가 상태/마스크 일관성 재확인
            var stateValue = GetRoomProperty<string>(RoomProps.State, MainState.Init.ToString());
            if (!Enum.TryParse(stateValue, out MainState state)) state = MainState.Init;

            if (state == MainState.Ready)
            {
                int mask = GetRoomProperty<int>(RoomProps.ReadyMask, 0);
                var miniGameId = GetRoomProperty<string>(RoomProps.MiniGameId, null);
                _currentMiniGame = string.IsNullOrEmpty(miniGameId) ? null : registry.Get(miniGameId);

                if (AreAllPlayerOn(mask))
                    SetRoomProperties(new Dictionary<string, object>
                    {
                        {RoomProps.State, MainState.LoadingMiniGame.ToString()},
                        {RoomProps.MiniGameId, _currentMiniGame.id},
                        {RoomProps.ReadyMask, 0}
                    });
            }
        }

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


        [PunRPC]
        private void RPC_CompletePicking()
        {
            OnPicked?.Invoke();
        }
    }
}