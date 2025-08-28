using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Network
{
    /// <summary>
    /// 빠른 매칭 컨트롤, UI 컨트롤러
    /// - 버튼 클릭 시 빠른 매칭 시작
    /// - 방 입장 후 팝업 생성 / 갱신(상태, 인원, 경과 시간)
    /// - 정원 도달 시 마스터가 matchState=Complete 전파 + 짧은 지연 후 씬 이동
    /// </summary>
    public class QuickMatchController : MonoBehaviour
    {

        [Header("UI")]
        [SerializeField] private Button quickMatchButton;
        
        [Header("Config")]
       
        private UI_Popup_QuickMatch _popupQuickMatch;  // 상태/인원/타이머 표시용 팝업
        private CancellationTokenSource _cts; // 팝업 생명주기 + 컨트롤러 생명주기에 연동될 토큰
        
        public bool starting;  // 중복 시작 방지 플래그
        private bool _requesting; // 빠른 매칭 시작 버튼 중복 연타 방지 플래그

        private void Start()
        {
            // 빠른 매칭 버튼 클릭 이벤트 바인딩
            quickMatchButton.onClick.AddListener(OnClickMatchingStart);
            
        }

        private void OnDestroy()
        {
            // 구독했던 이벤트 모두 해제
            quickMatchButton.onClick.RemoveListener(OnClickMatchingStart);
            Unsubscribe();
        }

        #region 이벤트 구독 / 구독 해제

        private void Subscribe()
        {
            // 방 입장 -> 팝업 생성 및 관리 바인딩
            Manager.Network.JoinedRoom += OnJoinedRoom;

            // 정원 변화 감지 → 마스터만 시작 판단
            Manager.Network.RoomPlayerCountChanged += TryStartGame;     // 마스터 클라이언트가 중간에 변경될 수도 있으므로 모두 구독처리하고 내부에서 마스터만 실행하도록 처리
            
            // 마스터 변경 시
            Manager.Network.MasterClientSwiched += OnMasterClientSwitched;
            
            // 룸 상태 변화 → UI 전환(매칭중 ↔ 매칭완료)
            Manager.Network.MatchStateChanged += OnMatchStateChanged;
            
        }

        private void Unsubscribe()
        {
            if (Manager.Network != null)
            {
                Manager.Network.JoinedRoom -= OnJoinedRoom;
                Manager.Network.RoomPlayerCountChanged -=  TryStartGame;  
                Manager.Network.MasterClientSwiched -= OnMasterClientSwitched;
                Manager.Network.MatchStateChanged -= OnMatchStateChanged;
                
                if (_popupQuickMatch != null)
                    Manager.Network.RoomPlayerCountChanged -= _popupQuickMatch.SetPlayerCount;
            }
            
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }


        #endregion


        #region 빠른 매칭 시작 / 취소

        // 빠른 매칭 시작
        public void OnClickMatchingStart()
        {
            // 1) 이미 방 안이거나 요청 중이면 무시
            if (_requesting || PhotonNetwork.InRoom) return;
            
            // 2) 이벤트 구독
            Subscribe();
            
            _requesting = true;
            MatchController.Instance.SetMatching(Define_LDH.MatchType.Quick, true);
            
            //이미 방에 들어와있으면 중복 호출 방지
            if (PhotonNetwork.CurrentRoom != null) return;
            Manager.Network.JoinQuickMatchRoom();
        }
        
        //빠른 매칭 취소
        public void OnClickMatchCancel()
        {
            Manager.Network.LeaveRoom();
            Unsubscribe();
            
            starting = false;
            _requesting = false;
            
            MatchController.Instance.SetMatching(Define_LDH.MatchType.Quick, false);
        }
        

        #endregion


        #region Slot

        private void AssignSlot()
        {
            
            //마스터가 제일 앞으로 오도록 정렬, 그 후 액터 넘버 순으로 정렬
            var currentPlayers = PhotonNetwork.PlayerList.OrderBy(p => p != PhotonNetwork.MasterClient)
                .ThenBy(p => p.ActorNumber)
                .ToArray();
            
            for (int i = 0; i < currentPlayers.Length; i++)
            {
                if (currentPlayers[i].IsLocal )
                {
                    int slotIndex = i;
                    Debug.Log($"슬롯 인덱스 할당 : {slotIndex}");
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
                    {
                        { Define_LDH.PlayerProps.SlotIndex, slotIndex }
                    });
                    break;
                }
                
             
            }
        }
        

        #endregion


        #region UI Control / Callback
        
        public void SetButtonInteractable(bool interactable)
        {
            if (quickMatchButton != null)
                quickMatchButton.interactable = interactable;
        }
        
        
        // 방 입장 직후 호출
        // 팝업 생성, 초기 상태 / 인원 / 타이머 설정
        private void OnJoinedRoom()
        {
            // 슬롯 배정
            AssignSlot();
            
            // 팝업 생성/표시
            _popupQuickMatch = Manager.UI.CreatePopupUI<UI_Popup_QuickMatch>();
            
            // CTS 재바인딩: 팝업이 닫히거나 파괴되면 자동 취소
            _cts?.Cancel();
            _cts?.Dispose();
            
            var destroyCancelToken = _popupQuickMatch.GetCancellationTokenOnDestroy();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancelToken);

            var ct = _cts.Token;
            
            // 팝업 닫힐 때 -> 매칭 취소 처리
            _popupQuickMatch.OnCloseRequested += (_) => OnClickMatchCancel();
            // 인원 수 UI 실시간 갱신
            Manager.Network.RoomPlayerCountChanged += _popupQuickMatch.SetPlayerCount;
            
            
            // 초기 텍스트/인원/타이머
            _popupQuickMatch.SetStatus(false);
            _popupQuickMatch.SetPlayerCount(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
            
            //경과시간 측정 시작 (비동기)
            TickElapsedAsync(ct).Forget();
            
            
            // 다 됐으면 팝업 활성화
            Manager.UI.ShowPopupUI(_popupQuickMatch).Forget();
        }

        
        /// 1초마다 경과시간을 UI에 반영한다. 팝업이 파괴되면 자동 취소된다.
        private async UniTaskVoid TickElapsedAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                _popupQuickMatch.SetElapsed();
                await UniTask.Delay(1000, cancellationToken: ct);
            }
        }


        /// 마스터만 정원 도달 시 시작 트리거.
        private void TryStartGame(int current, int max)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            if (current == max && !starting)
            {
                starting = true;
                MatchController.Instance.RequestStartGame();
            }
            else if (current < max)
            {
                // 누군가 나가면 다시 대기 모드
                starting = false;
            }
        }

        

        /// 방 상태가 바뀌면 모든 클라에서 UI 전환.
        /// 일정 시간 후 팝업 닫힘 처리
        private void OnMatchStateChanged(string state)
        {
            if (_popupQuickMatch == null) return;

            if (string.Equals(Define_LDH.MatchState.Complete.ToString(), state))
            {
                _popupQuickMatch.SetStatus(true);
                _popupQuickMatch.SetCancelable(false);
                
                // 타이머 종료
                _cts?.Cancel();
                _cts.Dispose();
                _cts = null;
                
                //UI 닫기
                _popupQuickMatch.AutoCloseAfter(MatchController.Instance.startDelaySec, _popupQuickMatch.GetCancellationTokenOnDestroy()).Forget();
            }
            else
            {
                _popupQuickMatch.SetStatus(false);
                _popupQuickMatch.SetCancelable(true);
                starting = false;
            }
        }


        private void OnMasterClientSwitched(Player newMasterClient)
        {
            if(!newMasterClient.IsLocal) return;
            
            AssignSlot();
        }
        
        #endregion
        

        
    
        
        
    }
}