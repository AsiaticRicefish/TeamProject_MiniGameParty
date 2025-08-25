using System;
using Managers;
using Photon.Pun;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class TestNetwork : MonoBehaviour
    {
        private void Awake()
        {
            Manager.Network.ConnectedToMaster += TryJoinRoom;
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Manager.Network.ConnectServer();
                
            }
        }

        public void TryJoinRoom()
        {
            if (!PhotonNetwork.InRoom)
            {
                Manager.Network.JoinQuickMatchRoom();
            }
        }
    }
}