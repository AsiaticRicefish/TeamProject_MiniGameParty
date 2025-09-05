using System;
using LDH_UI;
using Managers;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH.LDH_Scripts.Test
{
    public class UITestScript : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerInfoText;

        private void Awake()
        {
         
            Manager.Network.JoinedLobby += UpdatePlayerInfo;
        }

        private void Start()
        {
            if (PhotonNetwork.IsConnectedAndReady &&( PhotonNetwork.InLobby|| PhotonNetwork.InRoom ))
                UpdatePlayerInfo();
        }

        private void OnDestroy()
        {
            Manager.Network.JoinedLobby -= UpdatePlayerInfo;
        }

        private void UpdatePlayerInfo()
        {
            playerInfoText.text = $"Player ID : {PhotonNetwork.LocalPlayer.CustomProperties["uid"].ToString()} \n\nPlayer NickName :{PhotonNetwork.NickName}";
        }
    }
}