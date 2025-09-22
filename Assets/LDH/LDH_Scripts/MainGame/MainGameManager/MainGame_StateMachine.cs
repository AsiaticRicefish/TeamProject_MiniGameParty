using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        public IEnumerator Co_Picking()
        {
            MainGameManager.Instance.OnPicking?.Invoke();
            
            _uiBinder.CloseScorePanel();
            
            yield return new UnityEngine.WaitForSeconds(1.5f);

            if (_isMaster())
            {
                // 직전에 뽑은 미니게임은 다음에는 뽑지 않도록 함(단, 레지스트리에 1개만 있다면 동일한 미니게임 뽑도록 처리)
                _currentMini = _registry.PickRandomGame(info => _registry.Count == 1 || info.id != _currentMini?.id);
                _pc.SetRoomProps(new Dictionary<string, object>
                {
                    { RoomProps.MiniGameId, _currentMini.id }, { RoomProps.State, MainState.Ready.ToString() }
                });
            }

            MainGameManager.Instance.OnPicked?.Invoke();
            ;
        }

        public IEnumerator Co_Ready()
        {
            yield return new UnityEngine.WaitForSeconds(0.3f);

            MainGameManager.Instance.OnWaitAllReady?.Invoke();

            string id = _pc.GetRoomProps(RoomProps.MiniGameId, "");
            _currentMini = string.IsNullOrEmpty(id) ? null : _registry.Get(id);
            if (_currentMini != null)
                _uiBinder.BuildReadyPanel(_currentMini, PhotonNetwork.PlayerList, _isMaster(), out _);
        }

        public IEnumerator Co_LoadingMini()
        {
            if (_currentMini == null)
            {
                if (_isMaster())
                    _pc.SetRoomProps(RoomProps.State, MainState.Picking.ToString());
                yield break;
            }

            //UI 비활성화
            _uiBinder.SetActiveDebugUI(false);
            yield return _uiBinder.CloseReadyPanel().ToCoroutine();


            // Additive Load
            yield return MainGameManager.Instance.Loader.LoadAdditive(_sceneName(_currentMini), null);

            MainGameManager.Instance.OnLoadingMiniGame?.Invoke();
        }

        public IEnumerator Co_PlayingMini()
        {
            // 미니게임 종료는 외부에서 State=ApplyingResult로 전환한다고 가정
            _pc.SetLocalReady(false);
            _pc.SetLocalMiniGameDone(false);
            yield break;
        }

        public IEnumerator Co_UnloadingMini()
        {
            // 1) 미니게임 종료 연출
            // todo: 게임 종료 UI 띄우기
            yield return new WaitForSeconds(0.8f);

            // 2) 결과 집계 중 오버레이
            _uiBinder.ShowLoadingForResult();

            // 3) 미니게임 씬 언로드
            yield return MainGameManager.Instance.Loader.UnloadAdditive();


            // 4) 필요한 변수 초기화 및 UI 활성화
            PhotonViewSync.Instance.Clear();
            _uiBinder.SetActiveDebugUI(true);

            //5) 언로드 플래그 켜기
            // 각자 자기 Done = true
            _pc.SetLocalMiniGameDone(true);

            yield return null;

            //6) 모두 완료됐으면 마스터가 다음 스테이트로 알아서 전환함
        }

        public IEnumerator Co_ApplyingResult()
        {
            // 1) 모두 로딩창 닫기
            _uiBinder.SetLoadingProgress(1f);
            yield return new WaitForSeconds(0.8f);
            _uiBinder.CloseLoadingForResult();

            // 2) 마스터는 점수 계산 + 브로드 캐스트
            if(_isMaster())        
                MainGameManager.Instance.ApplyAndBroadcastScoreForCurrentRound();
            
            
            MainGameManager.Instance.OnEndMiniGame?.Invoke();
        }

        public IEnumerator Co_End()
        {
            MainGameManager.Instance.OnEndGame?.Invoke();
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

            _pc.SetRoomProps(RoomProps.State, MainState.ApplyingResult.ToString());
        }

        public void CheckNextState()
        {
            if (!_isMaster()) return;
            Debug.Log("모두 결과창 확인이 완료됐는지 체크 (PlayerProps 기반)");
            if (!_pc.AllPlayersResultDone()) return;
            
            int currentRound = _pc.GetRoomProps(RoomProps.Round, 1);
            bool isEnd = (currentRound + 1) > TotalRound;
            var nextState = isEnd ? MainState.End : MainState.Picking;
            int nextRound = isEnd ? currentRound : currentRound + 1;

            _pc.SetRoomProps(new Dictionary<string, object>
            {
                { RoomProps.Round, nextRound },
                { RoomProps.MiniGameId, "" },
                { RoomProps.State, nextState.ToString() }
            });
        }
    }
}