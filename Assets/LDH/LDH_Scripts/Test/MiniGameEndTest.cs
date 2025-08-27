using LDH_MainGame;
using LDH_Util;
using Photon.Pun;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class MiniGameEndTest : MonoBehaviour
    {
        public void EndMiniGame()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log("----------------------- 미니 게임 종료 요청 ---------------------------");
                MainGameManager.Instance?.SetRoomProperties(Define_LDH.RoomProps.State, Define_LDH.MainState.ApplyingResult.ToString());
            }
        }
    }
}