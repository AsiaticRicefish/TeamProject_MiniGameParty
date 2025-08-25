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
        Debug.Log("������ ���� ���� �Ϸ� �� �� ���� �õ�");
        CreateRoom();
    }

    private void CreateRoom()
    {
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers };
        PhotonNetwork.CreateRoom(null, options); // �̸� null�̸� ���� ����
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("�� ���� �Ϸ�! ���� �� �̸�: " + PhotonNetwork.CurrentRoom.Name);
    }
}
