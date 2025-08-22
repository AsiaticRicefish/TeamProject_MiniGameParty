using System;
using System.Collections;
using DesignPattern;
using LDH_Util;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using static LDH_Util.Define_LDH;

namespace Network
{
    public partial class NetworkManager : PunSingleton<NetworkManager>
    {
        [Header("Scene Setting")]
        [SerializeField] private string gameSceneName;
        [SerializeField] private bool autoSyncScene = true;
        
        private MatchType _createType = MatchType.None;
        //--- private matching ---- 
        private int _privateRetryCount;

        #region Events

        // ------ Events ------ //
        public event Action ConnectedToMaster; // 로딩 씬 UI에서 이벤트 구독할 예정

        public event Action CreatedRoom;      // 방 생성 이벤트
        public event Action JoinedRoom;       // 방 입장 이벤트
        public event Action LeftRoom;         // 방 퇴장 이벥트
        public event Action<Player> PlayerEntered;      // 다른 플레이어 입장 이벤트
        public event Action<Player> PlayerLeft;         // 다른 플레이어 퇴장 이벤트
        public event Action<int, int> RoomPlayerCountChanged; // (current, max)
        public event Action<short, string> JoinRandomFailed;    // 랜덤 룸 입장 실패 이벤트
        public event Action<short, string> JoinFailed;    // 비공개 룸 입장 실패 이벤트
        public event Action<string> MatchStateChanged;      // 매치 상태(룸 커스텀 프로퍼티) 변경 이벤트

        public event Action<Player, bool> ReadyStateChanged;     // 준비 상태(플레이어 커스텀 프로퍼티) 변경 이벤트
        


        #endregion
        

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


        #region Quick Matching API

        // 빠른 매칭 : 빠른 매칭 방에 랜덤 입장
        public void JoinQuickMatchRoom()
        {
            Debug.Log($"[NetworkManager] 빠른 매칭을 시작합니다. 방을 탐색합니다.");
            var expected = new Hashtable { { RoomProps.MatchType, MatchType.Quick.ToString() } };
            PhotonNetwork.JoinRandomRoom(expected, MAX_PLAYERS);
        }

        // 빠른 매칭 방 생성 : 빠른 매칭 방에 입장 실패 시 호출
        public void CreateQuickMatchRoom()
        {
            if(_createType!= MatchType.None) return;
            
            _createType = MatchType.Quick;

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
        
        
        #endregion
        
        
        #region Private Matching API

        #region Create Private Room Logic

        // 빠른 매칭 방 생성 : 빠른 매칭 방에 입장 실패 시 호출
        public void CreatePrivateRoom()
        {
            if(_createType!= MatchType.None) return;
            
            _createType = MatchType.Private;
            _privateRetryCount = 0;
            StartCoroutine(TryCreatePrivateRoom());
        }

        private IEnumerator TryCreatePrivateRoom()
        {
            yield return null; // 한 프레임 대기
            
            string roomCode = Util_LDH.Generate4DigitString();
            string roomName = $"PRIV-{roomCode}";
            PhotonNetwork.CreateRoom(roomName, SetPrivateRoomOptions(roomCode));
        }
        
        private RoomOptions SetPrivateRoomOptions(string roomCode)
        {
            return new RoomOptions
            {
                MaxPlayers = MAX_PLAYERS, // 최대 인원 설정
                IsVisible    = false,     // 코드로만 입장하도록 비노출 권장
                IsOpen = true, // 입장 가능 여부 -> 게임 시작 시 false로 만들어야 함
                CustomRoomProperties = new Hashtable
                {
                    { RoomProps.MatchType, MatchType.Private.ToString() }, 
                    {RoomProps.MatchState, MatchState.Matching.ToString()},
                    {RoomProps.RoomCode, roomCode}
                },
                CustomRoomPropertiesForLobby = new[] { RoomProps.MatchType, RoomProps.MatchState, RoomProps.RoomCode }
            };
        }

        #endregion
       
        
        // 비공개 룸 입장
        // 친구초대 보낼 때도 room code를 담아서 보내면 같은 api로 방 입장 시도 가능
        public void JoinPrivateRoomByCode(string code)
        {
            PhotonNetwork.JoinRoom($"PRIV-{code}");
        }
        
        #endregion



        #region Start Game / Leave Room API

        public void LeaveRoom() => PhotonNetwork.LeaveRoom();
        public void LoadGameScene()
        {
            Debug.Log("[NetworkManager] 게임 씬으로 이동합니다.");
            StartCoroutine(Util_LDH.LoadSceneWithDelay(gameSceneName, 0.5f));
        }
        

        #endregion

        #region Properties control

        public static void ClearAllPlayerProperty()
        {
            Debug.Log("[NetworkManager] 모든 플레이어 커스텀 프로퍼티를 초기화합니다.");
            
            var customProperties = PhotonNetwork.LocalPlayer.CustomProperties;

            var clearProperties = new ExitGames.Client.Photon.Hashtable();

            foreach (var key in customProperties.Keys)
            {
                clearProperties[key] = null;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(clearProperties);
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

        // ---- 방 입장 / 입장 실패 ---- 
        public override void OnJoinedRoom()
        {
            Debug.Log($"[NetworkManager] {PhotonNetwork.CurrentRoom.Name} 방에 입장했습니다.");

            JoinedRoom?.Invoke();
            RoomPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }
        
              
        // 랜덤 룸 입장 실패 (빠른 매칭)
        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log($"[NetworkManager] 빠른 매칭을 위한 랜덤 방 입장에 실패했습니다. 방을 생성합니다.");
            JoinRandomFailed?.Invoke(returnCode, message);
            CreateQuickMatchRoom();
        }
        
        // 랜덤 룸 입장 실패 (빠른 매칭)
        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.Log($"[NetworkManager] 비공개 방 입장에 실패했습니다. ({returnCode}) {message}");
            JoinFailed?.Invoke(returnCode, message);
        }
        
          


        // ---- 방 퇴장 -------
        public override void OnLeftRoom()
        {
            Debug.Log($"[NetworkManager] 방에서 나갔습니다.");
            
            ClearAllPlayerProperty();
            
            LeftRoom?.Invoke();
        }


        /// ------- 플레이어 입장 / 퇴장 -----------
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            
            Debug.Log($"[NetworkManager] {newPlayer.NickName}이 방에 입장했습니다.");
            PlayerEntered?.Invoke(newPlayer);
            RoomPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }
        
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            PlayerLeft?.Invoke(otherPlayer);
            RoomPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }

        
  
        
        // ------ 방 생성 

        public override void OnCreatedRoom()
        {
            Debug.Log($"[NetworkManager] 방 생성 완료(타입 : {_createType}) : {PhotonNetwork.CurrentRoom.Name}");
            _createType = MatchType.None;
            CreatedRoom?.Invoke();
        }
        
        
        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[NetworkManager] 방 생성 실패(타입 : {_createType})  - {returnCode} : {message}");
            
            // 비공개 방 생성 & 방 이름(방 코드) 중복인 경우 재시도
            if (_createType == MatchType.Private && returnCode == ErrorCode.GameIdAlreadyExists)
            {
                if (_privateRetryCount++ < PRIVATE_MAX_RETRY)
                {
                    StartCoroutine(TryCreatePrivateRoom());
                    return;
                }
                else
                {
                    Debug.LogWarning($"[NetworkManager] 비공개 방 생성 재시도 횟수 초과 : 시도 횟수 {_privateRetryCount}");
                }
            }

            _createType = MatchType.None;

        }
        
        
        // ------ 프로퍼티 관련 -------- //
        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed.TryGetValue(RoomProps.MatchState, out var value) && value is string state)
                MatchStateChanged?.Invoke(state);
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.TryGetValue(PlayerProps.ReadyState, out var value) && value is bool isReady)
                ReadyStateChanged?.Invoke(targetPlayer, isReady);
        }

        #endregion
        
    }
}