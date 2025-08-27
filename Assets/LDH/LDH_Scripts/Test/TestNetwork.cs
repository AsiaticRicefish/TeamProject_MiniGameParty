using System;
using ExitGames.Client.Photon;
using LDH_Util;
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
            Manager.Network.JoinedRoom += InitPlayerProperties;
            
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

        public void InitPlayerProperties()
        {
            var table = new Hashtable
            {
                { Define_LDH.PlayerProps.SlotIndex, PhotonNetwork.LocalPlayer.ActorNumber-1 },
                { Define_LDH.PlayerProps.ReadyState, true }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(table);
        }
        
    }
}