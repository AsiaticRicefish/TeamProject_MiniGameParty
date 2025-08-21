using System;
using DesignPattern;
using LDH_Util;
using LDH_Utils;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using static LDH_Util.Define_LDH;

namespace Network
{
    public partial class NetworkManager : PunSingleton<NetworkManager>
    {
        [SerializeField] private string gameSceneName;
        [SerializeField] private bool autoSyncScene = true;

        // ------ Events ------ //
        public event Action ConnectedToMaster; // 로딩 씬 UI에서 이벤트 구독할 예정
        public event Action JoinedRoom;
        public event Action<Player> PlayerEntered;
        public event Action<Player> PlayerLeft;
        public event Action<int, int> RoomPlayerCountChanged; // (current, max)
        public event Action<short, string> JoinRandomFailed;
        public event Action<string> MatchStateChanged;



        // 초기화 작업
        protected override void OnAwake()
        {
            PhotonNetwork.AutomaticallySyncScene = autoSyncScene;

            //임시로 awake 시점에 호출
            ConnectServer();
        }


        //필요한 시점에 호출하기 
        //구글 로그인 성공 후 메인 씬으로 이동하기 전에 진행하면 될 것으로 판단
        public void ConnectServer()
        {
            if (!PhotonNetwork.IsConnected)
                PhotonNetwork.ConnectUsingSettings();
            SetNickname();
        }

        // 임시 추가
        //todo: 파이어베이스 연결후 파이어베이스 닉네임을 적용하는 것으로 수정..? 아닌가? + 처음 계정 연동시 닉네임 설정 UI 제공 , 이후 프로필에서 수정가능 
        //지금은 임시 테스트를 위해 닉네임 임시 할당
        public void SetNickname()
        {
            if (string.IsNullOrEmpty(PhotonNetwork.NickName))
                PhotonNetwork.NickName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";

        }


        #region Matching API

        // 빠른 매칭 : 빠른 매칭 방에 랜덤 입장
        public void JoinQuickMatchRoom()
        {
            Debug.Log($"[NetworkManager] 빠른 매칭을 시작합니다. 방을 탐색합니다.");
            var exptected = new Hashtable { { RoomProps.MatchType, MatchType.Quick.ToString() } };
            PhotonNetwork.JoinRandomRoom(exptected, MAX_PLAYERS);
        }

        // 빠른 매칭 방 생성 : 빠른 매칭 방에 입장 실패 시 호출
        public void CreateQuickMatchRoom()
        {

            var options = new RoomOptions
            {
                MaxPlayers = MAX_PLAYERS, // 최대 인원 설정
                IsVisible = true, // 로비 노출 여부 
                IsOpen = true, // 입장 가능 여부 -> 게임 시작 시 false로 만들어야 함
                CustomRoomProperties = new Hashtable { { RoomProps.MatchType, MatchType.Quick.ToString() }, {RoomProps.MatchState, MatchState.Matching.ToString()} },
                CustomRoomPropertiesForLobby = new[] { RoomProps.MatchType, RoomProps.MatchState }
            };
            string roomName = $"QUICK-{UnityEngine.Random.Range(100000, 999999)}";
            PhotonNetwork.CreateRoom(roomName, options);
        }




        public void LeaveRoom() => PhotonNetwork.LeaveRoom();
        public void LoadGameScene()
        {
            Debug.Log("[NetworkManager] 게임 씬으로 이동합니다.");
            StartCoroutine(Util_LDH.LoadSceneWithDelay(gameSceneName, 0.5f));
        }

        #endregion
        

        #region Pun Callbacks - Connection

        public override void OnConnectedToMaster()
        {
            Debug.Log("[NetworkManager] 마스터 서버에 연결 완료");
            ConnectedToMaster?.Invoke();
        }

        /// 서버 연결 끊어졌을 때 재접속 시도
        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log("[NetworkManager] 서버 연결 끊어짐. 재접속 시도");
            base.OnDisconnected(cause);
            PhotonNetwork.ConnectUsingSettings(); // 재접속
        }

        #endregion


        #region Pun Callbacks - Room

        // 방 입장시
        public override void OnJoinedRoom()
        {
            Debug.Log($"[NetworkManager] {PhotonNetwork.CurrentRoom.Name} 방에 입장했습니다.");

            JoinedRoom?.Invoke();
            RoomPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }


        /// 다른 플레이어가 방에 새로 입장했을 때 호출
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            
            Debug.Log($"[NetworkManager] {newPlayer.NickName}이 방에 입장했습니다.");
            PlayerEntered?.Invoke(newPlayer);
            RoomPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }


        /// 다른 플레이어가 방에서 나갔을 때 호출
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            PlayerLeft?.Invoke(otherPlayer);
            RoomPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log($"[NetworkManager] 빠른 매칭을 위한 랜덤 방 입장에 실패했습니다. 방을 생성합니다.");
            JoinRandomFailed?.Invoke(returnCode, message);
            CreateQuickMatchRoom();
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[NetworkManager] 방 생성 실패 - {returnCode} : {message}");
        }
        
        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed.TryGetValue(RoomProps.MatchState, out var value) && value is string state)
                MatchStateChanged?.Invoke(state);
        }
        

        #endregion
    }
}