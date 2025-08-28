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
                    { ShootingGamePropertyKeys.Turn, -1 },
                     { ShootingGamePropertyKeys.Round, -1 },
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
            RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.State, (value) =>
            {
                string newState = (string)value;
                ShootingGameManager.Instance.ChangeStateByName(newState);
            });

            // 게임 턴,라운드(int) 구독
            RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.Turn, (value) =>
            {
                int newTurnIndex = (int)value;             

                TurnManager.Instance.currentTurnIndex = newTurnIndex;
                int newRound = (int)RoomPropertyObserver.Instance.GetRoomProperty("Round"); //          현재 최신 Round 읽기

                TurnManager.Instance.SetCurrentTurn();                                                  // 내 턴인지 판단
            });

            RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.Round, (value) =>
            {
                //라운드 변경시 필요할 부분 추가
            });


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
    }
}