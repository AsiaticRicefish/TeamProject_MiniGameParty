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
                //������ Ŭ���̾�Ʈ�� ����� �� ������Ƽ ���� �� ���� ó��
                var props = new ExitGames.Client.Photon.Hashtable
                {
                    { ShootingGamePropertyKeys.State, "InitState" },
                    { ShootingGamePropertyKeys.Turn, -1 },
                    ///{ ShootingGamePropertyKeyss.PlayerScore_Prefix + Player },
                };
                foreach (var player in PlayerManager.Instance.Players)
                {
                    string scoreKey = ShootingGamePropertyKeys.PlayerScore_Prefix + player.Value.PlayerId;
                    props.Add(scoreKey, 0);                                                                     // �ʱ� ���� 0
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
            // ���� ���� ����
            RoomPropertyObserver.Instance.RegisterObserver(ShootingGamePropertyKeys.State, (value) =>
            {
                string newState = (string)value;
                ShootingGameManager.Instance.ChangeStateByName(newState);
            });

            //�÷��̾� ���� ����
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