using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Network
{
    
    /// <summary>
    /// 비공개 매칭 컨트롤, UI 컨트롤러
    /// - 방 생성 버튼 클릭 시 비공개 룸 생성
    /// - 코드 입력 input field 클릭 시 input field에 방 코드 입력 가능
    /// - 모든 플레이어가 준비 상태가 되면 게임 시작
    /// </summary>
    public class PrivateMatchController  : MonoBehaviour
    {
        
        //UI
        [SerializeField] private UI_PrivateMatchOptions privateMatchOptions;
        private UI_Popup_PrivateRoom _popupPrivateRoom;
        
        
        //flag
        private bool _requesting; // 빠른 매칭 시작 버튼 중복 연타 방지 플래그
        
        
        private void Start()
        {
            privateMatchOptions.CreateRoomButton.onClick.AddListener(RequestCreatePrivateRoom);
            privateMatchOptions.RoomCodeInputField.onEndEdit.AddListener(RequestJoinPrivateRoom);
        }

        private void OnDestroy()
        {
            privateMatchOptions.RoomCodeInputField.onEndEdit.RemoveAllListeners();
            
            Unsubscribe();
        }


        #region 이벤트 구독 / 구독 해제

        private void Subscribe()
        {
            Manager.Network.JoinedRoom += OnJoinedRoom;
            Manager.Network.JoinFailed += OnJoinFailed;
           // Manager.Network.PlayerEntered += ____;  // 다른 플레이어 입장 시 패널 추가..? 플레이어 패널
           // Manager.Network.PlayerExited += _____; // 다른 플레이어 입장 시 패널 삭제;
           Manager.Network.ReadyStateChanged += OnPlayerStateChanged;

        }

        private void Unsubscribe()
        {
            Manager.Network.JoinedRoom -= OnJoinedRoom;
            Manager.Network.JoinFailed -= OnJoinFailed;
            // Manager.Network.PlayerEntered += ____;  // 다른 플레이어 입장 시 패널 추가..? 플레이어 패널
            // Manager.Network.PlayerExited += _____; // 다른 플레이어 입장 시 패널 삭제;
            Manager.Network.ReadyStateChanged -= OnPlayerStateChanged;
        }


        #endregion
        
        private void RequestJoinPrivateRoom(string input)
        {
            Debug.Log("방코드 제출");
            Subscribe();
            Manager.Network.JoinPrivateRoomByCode(input.Trim());
        }

        private void RequestCreatePrivateRoom()
        {
            if(_requesting || PhotonNetwork.InRoom) return;
            privateMatchOptions.PrivateMatchToggle.isOn = false;
            privateMatchOptions.PrivateMatchToggle.interactable = false;
            
            Subscribe();
            Manager.Network.CreatePrivateRoom();
        }

      
        private void OnJoinedRoom()
        {
            _requesting = false;
         
            
            // 룸 패널 Ui 생성
            _popupPrivateRoom = Manager.UI.CreatePopupUI<UI_Popup_PrivateRoom>();

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Define_LDH.RoomProps.RoomCode, out var value) && value is string roomCode)
            {
                _popupPrivateRoom.SetRoomCode(roomCode);
            }
      
            _popupPrivateRoom.OnCloseRequested += (_) => OnClickLeaveRoom();
            
            Manager.UI.ShowPopupUI(_popupPrivateRoom).Forget();
            // 현재 입장한 플레이어 리스트를 기반으로 플레이어 패널 설정
            
        }

        private void OnJoinFailed(short returnCode, string message)
        {
            Manager.UI.EnqueueToast($"({returnCode}) {message}");
        }
        
        private void OnClickLeaveRoom()
        {
            Manager.Network.LeaveRoom();
            Unsubscribe();

            _requesting = false;
            privateMatchOptions.PrivateMatchToggle.interactable = true;
        }



        private void OnPlayerEnteredRoom()
        {
            // 들어온 플레이어 정보를 플레이어 패널에 반영
        }

        private void OnPlayerExitRoom()
        {
            // 나간 플레이어 정보에 따라 플레이어 패널 삭제
            
        }


        private void OnPlayerStateChanged(Player targetPlayer, bool isReady)
        {
            
        }
    }
}