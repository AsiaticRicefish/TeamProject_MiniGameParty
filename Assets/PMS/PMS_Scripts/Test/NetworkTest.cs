using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkTest : MonoBehaviourPunCallbacks
{
    [SerializeField] private byte maxPlayers = 4;
    public override void OnConnectedToMaster()
    {
        Debug.Log("마스터 서버 접속 완료 → 방 생성 시도");
        CreateRoom();
    }

    private void CreateRoom()
    {
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers };
        PhotonNetwork.CreateRoom(null, options); // 이름 null이면 랜덤 생성
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("방 입장 완료! 현재 방 이름: " + PhotonNetwork.CurrentRoom.Name);
    }
}
