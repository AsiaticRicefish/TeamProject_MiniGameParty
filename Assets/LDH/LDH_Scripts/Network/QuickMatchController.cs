using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
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
        private float _startDelaySec = 0.8f;
        private UI_Popup_QuickMatch _popup;  // 상태/인원/타이머 표시용 팝업
        private CancellationTokenSource _cts; // 팝업 생명주기 + 컨트롤러 생명주기에 연동될 토큰
        private bool _starting;  // 중복 시작 방지 플래그
        
        
        private void Start() => Subscribe();

        private void OnDestroy() => Unsubscribe();

        #region 이벤트 구독 / 구독 해제

        private void Subscribe()
        {
            // 빠른 매칭 버튼 클릭 이벤트 바인딩
            quickMatchButton.onClick.AddListener(OnClickMatchingStart);

            // 방 입장 -> 팝업 생성 및 관리 바인딩
            Manager.Network.JoinedRoom += OnJoinedRoom;

            // 정원 변화 감지 → 마스터만 시작 판단
            Manager.Network.RoomPlayerCountChanged += TryStartGame;     // 마스터 클라이언트가 중간에 변경될 수도 있으므로 모두 구독처리하고 내부에서 마스터만 실행하도록 처리
            
            // 룸 상태 변화 → UI 전환(매칭중 ↔ 매칭완료)
            Manager.Network.MatchStateChanged += OnMatchStateChanged;
            
        }

        private void Unsubscribe()
        {
            // 구독했던 이벤트 모두 해제
            
            quickMatchButton.onClick.RemoveListener(OnClickMatchingStart);

            if (Manager.Network != null)
            {
                Manager.Network.JoinedRoom -= OnJoinedRoom;
                Manager.Network.RoomPlayerCountChanged -=  TryStartGame;  
                Manager.Network.MatchStateChanged -= OnMatchStateChanged;
                if (_popup != null)
                    Manager.Network.RoomPlayerCountChanged -= _popup.SetPlayerCount;
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
            Manager.Network.JoinQuickMatchRoom();
        }
        
        //빠른 매칭 취소
        public void OnClickMatchCancel()
        {
            Manager.Network.LeaveRoom();
            _cts?.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        

        #endregion


        #region UI Control
        
        // 방 입장 직후 호출
        // 팝업 생성, 초기 상태 / 인원 / 타이머 설정
        private void OnJoinedRoom()
        {
            // 팝업 생성/표시
            _popup = Manager.UI.CreatePopupUI<UI_Popup_QuickMatch>();
            
            // CTS 재바인딩: 팝업이 닫히거나 파괴되면 자동 취소
            _cts?.Cancel();
            _cts?.Dispose();
            
            var destroyCancelToken = _popup.GetCancellationTokenOnDestroy();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancelToken);

            var ct = _cts.Token;
            
            // 팝업 닫힐 때 -> 매칭 취소 처리
            _popup.OnCloseRequested += (_) => OnClickMatchCancel();
            // 인원 수 UI 실시간 갱신
            Manager.Network.RoomPlayerCountChanged += _popup.SetPlayerCount;
            
            
            // 초기 텍스트/인원/타이머
            _popup.SetStatus(false);
            _popup.SetPlayerCount(PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
            
            //경과시간 측정 시작 (비동기)
            TickElapsedAsync(ct).Forget();
            
            // 다 됐으면 팝업 활성화
            Manager.UI.ShowPopupUI(_popup).Forget();
        }

        
        /// 1초마다 경과시간을 UI에 반영한다. 팝업이 파괴되면 자동 취소된다.
        private async UniTaskVoid TickElapsedAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                _popup.SetElapsed();
                await UniTask.Delay(1000, cancellationToken: ct);
            }
        }


        /// 마스터만 정원 도달 시 시작 트리거.
        private void TryStartGame(int current, int max)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            if (current == max && !_starting)
            {
                _starting = true;
                RequestStartGame();
            }
            else if (current < max)
            {
                // 누군가 나가면 다시 대기 모드
                _starting = false;
            }
        }

        
        /// 방 상태를 Complete로 전파하고(취소 불가), 짧은 지연 후 씬 로드.
        /// 중간 이탈이 있으면 상태를 Matching으로 롤백.
        private void RequestStartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            var room = PhotonNetwork.CurrentRoom;
            if (room == null) return;
            
            Debug.Log("[QuickMatchController] 매칭 완료.");
            
            // 입장 차단 + 상태 전파
            room.IsOpen = false;
            room.SetCustomProperties( new Hashtable { { Define_LDH.RoomProps.MatchState, Define_LDH.MatchState.Complete.ToString() } });


            StartCoroutine(StartGameWithDelay());
        }

        /// 매칭 완료 연출 시간만큼 기다렸다가 씬 이동
        private IEnumerator StartGameWithDelay()
        {
            Debug.Log("[QuickMatchController] 마스터 클라이언트에서 게임을 시작합니다.");
            yield return new WaitForSeconds(_startDelaySec);
            
            
            // 안전 재검증(이탈 대비)
            var room = PhotonNetwork.CurrentRoom;
            if (room != null &&
                room.PlayerCount == room.MaxPlayers &&
                Equals(room.CustomProperties[Define_LDH.RoomProps.MatchState], Define_LDH.MatchState.Complete.ToString()))
            {
                
                Manager.Network.LoadGameScene();
            }
            else
            {
                // 롤백
                if (room != null)
                {
                    room.IsOpen = true;
                    room.SetCustomProperties(new Hashtable {
                        { Define_LDH.RoomProps.MatchState, Define_LDH.MatchState.Matching.ToString() }
                    });
                }
                _starting = false;
            }
            
        }

        /// 방 상태가 바뀌면 모든 클라에서 UI 전환.
        /// 일정 시간 후 팝업 닫힘 처리
        private void OnMatchStateChanged(string state)
        {
            if (_popup == null) return;

            if (string.Equals(Define_LDH.MatchState.Complete.ToString(), state))
            {
                _popup.SetStatus(true);
                _popup.SetCancelable(false);
                
                // 타이머 종료
                _cts?.Cancel();
                _cts.Dispose();
                _cts = null;
                
                //UI 닫기
                _popup.AutoCloseAfter(_startDelaySec, _popup.GetCancellationTokenOnDestroy()).Forget();
            }
            else
            {
                _popup.SetStatus(false);
                _popup.SetCancelable(true);
                _starting = false;
            }
        }

        #endregion
        

        
    
        
        
    }
}