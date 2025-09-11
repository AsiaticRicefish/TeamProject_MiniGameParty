using System;
using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using ExitGames.Client.Photon.StructWrapping;
using LDH_Util;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using static LDH_Util.Define_LDH;
using Random = System.Random;

namespace Network
{
    public partial class NetworkManager : PunSingleton<NetworkManager>
    {
        [Header("Scene Setting")] [SerializeField]
        private string gameSceneName;

        [SerializeField] private string lobbySceneName;

        [SerializeField] private bool autoSyncScene = true;

        // ---- 인증 여부, 로비 진입과 관련 플래그
        private bool _authReady = false;

        //--- matching ---- 
        private MatchType _matchType = MatchType.None;
        private int _quickRetryCount;
        private int _privateRetryCount;
        private bool _isNavigating = false;


        #region Events

        // ------ Events ------ //
        public event Action ConnectedToMaster; // 로딩 씬 UI에서 이벤트 구독할 예정
        public event Action JoinedLobby; // 로비 진입시
        public event Action CreatedRoom; // 방 생성 이벤트
        public event Action JoinedRoom; // 방 입장 이벤트
        public event Action LeftRoom; // 방 퇴장 이벥트
        public event Action<Player> PlayerEntered; // 다른 플레이어 입장 이벤트
        public event Action<Player> PlayerLeft; // 다른 플레이어 퇴장 이벤트
        public event Action<int, int> RoomPlayerCountChanged; // (current, max)
        public event Action<short, string> JoinRandomFailed; // 랜덤 룸 입장 실패 이벤트
        public event Action<short, string> JoinFailed; // 비공개 룸 입장 실패 이벤트
        public event Action<string> MatchStateChanged; // 매치 상태(룸 커스텀 프로퍼티) 변경 이벤트
        public event Action<Player, bool> ReadyStateChanged; // 준비 상태(플레이어 커스텀 프로퍼티) 변경 이벤트

        public event Action<Player, int> SlotIndexChanged;
        public event Action<Player> MasterClientSwiched;

        #endregion


        // 초기화 작업
        protected override void OnAwake()
        {
            PhotonNetwork.AutomaticallySyncScene = autoSyncScene;

            //임시로 awake 시점에 호출
            //if (autoConnectOnAwake)

#if TEST_WITHOUT_LOGIN
            if(SceneManager.GetActiveScene().name.Equals(lobbySceneName))
                ConnectServer();
#endif
        }


        #region Connect Server(로그인 없이 게임 테스트 시 사용할 메서드)

        public void ConnectServer()
        {
#if TEST_WITHOUT_LOGIN
            SetTestNicknameAndID();
            
            if (!PhotonNetwork.IsConnected)
                PhotonNetwork.ConnectUsingSettings();
            else
            {
                TryJoinLobby();
            }
#endif
        }

        // 임시 추가
        //todo: 파이어베이스 연결후 파이어베이스 닉네임을 적용하는 것으로 수정..? 아닌가? + 처음 계정 연동시 닉네임 설정 UI 제공 , 이후 프로필에서 수정가능 
        //지금은 임시 테스트를 위해 닉네임 임시 할당
        public void SetTestNicknameAndID(string nickName = null)
        {
            // 닉네임 자동 설정
            if (string.IsNullOrEmpty(nickName))
                PhotonNetwork.NickName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
            else
                PhotonNetwork.NickName = nickName;
            //아이디 = 닉네임이랑 똑같은 아이디로 부여
            PhotonNetwork.AuthValues = new AuthenticationValues(PhotonNetwork.NickName);
        }

        #endregion


        #region Lobby 진입 관련 로직
        

        private void TryJoinLobby()
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.Log("[NetworkManager] 서버에 연결이 완료되지 않았습니다.");
                return; // 마스터에 아직 연결 안 됐으면 대기
            }

            if (PhotonNetwork.InLobby || PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState == ClientState.JoiningLobby)
            {
                Debug.Log("[NetworkManager] 로비로 진입 중이거나 이미 로비거나 현재 룸에 들어온 상태입니다.");
                return;
            }

            Debug.Log("[NetworkManager] TryJoinLobby -> JoinLobby()");
            PhotonNetwork.JoinLobby();
        }

        #endregion


        #region Quick Matching API
        
        // 빠른 매칭 : 빠른 매칭 방에 랜덤 입장
        public void JoinQuickMatchRoom()
        {
            Debug.Log($"[NetworkManager] 빠른 매칭을 시작합니다. 방을 탐색합니다.");
            var expected = new Hashtable { { RoomProps.MatchType, MatchType.Quick.ToString() } };
            PhotonNetwork.JoinRandomRoom(expected, MaxPlayers);
        }

        // 빠른 매칭 방 생성 : 빠른 매칭 방에 입장 실패 시 호출
        public void CreateQuickMatchRoom()
        {
            if (_matchType != MatchType.None) return;

            _matchType = MatchType.Quick;
            
            string roomName = $"QUICK-{UnityEngine.Random.Range(100000, 999999)}";
            PhotonNetwork.CreateRoom(roomName, SetQuickRoomOptions());
        }
        
        private RoomOptions SetQuickRoomOptions()
        {
            return new RoomOptions
            {
                MaxPlayers = MaxPlayers, // 최대 인원 설정
                IsVisible = true, // 로비 노출 여부 
                IsOpen = true, // 입장 가능 여부 -> 게임 시작 시 false로 만들어야 함
                CleanupCacheOnLeave = true, // 떠날 때 캐시 정리
                EmptyRoomTtl = 0,           // 방이 비는 즉시 삭제
                PlayerTtl = 0,              // 플레이어를 Inactive로 남겨두지 않음
                CustomRoomProperties =
                    new Hashtable
                    {
                        { RoomProps.MatchType, MatchType.Quick.ToString() },
                        { RoomProps.MatchState, MatchState.Matching.ToString() }
                    },
                CustomRoomPropertiesForLobby = new[] { RoomProps.MatchType, RoomProps.MatchState }
            };
            
        }

        #endregion

        #region Private Matching API

        #region Create Private Room Logic

        // 비공개 방 생성
        public void CreatePrivateRoom()
        {
            if (_matchType != MatchType.None) return;

            _matchType = MatchType.Private;
            _privateRetryCount = 0;
            StartCoroutine(TryCreatePrivateRoom());
        }

        private IEnumerator TryCreatePrivateRoom()
        {
            yield return null; // 한 프레임 대기
            _privateRetryCount++;
            string roomCode = Util_LDH.Generate4DigitString();
            string roomName = $"PRIV-{roomCode}";
            PhotonNetwork.CreateRoom(roomName, SetPrivateRoomOptions(roomCode));
        }

        private RoomOptions SetPrivateRoomOptions(string roomCode)
        {
            return new RoomOptions
            {
                MaxPlayers = MaxPlayers, // 최대 인원 설정
                IsVisible = false, // 코드로만 입장하도록 비노출 권장
                IsOpen = true, // 입장 가능 여부 -> 게임 시작 시 false로 만들어야 함
                CleanupCacheOnLeave = true, // 떠날 때 캐시 정리
                EmptyRoomTtl = 0,           // 방이 비는 즉시 삭제
                PlayerTtl = 0,              // 플레이어를 Inactive로 남겨두지 않음
                CustomRoomProperties = new Hashtable
                {
                    { RoomProps.MatchType, MatchType.Private.ToString() },
                    { RoomProps.MatchState, MatchState.Matching.ToString() },
                    { RoomProps.RoomCode, roomCode }
                },
                CustomRoomPropertiesForLobby = new[] { RoomProps.MatchType, RoomProps.MatchState, RoomProps.RoomCode }
            };
        }

        #endregion


        // 비공개 룸 입장
        // 친구초대 보낼 때도 room code를 담아서 보내면 같은 api로 방 입장 시도 가능
        public void JoinPrivateRoomByCode(string code)
        {
            Debug.Log($"[NetworkManager] PRIV-{code}에 입장을 시도합니다.");
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

            // 보존해야 할 키
            var keepKeys = new HashSet<string>(PlayerProps.PlayerInfoKeyDict.Values);
            
            var customProperties = PhotonNetwork.LocalPlayer.CustomProperties;

            var clearProperties = new ExitGames.Client.Photon.Hashtable();

            foreach (DictionaryEntry entry in customProperties)
            {
                var keyStr = entry.Key as string ?? entry.Key?.ToString();
                if (string.IsNullOrEmpty(keyStr)) continue;
                
                if (!keepKeys.Contains(keyStr))
                    clearProperties[keyStr] = null;
            }
            
            if (clearProperties.Count > 0)
                PhotonNetwork.LocalPlayer.SetCustomProperties(clearProperties);
        }

        #endregion


        #region Pun Callbacks - Connection

        public override void OnConnectedToMaster()
        {
            Debug.Log("[NetworkManager] 마스터 서버에 연결 완료");
            TryJoinLobby(); // 로비로 가겠다는 요청이 아니므로 tryjoinlobby를 사용. 로비로 가겠다는 요청이 있었다면 로비로 진입하고 없었다면 로비로 진입하지 않음.
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


        #region Pun Callbacks - Lobby

        public override void OnJoinedLobby()
        {
            Debug.Log("[NetworkManager] JoinedLobby.");

            // 커스텀 프로퍼티 설정
            if (!PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("uid"))
            {
                Debug.Log("프로퍼티 - uid를 설정합니다.");
                var props = new Hashtable { { "uid", PhotonNetwork.AuthValues?.UserId }, };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }


            var current = SceneManager.GetActiveScene().name;
            if (!string.Equals(current, lobbySceneName, System.StringComparison.Ordinal) && !_isNavigating)
            {
                Debug.Log($"[NetworkManager]로비 씬으로 이동합니다.");
                StartCoroutine(Util_LDH.LoadSceneWithDelay(lobbySceneName, 0.5f));
            }
            else
            {
                Debug.Log("[NetworkManager] 현재 로비 씬입니다.");
            }


            JoinedLobby?.Invoke();
        }

        #endregion

        #region Pun Callbacks - Room

        #region 방 입장 / 입장 실패

        public override void OnJoinedRoom()
        {
            Debug.Log($"[NetworkManager] {PhotonNetwork.CurrentRoom.Name} 방에 입장했습니다.");

            PhotonNetwork.AutomaticallySyncScene = autoSyncScene;

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
            
            TryJoinLobby();
        }

        #endregion

        #region 방 퇴장

        public override void OnLeftRoom()
        {
            Debug.Log($"[NetworkManager] 방에서 나갔습니다.");

            PlayerManager.Instance.ClearAllPlayers();
            ClearAllPlayerProperty();
            LeftRoom?.Invoke();

            //로비로 복귀 시도
            TryJoinLobby();
        }

        #endregion

        #region 플레이어 입장 / 퇴장 / 마스터 변경

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


        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            MasterClientSwiched?.Invoke(newMasterClient);
        }

        #endregion

        #region 방 생성 / 방 생성 실패

        public override void OnCreatedRoom()
        {
            Debug.Log($"[NetworkManager] 방 생성 완료(타입 : {_matchType}) : {PhotonNetwork.CurrentRoom.Name}");
            _matchType = MatchType.None;
            CreatedRoom?.Invoke();
        }


        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[NetworkManager] 방 생성 실패(타입 : {_matchType})  - {returnCode} : {message}");

            // 비공개 방 생성 & 방 이름(방 코드) 중복인 경우 재시도
            if (_matchType == MatchType.Private && returnCode == ErrorCode.GameIdAlreadyExists)
            {
                if (_privateRetryCount < PRIVATE_MAX_RETRY)
                {
                    StartCoroutine(TryCreatePrivateRoom());
                    return;
                }
                else
                {
                    Debug.LogWarning($"[NetworkManager] 비공개 방 생성 재시도 횟수 초과 : 시도 횟수 {_privateRetryCount}");
                }
            }

            _matchType = MatchType.None;
        }

        #endregion

        #region 프로퍼티

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed.TryGetValue(RoomProps.MatchState, out var value) && value is string state)
                MatchStateChanged?.Invoke(state);
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.TryGetValue(PlayerProps.ReadyState, out var readyValue) && readyValue is bool isReady)
                ReadyStateChanged?.Invoke(targetPlayer, isReady);

            if (changedProps.TryGetValue(PlayerProps.SlotIndex, out var slotValue) && slotValue is int slotIndex)
                SlotIndexChanged?.Invoke(targetPlayer, slotIndex);
        }

        #endregion

        #endregion
    }
}
