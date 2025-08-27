using System.Linq;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using static LDH_Util.Define_LDH;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Network
{
    /// <summary>
    /// 비공개 매칭 컨트롤, UI 컨트롤러
    /// </summary>
    public class PrivateMatchController : MonoBehaviour
    {
        [SerializeField] private UI_PrivateMatchOptions privateMatchOptions; // 비공개 매칭 선택 패널
        private UI_Popup_PrivateRoom _popupRoom; // 비공개 룸 패널 UI
        private UI_Popup_FriendsList _popupFriends;
        
        private int? _preferredSlotToJoin; // 로컬 입장 시 희망 슬롯(초대 수락 경로로 설정됨)
        
        
        public bool starting;  // 중복 시작 방지 플래그
        private bool _requesting; // 중복 로직 실행 방지 플래그
        private bool _isMaster => PhotonNetwork.IsMasterClient;
        
        
        private void Start()
        {
            privateMatchOptions.CreateRoomButton.onClick.AddListener(RequestCreatePrivateRoom);
            privateMatchOptions.RoomCodeInputField.onEndEdit.AddListener(RequestJoinPrivateRoom);
        }

        private void OnDestroy()
        {
            privateMatchOptions.CreateRoomButton.onClick.RemoveAllListeners();
            privateMatchOptions.RoomCodeInputField.onEndEdit.RemoveAllListeners();
            UnsubscribeNetwork();
        }


        #region 이벤트 구독 / 구독 해제
        
        private void SubscribeNetwork()
        {
            if (Manager.Network == null) return;
            
            Debug.Log("[PrivateMatchController] Subscribe Network 호출. 네트워크 매니저 이벤트 바인딩 처리");
            
            Manager.Network.JoinedRoom += OnJoinedRoom;
            Manager.Network.JoinFailed += OnJoinFailed;
            Manager.Network.PlayerEntered += OnPlayerEnteredRoom;
            Manager.Network.PlayerLeft += OnPlayerLeftRoom;
            Manager.Network.ReadyStateChanged += OnPlayerReadyChanged;
            Manager.Network.SlotIndexChanged  += OnPlayerSlotChanged;
            Manager.Network.MatchStateChanged += OnMatchStateChanged;
            Manager.Network.MasterClientSwiched += OnMasterClientChanged;
        }

        private void UnsubscribeNetwork()
        {
            if (Manager.Network == null) return;
            
            Debug.Log("[PrivateMatchController] Unsubscribe Network 호출. 네트워크 매니저 이벤트 바인딩 해제");
                
            Manager.Network.JoinedRoom -= OnJoinedRoom;
            Manager.Network.JoinFailed -= OnJoinFailed;
            Manager.Network.PlayerEntered -= OnPlayerEnteredRoom;
            Manager.Network.PlayerLeft -= OnPlayerLeftRoom;
            Manager.Network.ReadyStateChanged -= OnPlayerReadyChanged;
            Manager.Network.SlotIndexChanged  -= OnPlayerSlotChanged;
            Manager.Network.MatchStateChanged -= OnMatchStateChanged;
            Manager.Network.MasterClientSwiched -= OnMasterClientChanged;
        }

        
        public void SetButtonInteractable(bool interactable)
        {
            if (privateMatchOptions.PrivateMatchToggle != null)
            {
                privateMatchOptions.PrivateMatchToggle.interactable = interactable;
                privateMatchOptions.PrivateMatchToggle.isOn = false;
            }
              
        }
        #endregion

        #region Invitation


        public void OpenFriendsList()
        {
            Debug.Log($"[PrivateMatchController] 친구 목록 패널 팝업을 생성합니다.");
            // 룸 패널 팝업 ui 생성
            _popupFriends = Manager.UI.CreatePopupUI<UI_Popup_FriendsList>();
            
            //todo: 친구 목록 불러오기 및 설정 코드 추가

            Manager.UI.ShowPopupUI(_popupFriends).Forget();
            
        }

        /// <summary>
        /// 초대 수락(선호 슬롯을 전달받음 -> 선호 슬롯 인덱스 셋팅 후 진입 시도)
        /// </summary>
        public void AcceptInviteAndJoin(string roomCode, int preferredSlot)
        {
            Debug.Log($"[PrivateMatchController] AcceptInviteAndJoin(roomCode:{roomCode}, preferredSlot:{preferredSlot})");
            _preferredSlotToJoin = preferredSlot;
            SubscribeNetwork();
            Manager.Network.JoinPrivateRoomByCode(roomCode);
        }

        #endregion
        
        
        #region Request Create/Join/Leave Room

        private void RequestJoinPrivateRoom(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            if (_requesting || PhotonNetwork.InRoom) return;
            
            Debug.Log($"[PrivateMatchController]  RequestJoinPrivateRoom(input:'{input.Trim()}')");
            
            _requesting = true;
            _preferredSlotToJoin = null; // 일반 입장 → 앞에서부터
            SubscribeNetwork();

            MatchController.Instance.SetMatching(MatchType.Private, true);

            Manager.Network.JoinPrivateRoomByCode(input.Trim());
        }

        private void RequestCreatePrivateRoom()
        {
            if (_requesting || PhotonNetwork.InRoom) return;
            
            Debug.Log($"[PrivateMatchController] RequestCreatePrivateRoom()");
            
            _requesting = true;
            SubscribeNetwork();
            
            
            MatchController.Instance.SetMatching(MatchType.Private, true);
            
            //마스터는 반드시 0번 슬롯에 위치해야 하므로!
            //방에 입장했을 때 로컬에서 자기 선호 슬롯으로 배치될 수 있는지를 판단하고 플레이어 프로퍼티를 설정하기 때문에
            //
            //마스터의 선호 슬롯 인덱스를 0번으로 설정하고 방을 생성한다.
            _preferredSlotToJoin = 0;
            
            Manager.Network.CreatePrivateRoom();
        }
        
        #endregion
        

        #region Room Callback Event - 방 입장 / 퇴장

        // 방 입장
        private void OnJoinedRoom()
        {
            Debug.Log($"[PrivateMatchController] OnJoinedRoom");
            _requesting = false;
            
            
            Debug.Log($"[PrivateMatchController] 룸 패널 팝업을 생성합니다.");
            // 룸 패널 팝업 ui 생성
            _popupRoom = Manager.UI.CreatePopupUI<UI_Popup_PrivateRoom>();

            // -------- UI 설정 및 바인딩 -----------
            // 1) 방 코드 설정
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Define_LDH.RoomProps.RoomCode, out var value) &&
                value is string roomCode)
            {
                Debug.Log($"[PrivateMatchController] Room code set: {roomCode}");
                _popupRoom.SetRoomCode(roomCode);
            }

            // 2) 나가기 버튼 연결
            Debug.Log($"[PrivateMatchController] 나가기 버튼 이벤트 바인딩(OnClosedRequested -> OnClickLeaveRoom)");
            _popupRoom.OnCloseRequested += (_) => OnClickLeaveRoom();
            
            
            // 3) 패널 초기화 및 슬롯 인덱스 설정
            Debug.Log($"[PrivateMatchController] ResetAllSlots(canInvite:{_isMaster})");
            _popupRoom.ResetAllSlots(_isMaster);
            
            
            // 4) 패널 클릭 이벤트 바인딩
            BindAllPanelEvents();
            
            // 5) 로컬 플레이어의 초기 ReadyState를 false로 설정
            Debug.Log($"[PrivateMatchController] Local ReadyState set to false");
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable()
            {
                { PlayerProps.ReadyState, false }
            });
            
            // 6) 로컬 플레이어의 슬롯 확정 및 바인딩(선호 슬롯 or 앞자리부터)
            AssignSlotIndex(PhotonNetwork.LocalPlayer);

            
            // 7) 초기 UI 빌드
            //RebuildAllPanels();
            
            
            // 모든 설정이 완료됐다면 UI를 표시한다.
            Debug.Log($"[PrivateMatchController] Popup shown");
            Manager.UI.ShowPopupUI(_popupRoom).Forget();
        }

        // 방 입장 실패
        private void OnJoinFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[PrivateMatchController] 비공개 룸 입장 실패");
            Manager.UI.EnqueueToast($"({returnCode}) {message}");
            
            UnsubscribeNetwork();
            
            MatchController.Instance.SetMatching(MatchType.Private, false);
            
            _requesting = false;
 
        }

        
        // 방 퇴장
        private void OnClickLeaveRoom()
        {
            Debug.Log($"[PrivateMatchController] 나가기 버튼 클릭 -> 방 나가기 및 설정 정리");
            
            UnbindAllPanelEvents();  
            Manager.Network.LeaveRoom();
            UnsubscribeNetwork();

            MatchController.Instance.SetMatching(MatchType.Private, false);
            _requesting = false;
            
        }

        #endregion
        
        #region  Player Enter/Leave/Props/Master Change

        private void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[PrivateMatchController] {newPlayer.NickName}가 입장했습니다.");
            // 슬롯 인덱스 설정이 완료된 플레이어면 패널 UI 빌드
            int slotIndex = GetSlotIndex(newPlayer);
            Debug.Log($"[PrivateMatchController] Entered player slot:{slotIndex}");
            
            if (slotIndex >= 0)
                BuildPanel(slotIndex, newPlayer);
                
        }

        private void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[PrivateMatchController] {otherPlayer.NickName}가 퇴장했습니다.");
           RebuildAllPanels();
        }

        private void OnMasterClientChanged(Player newMaster)
        {
            if(!newMaster.IsLocal) return;
            
            //로컬이고 마스터가 된 경우
            //슬롯을 0번으로 옮기기 위해 선호 슬롯을 정해주고 assgin을 다시 한다.(강제)
            _preferredSlotToJoin = 0;
            AssignSlotIndex(newMaster, true);
        }
        
        private void OnPlayerReadyChanged(Player targetPlayer, bool isReady)
        {
            Debug.Log($"[PrivateMatchController] OnPlayerReadyChanged({targetPlayer.NickName}, ready:{isReady})");

            int slotIdx = GetSlotIndex(targetPlayer);
            _popupRoom[slotIdx]?.SetReadyVisual(isReady);
            
            if(PhotonNetwork.IsMasterClient)
                TryStartGame();
        }

        private void OnPlayerSlotChanged(Player targetPlayer, int newSlot)
        {
            Debug.Log($"[PrivateMatchController] OnPlayerSlotChanged({targetPlayer.NickName}, newSlot:{newSlot})");
            RebuildAllPanels();
        }

        #endregion
        
        
        #region Panel Binding

        private void BindAllPanelEvents()
        {
            Debug.Log($"[PrivateMatchController] BindAllPanelEvents()");
            
            if (_popupRoom?.PlayerPanels == null) return;

            foreach (var panel in _popupRoom.PlayerPanels)
            {
                if (panel == null) continue;
                panel.InviteButtonClicked += OnClickInviteButton;
                panel.ReadyClicked   += OnClickReady;
            }
        }
        
        private void UnbindAllPanelEvents()
        {
            Debug.Log($"[PrivateMatchController] UnbindAllPanelEvents()");
            if (_popupRoom?.PlayerPanels == null) return;

            foreach (var panel in _popupRoom.PlayerPanels)
            {
                if (panel == null) continue;
                panel.InviteButtonClicked -= OnClickInviteButton;
                panel.ReadyClicked   -= OnClickReady;
            }
        }

        #endregion

        #region UI Events Handlers

        private void OnClickInviteButton(int slot)
        {
            Debug.Log($"[PrivateMatchController] OnClickInviteButton(slot:{slot})");
            
            // 마스터만 초대 허용 + 빈 슬롯이어야 의미 있음
            if (!PhotonNetwork.IsMasterClient) return;
            Debug.Log($"[PrivateMatchController] 마스터이므로 초대가 가능합니다.");
            
            if (!IsSlotFree(slot)) return;
            Debug.Log($"[PrivateMatchController] 슬롯이 비어있으므로 초대가 가능합니다.");

            // TODO: 친구 목록 팝업 열기 후, 선택 시 실제 초대 전송 (roomCode, preferredSlot=slot)
            
            OpenFriendsList();
            
            // FriendsPopup.Open(code, slot, onInvite=SendInvite)
            // Manager.UI.EnqueueToast($"Invite requested for slot {slot}");
        }

        private void OnClickReady(int slot)
        {
            Debug.Log($"[PrivateMatchController] OnClickReady(slot:{slot}");
            
            int mySlot = GetSlotIndex(PhotonNetwork.LocalPlayer);
            if (mySlot != slot)
            {
                Debug.Log($"[PrivateMatchController] Ready toggle ignored! mySlot:{mySlot} != clicked:{slot}");
                return;
            }
            
            
            bool now = GetReady(PhotonNetwork.LocalPlayer);
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { PlayerProps.ReadyState, !now }
            });
            Debug.Log($"[PrivateMatchController] Ready toggled: {now} -> {!now}");
        }
        

        #endregion

        #region UI Rebuild / Sync

        private void RebuildAllPanels()
        {
            Debug.Log($"[PrivateMatchController] RebuildAllPanels()");
            
            if (_popupRoom?.PlayerPanels == null) return;
            
            // 모든 패널 초기화
            Debug.Log($"[PrivateMatchController] 모든 패널을 초기화합니다.");
            _popupRoom.ResetAllSlots(_isMaster);
            
            
            // 각 플레이어의 패널 설정해주기
            foreach (var pl in PhotonNetwork.PlayerList)
            {
                int idx = GetSlotIndex(pl);
                if (idx < 0 && pl.IsLocal)
                {
                    Debug.Log($"[PrivateMatchController] Local has no slot — AssignSlotIndex({pl.NickName})");
                    AssignSlotIndex(pl);
                }

                else
                {
                    //UI 갱신
                    BuildPanel(idx, pl);
                }
            }
        }

        private void BuildPanel(int slotIdx, Player pl)
        {
            //UI 갱신
            Debug.Log($"[PrivateMatchController] BuildPanel(slot:{slotIdx}, player:{pl.NickName}), ready:{GetReady(pl)}, isLocal:{pl.IsLocal})");

           _popupRoom.SetPlayerPanel(slotIdx, GetReady(pl), pl.IsLocal, pl.IsMasterClient);
        }
        
        
        #endregion
        
        
        #region Slot

        // 슬롯 배정
        private int AssignSlotIndex(Player p, bool force = false)
        {
            
            Debug.Log($"[PrivateMatchController] AssignSlotIndex({p.NickName}))");
            int slotIndex = -1;
            
            // 1) 선호 슬롯 사용 시도
            if (_preferredSlotToJoin.HasValue && (force || IsSlotFree(_preferredSlotToJoin.Value)))
            {
                slotIndex = _preferredSlotToJoin.Value;
                Debug.Log($"[PrivateMatchController] 선호 슬롯 사용 가능 : {_preferredSlotToJoin.Value}");
            }


            // 2) 선호 슬롯이 없거나 점유됐다면, 앞에서부터 배치 시도
            else
            {
                slotIndex = GetFirstEmptySlotIndex();
                Debug.Log($"[PrivateMatchController] 선호 슬롯 없거나 사용 불가하여 빈 슬롯 찾음 : {slotIndex}");
            }
             
            
            // slot Index로 플레이어 속성 설정해주기
            AssignSlot(p, slotIndex);
            _preferredSlotToJoin = null;
            
            return slotIndex;
        }
        private void AssignSlot(Player player, int slotIndex)
        {
            if (!player.IsLocal) return;
            
            Debug.Log($"[PrivateMatchController] 로컬 플레이어를 대상으로 AssignSlot({player.NickName}, slot:{slotIndex})");
            
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { PlayerProps.SlotIndex, slotIndex }
            });
        }

        
        /// <summary>
        /// 플레이어의 커스텀 프로퍼티 중 슬롯 인덱스의 값을 반환
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private int GetSlotIndex(Player p)
        {
            if (p?.CustomProperties == null) return -1;
            return (p.CustomProperties.TryGetValue(PlayerProps.SlotIndex, out var v) && v is int idx) ? idx : -1;
        }

       
        private static bool GetReady(Player p)
        {
            if (p?.CustomProperties == null) return false;
            return p.CustomProperties.TryGetValue(PlayerProps.ReadyState, out var v) && v is bool b && b;
        }
        
        
        /// <summary>
        /// 슬롯의 점유 여부 반환
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        // private bool IsSlotFree(int slot) => !(_popupRoom.PlayerPanels[slot].IsOccupied);
        private bool IsSlotFree(int slot) =>
            !PhotonNetwork.PlayerList.Any(pl => GetSlotIndex(pl) == slot); //UI 패널의 IsOccupied에 의존하면, 이벤트 순서상 잠깐 어긋나는 타이밍에 잘못 판단할 수 있어서 ->  슬롯 점유/빈자리 판단은 항상 Photon 커스텀 프로퍼티로 계산한다.

        /// <summary>
        /// 비어있는 슬롯 중 가장 앞의 슬롯의 인덱스를 반환한다. 없으면 -1 반환
        /// </summary>
        /// <returns></returns>
        private int GetFirstEmptySlotIndex()
        {
            int idx = -1;

            for (int i = 0; i < _popupRoom.PlayerPanels.Count; i++)
            {
                if (IsSlotFree(i))
                {
                    idx = i;
                    break;
                }
            }
            return idx;
        }
        
        #endregion


        #region 매치 상태 / 시작
        
        // 마스터만 게임 시작 가능한지 체크
        private void TryStartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            Debug.Log($"[PrivateMatchController] 마스터 클라이언트가 게임 시작 가능한지 체크합니다.");
   
            // 모두 Ready인지 체크하기
            var players = PhotonNetwork.PlayerList;
            if (players.Length != MAX_PLAYERS)
            {
                Debug.Log($"[PrivateMatchController] TryStartGame blocked — players:{players.Length}/{MAX_PLAYERS}");
                return;
            }

            bool allReady = players.All(GetReady);

            if (allReady && !starting)
            {
                Debug.Log($"[PrivateMatchController] 모든 플레이어가 준비 완료되어 게임 시작을 요청합니다. -> RequestStartGame()");
                starting = true;
                MatchController.Instance.RequestStartGame();
            }
            else
            {
                starting = false;
                Debug.Log($"[PrivateMatchController] 아직 모든 플레이어가 준비 완료하지 않았습니다.");
            }
        }
        
        
        private void OnMatchStateChanged(string state)
        {
            if (string.Equals(Define_LDH.MatchState.Complete.ToString(), state))
            {
                Debug.Log($"[PrivateMatchController] MatchState가 {state}로 변경되었습니다. 사용자 입력을 막고 UI 자동 닫기를 처리합니다.");
                // 레디 버튼, 초대 버튼, 나가기 버튼 interactable 못하게 처리
                foreach (var playerPanel in _popupRoom.PlayerPanels)
                {
                    playerPanel.SetInteractableAll(false);
                }
                
                //UI 자동 닫기
                _popupRoom.AutoCloseAfter(MatchController.Instance.startDelaySec, _popupRoom.GetCancellationTokenOnDestroy()).Forget();
            }
            else
            {
                Debug.Log($"[PrivateMatchController] MatchState가 {state}로 변경되었습니다. ");
               
            }

        }
        #endregion
    }

}
