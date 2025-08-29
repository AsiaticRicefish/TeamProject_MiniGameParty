using DesignPattern;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace RhythmGame
{
    public class NetworkManager : PunSingleton<NetworkManager>
    {
        [Header("플레이어 프리팹 이름")]
        [SerializeField] string playerPrefabName = "RythmPlayer";
        [SerializeField] Vector3 tempSpawnPos = Vector3.zero; // 임시 스폰 위치

        [Header("룸 옵션")]
        [SerializeField] string gameRoomName = "RythemTestRoom";
        [SerializeField] byte maxPlayers = 1;

        /// <summary>
        /// 씬 이동 동기화 해제
        /// 방장이 방을 떠나도 게임은 진행할 수 있도록.
        /// </summary>
        protected override void Awake()
        {
            isPersistent = false;
            PhotonNetwork.AutomaticallySyncScene = false;
        }

        void Start()
        {
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                // Debug.Log("접속 완료");
                // //이전 BGM 정지
                // SoundManager.Instance.StopBGM();

                // //플레이어 생성
                // PlayerSpawn();
                // //마스터는 해당 씬에 들어온 플레이어 체크하기
                PhotonNetwork.JoinRandomOrCreateRoom();
            }
        }

        public override void OnConnectedToMaster()
        {
            PhotonNetwork.JoinRandomOrCreateRoom();
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            // 없으면 생성
            var roomOptions = new RoomOptions { MaxPlayers = maxPlayers, IsVisible = true, IsOpen = true };
            PhotonNetwork.CreateRoom(gameRoomName, roomOptions);
        }

        public override void OnCreatedRoom()
        {
            Debug.Log("방 생성");
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"방접속, 현재 방 참여 인원 수 : {PhotonNetwork.CurrentRoom.PlayerCount}");

            // 캐릭터 생성
            PhotonNetwork.Instantiate(playerPrefabName, tempSpawnPos, Quaternion.identity);

            // 2명이 되었으면 마스터가 시작
            StartGame();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"플레이어 접속, 접속 인원: {PhotonNetwork.CurrentRoom.PlayerCount}");
            StartGame();
        }

        /// <summary>
        /// 게임 시작 로직(임시)
        /// 
        /// 마스터일 경우 현재 플레이어가 맥스 플레이어 수 면 게임 시작
        /// </summary>
        void StartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (PhotonNetwork.CurrentRoom.PlayerCount >= maxPlayers)
            {
                Debug.Log("게임 시작");
                GameManager.Instance.StartGame();
            }
        }
        //마스터 클라이언트 변경시
        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (PhotonNetwork.LocalPlayer == newMasterClient)
            {
                Debug.Log("마스터 변경 및 권한 양도");

                //스폰 시스템 정지 후 바로 다시 스폰 시키도록
                NoteSpawner.Instance.StopSpawn();
                NoteSpawner.Instance.StartSpawn();

                //과열 관리 시스템 
            }
        }
    }
}