using System;
using System.Collections;
using System.Collections.Generic;
using LDH_Util;
using Photon.Pun;
using UnityEngine;
using static LDH_Util.Define_LDH;

namespace LDH_MainGame
{
    public class MainGame_StateMachine
    {
        private readonly MainGame_PropertiesController _pc;
        private readonly MainGame_UIBinder _uiBinder;
        private readonly MiniGameRegistry _registry;
        private readonly System.Func<bool> _isMaster;
        private readonly System.Func<int> _getLocalSlot;
        private readonly System.Action<int> _onRoundChanged;
        private readonly System.Func<MiniGameInfo, string> _sceneName;
        private readonly System.Func<IEnumerator, Coroutine> _start;
        private readonly System.Action<Coroutine> _stop;
        private readonly PhotonView _pv; // 주입받은 PhotonView (현재는 사용 안 함)

        public int TotalRound { get; }
        private Define_LDH.MainState _state = Define_LDH.MainState.Init;
        private MiniGameInfo _currentMini;

        public MainGame_StateMachine(
            MainGame_PropertiesController pc,
            MainGame_UIBinder ui,
            MiniGameRegistry registry,
            System.Func<bool> isMaster, System.Func<int> getLocalSlot, System.Action<int> onRoundChanged,
            System.Func<MiniGameInfo, string> sceneName,
            System.Func<IEnumerator, Coroutine> startCoroutine,
            System.Action<Coroutine> stopCoroutine,
            int totalRound,
            PhotonView pv)
        {
            _pc = pc;
            _uiBinder = ui;
            _registry = registry;
            _isMaster = isMaster;
            _getLocalSlot = getLocalSlot;
            _onRoundChanged = onRoundChanged;
            _sceneName = sceneName;
            _start = startCoroutine;
            _stop = stopCoroutine;
            TotalRound = totalRound;
            _pv = pv;
        }
        
        
        public MainState Get() => _state;
        public MainState ReadOrDefault()
        {
            var s = _pc.GetRoomProps(RoomProps.State, MainState.Init.ToString());
            return System.Enum.TryParse(s, out MainState m) ? m : MainState.Init;
        }
        public bool Changed(MainState next) => next != _state;
        public void Set(MainState next) => _state = next;

        #region Coroutine
        
        public IEnumerator Co_Picking(System.Action onPicking = null, System.Action onPicked= null)
        {
            onPicking?.Invoke();
            
            yield return new UnityEngine.WaitForSeconds(1.5f);

            if (_isMaster())
            {
                _currentMini = _registry.PickRandomGame();
                _pc.SetRoomProps(new Dictionary<string, object> {
                    { RoomProps.MiniGameId, _currentMini.id },
                    { RoomProps.State, MainState.Ready.ToString() }
                });
            }

            onPicked?.Invoke();
        }

        public IEnumerator Co_Ready(Action onWaitAllReady = null)
        {
            yield return new UnityEngine.WaitForSeconds(0.3f);
            
            onWaitAllReady?.Invoke();

            string id = _pc.GetRoomProps(RoomProps.MiniGameId, "");
            _currentMini = string.IsNullOrEmpty(id) ? null : _registry.Get(id);
            if (_currentMini != null)
                _uiBinder.BuildReadyPanel(_currentMini, PhotonNetwork.PlayerList, _isMaster(), out _);

            // // 마스터가 모두 준비되면 로딩으로
            // while (_state == MainState.Ready)
            // {
            //     if (_isMaster())
            //     {
            //         if (_pc.AllPlayersReady())
            //         {
            //             _pc.SetRoomProps(new Dictionary<string, object> {
            //                 { RoomProps.State, MainState.LoadingMiniGame.ToString() }
            //             });
            //             break;
            //         }
            //     }
            //     yield return null;
            // }
        }

        public IEnumerator Co_LoadingMini(Action onLoadingMiniGame = null)
        {
            
            
            if (_currentMini == null)
            {
                if (_isMaster())
                    _pc.SetRoomProps(RoomProps.State, MainState.Picking.ToString());
                yield break;
            }

            // Additive Load
            yield return MiniGameLoader.LoadAdditive(_sceneName(_currentMini), null);

            _uiBinder.CloseReadyPanel();
            if (_isMaster())
                _pc.SetRoomProps(RoomProps.State, MainState.PlayingMiniGame.ToString());
            
            onLoadingMiniGame?.Invoke();
            
        }

        public IEnumerator Co_PlayingMini()
        {
            // 미니게임 종료는 외부에서 State=ApplyingResult로 전환한다고 가정
            yield break;
        }

        public IEnumerator Co_ApplyingResult(Action onApplyingResult)
        {
            yield return MiniGameLoader.UnloadAdditive();

            // 각자 자기 Done = true
            MainGame_PropertiesController.SetLocalDone(true);
            
            onApplyingResult?.Invoke();
        }

        public IEnumerator Co_End(Action onEndGame)
        {
            onEndGame?.Invoke();
            yield return new UnityEngine.WaitForSeconds(3f);
            // LeaveRoom은 MainGameManager에서 호출 (씬 전환 담당)
        }
        
        #endregion
        
        
        // ---- Done 종합 판정 → 라운드 증가/전이 ----
        public void CheckAllPlayerDone()
        {
            if (!_isMaster()) return;
           
            Debug.Log("모두 완료됐는지 체크 (PlayerProps 기반)");
            if (!_pc.AllPlayersDone()) return;

            int currentRound = _pc.GetRoomProps(RoomProps.Round, 1);
            bool isEnd    = (currentRound+1) > TotalRound;
            var nextState = isEnd ? MainState.End : MainState.Picking;
            int nextRound = isEnd ? currentRound : currentRound + 1;
            
            _pc.SetRoomProps(new Dictionary<string, object> {
                { RoomProps.Round,      nextRound },
                { RoomProps.MiniGameId, ""        },
                { RoomProps.State,      nextState.ToString() }
            });
        }
    }
}
