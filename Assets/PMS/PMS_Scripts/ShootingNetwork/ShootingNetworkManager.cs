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

            //플레이어 점수 구독
            foreach (var player in PlayerManager.Instance.Players)
            {
                string scoreKey = ShootingGamePropertyKeys.PlayerScore_Prefix + player.Value.PlayerId;
                RoomPropertyObserver.Instance.RegisterObserver(scoreKey, (value) =>
                {
                    int newScore = (int)value;
                });
            }
        }
    }
}