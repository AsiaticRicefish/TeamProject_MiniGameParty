using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    public class ShootingNetworkManager : PunSingleton<ShootingNetworkManager>, IGameComponent
    {
        private string turnObserverId;
        private string SceneChangeObserverId;

        private Coroutine _setTurnCoroutine;

        public Coroutine SetTurnCoroutine
        {
            get => _setTurnCoroutine;
            set
            {
                _setTurnCoroutine = value;
            }
        }

        protected override void OnAwake()
        {
            base.isPersistent = false;
        }

        public void Initialize()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                //마스터 클라이언트만 사용할 룸 프로퍼티 생성 및 구독 처리
                var props = new ExitGames.Client.Photon.Hashtable
                {
                    { ShootingGamePropertyKeys.State, "InitState" },
                    { ShootingGamePropertyKeys.Turn, 0 },
                     { ShootingGamePropertyKeys.Round, 1 },
                    ///{ ShootingGamePropertyKeyss.PlayerScore_Prefix + Player },
                };
                foreach (var player in PlayerManager.Instance.Players)
                {
                    string scoreKey = ShootingGamePropertyKeys.PlayerScore_Prefix + player.Value.PlayerId;
                    props.Add(scoreKey, 0);                                                                     // 초기 점수 0
                }

                PhotonNetwork.CurrentRoom.SetCustomProperties(props);

                ShootingGameRoomPropertyRegister();
            }
            else
            {
                ShootingGameRoomPropertyRegister();
            }
        }


        private void ShootingGameRoomPropertyRegister()
        {
            // 게임 상태 구독
            ShootingGameSceneChangeRoomPropertiesReigster();
            // 게임 턴,라운드(int) 구독
            //ShootingGameTurnAndRoundRoomPropertiesReigster();

            //// 게임 턴,라운드(int) 구독
            //RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.Turn, (value) =>
            //{
            //    int newTurnIndex = (int)value;             

            //    TurnManager.Instance.currentTurnIndex = newTurnIndex;
            //    int newRound = (int)RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.Round); //          현재 최신 Round 읽기

            //    TurnManager.Instance.SetCurrentTurn();                                                  // 내 턴인지 판단
            //});

            //RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.Round, (value) =>
            //{
            //    //라운드 변경시 필요할 부분 추가
            //});


            //플레이어 점수 구독
            //foreach (var player in PlayerManager.Instance.Players)
            //{
            //    string scoreKey = ShootingGamePropertyKeys.PlayerScore_Prefix + player.Value.PlayerId;
            //    RoomPropertyObserver.Instance.RegisterObserver(scoreKey, (value) =>
            //    {
            //        int newScore = (int)value;
            //    });
            //}
        }

        public void ShootingGameSceneChangeRoomPropertiesReigster()
        {
            // 게임 상태 구독
            SceneChangeObserverId = RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.State, (value) =>
            {
                string newState = (string)value;
                ShootingGameManager.Instance.ChangeStateByName(newState);
            });
        }

        public void ShootingGameSceneChangeRoomPropertiesUnReigster()
        {
            if (!string.IsNullOrEmpty(SceneChangeObserverId))
            {
                RoomPropertyObserver.Instance.UnregisterObserverById(SceneChangeObserverId);
                SceneChangeObserverId = null;
            }
        }

        public void ShootingGameTurnAndRoundRoomPropertiesReigster()
        {
            // 게임 턴,라운드(int) 구독           
            // 람다를 변수에 저장해둠
            turnObserverId = RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.Turn, (value) =>
            {   
                // 모든 클라이언트에서 Turn Order (linkedlist)의 current를 업데이트한다.
                TurnManager.Instance.MoveToNextTurn();
                
                int newTurnIndex = (int)value;
                TurnManager.Instance.currentTurnIndex = newTurnIndex;

                int newRound = (int)RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.Round);
                TurnManager.Instance.currentRoundIndex = newRound;

                if (_setTurnCoroutine != null)
                {
                    StopCoroutine(_setTurnCoroutine);
                    _setTurnCoroutine = null;
                }
                StartCoroutine(TurnManager.Instance.SetCurrentTurn());
            });
        }

        public void ShootingGameTurnAndRoundRoomPropertiesUnReigster()
        {
            if (!string.IsNullOrEmpty(turnObserverId))
            {
                RoomPropertyObserver.Instance.UnregisterObserverById(turnObserverId);
                turnObserverId = null;
            }
        }

        //슈팅 게임 룸 프로퍼티 초기화 함수
        public void ClearShootingGameRoomProperties()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 초기화할 룸 프로퍼티 키들
            string[] gameKeys = { 
                ShootingGamePropertyKeys.State, 
                ShootingGamePropertyKeys.Turn, 
                ShootingGamePropertyKeys.Round,
                ShootingGamePropertyKeys.KEY_DECK_VALUES,
                ShootingGamePropertyKeys.KEY_CARD_OWNERS,
                ShootingGamePropertyKeys.KEY_STATE,
                ShootingGamePropertyKeys.KEY_TURN_ORDER,
             };

            var props = new ExitGames.Client.Photon.Hashtable();
            foreach (var key in gameKeys)
            {
                props[key] = null;  // 또는 초기값 설정 가능
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        //슈팅 게임 플레이어 프로퍼티 초기화 함수
        public void ClearShootingGamePlayerProperties()
        {
            //각자 초기화

            //초기화할 플레이어 프로퍼티 키들
            string[] keys = { 
                ShootingGamePlayerPropertyKeys.MyPrefabName, 
                ShootingGamePlayerPropertyKeys.MyTurnIndex 
            };

            var props = new ExitGames.Client.Photon.Hashtable();
            foreach (var key in keys)
            {
                props[key] = null;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
    }
}