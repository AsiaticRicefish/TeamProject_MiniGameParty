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
        private readonly MainGame_RoomPropController _rpc;
        private readonly MainGame_MaskController _maskctr;
        private readonly MainGame_UIBinder _uiBinder;
        private readonly MiniGameRegistry _registry;
        private readonly System.Func<bool> _isMaster;
        private readonly System.Func<int> _getLocalSlot;
        private readonly System.Action<int> _onRoundChanged;
        private readonly System.Func<MiniGameInfo, string> _sceneName;
        private readonly System.Func<IEnumerator, Coroutine> _start;
        private readonly System.Action<Coroutine> _stop;
        private readonly PhotonView _pv; // 주입받은 PhotonView

        public int TotalRound { get; }
        private Define_LDH.MainState _state = Define_LDH.MainState.Init;
        private MiniGameInfo _currentMini;

        public MainGame_StateMachine(
            MainGame_RoomPropController rpc, MainGame_MaskController mask, MainGame_UIBinder ui,
            MiniGameRegistry registry,
            System.Func<bool> isMaster, System.Func<int> getLocalSlot, System.Action<int> onRoundChanged,
            System.Func<MiniGameInfo, string> sceneName,
            System.Func<IEnumerator, Coroutine> startCoroutine,
            System.Action<Coroutine> stopCoroutine,
            int totalRound,
            PhotonView pv)
        {
            _rpc = rpc;
            _maskctr = mask;
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
            var s = _rpc.Get(RoomProps.State, MainState.Init.ToString());
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
                _rpc.Set(new Dictionary<string, object> {
                    { RoomProps.MiniGameId, _currentMini.id },
                    { RoomProps.ReadyMask, 0 }, 
                    { RoomProps.DoneMask, 0 },
                    { RoomProps.State, MainState.Ready.ToString() }
                });
                // _pv.RPC(
                //     nameof(MainGameManager.RPC_CompletePicking), RpcTarget.All);
            }

            onPicked?.Invoke();
        }

        public IEnumerator Co_Ready(Action onWaitAllReady = null)
        {
            yield return new UnityEngine.WaitForSeconds(0.3f);
            
            onWaitAllReady?.Invoke();

            string id = _rpc.Get(RoomProps.MiniGameId, "");
            _currentMini = string.IsNullOrEmpty(id) ? null : _registry.Get(id);
            if (_currentMini != null)
                _uiBinder.BuildReadyPanel(_currentMini, PhotonNetwork.PlayerList, _isMaster(), out _);

            // 마스터가 모두 준비되면 로딩으로
            while (_state == MainState.Ready)
            {
                if (_isMaster())
                {
                    int mask = _rpc.Get(RoomProps.ReadyMask, 0);
                    if (_maskctr.IsAllReady(mask))
                    {
                        _rpc.Set(new Dictionary<string, object> {
                            { RoomProps.State, MainState.LoadingMiniGame.ToString() },
                            { RoomProps.ReadyMask, 0 }
                        });
                        break;
                    }
                }
                yield return null;
            }
            
            
        }

        public IEnumerator Co_LoadingMini()
        {
            if (_currentMini == null)
            {
                if (_isMaster())
                    _rpc.Set(RoomProps.State, MainState.Picking.ToString());
                yield break;
            }

            // Additive Load
            yield return MiniGameLoader.LoadAdditive(_sceneName(_currentMini), null);

            _uiBinder.CloseReadyPanel();
            if (_isMaster())
                _rpc.Set(RoomProps.State, MainState.PlayingMiniGame.ToString());
        }

        public IEnumerator Co_PlayingMini()
        {
            // 미니게임이 끝나면 외부에서 State=ApplyingResult로 전환한다고 가정
            yield break;
        }

        public IEnumerator Co_ApplyingResult()
        {
            yield return MiniGameLoader.UnloadAdditive();

            if (_isMaster())
            {
                // 마스터 자기 비트 멱등 반영
                int mask = _rpc.Get(RoomProps.DoneMask, 0);
                int bit = 1 << _getLocalSlot();
                if ((mask & bit) == 0) _rpc.Set(RoomProps.DoneMask, mask | bit);

                CheckAllPlayerDone();
            }
            else
            {
                // 클라 → 마스터에게 Done 요청
                PhotonNetwork.LocalPlayer.TagObject = null; // 필요시
                _pv?.RPC(nameof(MainGameManager.RPC_SetDoneBySlot),
                          RpcTarget.MasterClient, _getLocalSlot(), true);
            }
        }

        public IEnumerator Co_End()
        {
            yield return new UnityEngine.WaitForSeconds(1.5f);
            // LeaveRoom은 MainGameManager에서 호출 (씬 전환 담당)
        }
        
        #endregion
        
        
        // ---- DoneMask 종합 판정 → 라운드 증가/전이 ----
        public void CheckAllPlayerDone()
        {
            if (!_isMaster()) return;
           
            Debug.Log("모두 완료됐는지 체크");
            int done = _rpc.Get(RoomProps.DoneMask, 0);
            if (!_maskctr.IsAllDone(done)) return;

            int round = _rpc.Get(RoomProps.Round, 1) + 1;
            var kv = new Dictionary<string, object> {
                { RoomProps.Round, round }, { RoomProps.DoneMask, 0 }, { RoomProps.MiniGameId, "" }
            };
            bool end = (round > TotalRound);
            kv[RoomProps.State] = end ? MainState.End.ToString() : MainState.Picking.ToString();
            _rpc.Set(kv);

            _onRoundChanged?.Invoke(round);
        }
    }
}