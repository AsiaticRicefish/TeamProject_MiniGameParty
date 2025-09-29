using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using LDH_UI;
using LDH_Util;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using static LDH_Util.Define_LDH;

namespace Network
{
    public class MatchController : MonoBehaviour
    {
        private static MatchController _instance;
        public static MatchController Instance => _instance;

        // 매칭 모드
        public MatchType CurrentMatchType { get; private set; } = MatchType.None;
        public bool IsMatching { get; private set; }
        public event Action<MatchType, bool> MatchTypeChanged;


        [Header("Match Controller")] public QuickMatchController QuickMatch;
        public PrivateMatchController PrivateMatch;
        public float startDelaySec = 0.8f;


        // 취소 플래그
        private bool _leavingByAbort = false;
        private CancellationTokenSource _startCts; // 시작 진행 취소용


        [Header("Loading UI")] [SerializeField]
        private UI_LoadingTheme loadingTheme; // 테마

        private UI_Loading _uiLoading; // 전환 로딩창


        private void Awake()
        {
            _instance = this;
            _leavingByAbort = false;
        }

        private void Start()
        {
            QuickMatch ??= GetComponent<QuickMatchController>();
            PrivateMatch ??= GetComponent<PrivateMatchController>();

            Subscribe();
        }

        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            PhotonNetwork.NetworkingClient.StateChanged += OnPhotonStateChanged;
            Manager.Network.MatchStateChanged += CloseMatchingPanelAndShowLoading;
        }

        private void Unsubscribe()
        {
            PhotonNetwork.NetworkingClient.StateChanged -= OnPhotonStateChanged;
            if (Manager.Network)
                Manager.Network.MatchStateChanged -= CloseMatchingPanelAndShowLoading;
        }


        public void SetMatching(MatchType type, bool isMatching)
        {
            IsMatching = isMatching;
            CurrentMatchType = isMatching ? type : MatchType.None;

            MatchTypeChanged?.Invoke(type, isMatching);
            RefreshButtons();
        }

        #region PlayerCount

#if TEST_PLAYER_COUNT
        public void ShowPlayerCount(MatchType matchType)
        {
            if (IsMatching) return;

            SetMatching(matchType, true);

            var playerCount = Manager.UI.CreatePopupUI<UI_Popup_PlayerCount>();
            Manager.UI.ShowPopupUI(playerCount).Forget();
        }


        public void StartMatching()
        {
            switch (CurrentMatchType)
            {
                case MatchType.Quick:
                    QuickMatch.OnClickMatchingStart();
                    break;
                case MatchType.Private:
                    PrivateMatch.RequestCreatePrivateRoom();
                    break;
                default:
                    return;
            }
        }

        public void CancelMatching()
        {
            // Debug.Log($"<color=pink>{CurrentMatchType} 매칭 취소</color>");
            SetMatching(CurrentMatchType, false);
        }

#endif

        #endregion


        #region Matching Button Control

        private void OnPhotonStateChanged(ClientState prev, ClientState curr)
        {
            // Debug.Log($"<color=blue>{prev} -> {curr}</color>");
            RefreshButtons();
        }


        private bool ReadyToMatch() =>
            PhotonNetwork.IsConnected && PhotonNetwork.InLobby && !PhotonNetwork.InRoom && !IsMatching;

        private void RefreshButtons()
        {
            // Debug.Log("<color=red>refresh button</color>");
            bool ready = ReadyToMatch();

            // Debug.Log($"<color=green>[MatchController] isconnected :{PhotonNetwork.IsConnected} / inlobby : {PhotonNetwork.InLobby} / inroom : {PhotonNetwork.InRoom} / is maching : {IsMatching}</color>");
            QuickMatch?.SetButtonInteractable(ready);
            PrivateMatch?.SetButtonInteractable(ready);
        }

        #endregion


        #region Game Start Logic

        /// 방 상태를 Complete로 전파하고(취소 불가), 짧은 지연 후 씬 로드.
        /// 중간 이탈이 있으면 상태를 Matching으로 롤백.
        public void RequestStartGame()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            var room = PhotonNetwork.CurrentRoom;
            if (room == null) return;
            if (_leavingByAbort) return; // 이미 중단 플래그 켜져있는 경우

            Debug.Log("[MatchController] 매칭 완료 / 모든 플레이어 준비 완료. 게임을 시작합니다.");

            // 시작 작업용 CTS 갱신
            _startCts?.Cancel();
            _startCts?.Dispose();
            _startCts = new CancellationTokenSource();

            // 로딩 창 띄우기


            // 가드: InRoom & Joined 상태에서만 입장 차단 + 상태 전파
            if (PhotonNetwork.InRoom && PhotonNetwork.NetworkClientState == ClientState.Joined)
            {
                room.IsOpen = false;
                room.SetCustomProperties(new Hashtable
                {
                    { Define_LDH.RoomProps.MatchState, Define_LDH.MatchState.Complete.ToString() }
                });
            }

            StartGameAsync(_startCts.Token).Forget();
        }

        /// 매칭 완료 연출 시간만큼 기다렸다가 씬 이동
        private async UniTask StartGameAsync(CancellationToken ct)
        {
            Debug.Log("[MatchController] 마스터 클라이언트에서 게임을 시작합니다.");
            await UniTask.Delay(TimeSpan.FromSeconds(startDelaySec));

            // 취소/중단 플래그면 즉시 종료
            if (ct.IsCancellationRequested || _leavingByAbort) return;

            // 안전 재검증(이탈 대비)
            var room = PhotonNetwork.CurrentRoom;
            if (room != null &&
                PhotonNetwork.InRoom &&
                PhotonNetwork.NetworkClientState == ClientState.Joined &&
                room.PlayerCount == room.MaxPlayers &&
                Equals(room.CustomProperties[Define_LDH.RoomProps.MatchState],
                    Define_LDH.MatchState.Complete.ToString()))
            {
                Manager.Network.LoadGameScene();
            }
            else
            {
                // Joined일 때만 롤백
                if (room != null && PhotonNetwork.InRoom &&
                    PhotonNetwork.NetworkClientState == ClientState.Joined && !_leavingByAbort)
                {
                    room.IsOpen = true;
                    room.SetCustomProperties(new Hashtable
                    {
                        { Define_LDH.RoomProps.MatchState, Define_LDH.MatchState.Matching.ToString() }
                    });
                }

                QuickMatch.starting = false;
                PrivateMatch.starting = false;
            }
        }

        private async void CloseMatchingPanelAndShowLoading(string state)
        {
            if (string.Equals(state, Define_LDH.MatchState.Complete.ToString(), StringComparison.Ordinal))
            {
                if (_uiLoading == null)
                {
                    _uiLoading = Manager.UI.CreatePopupUI<UI_Loading>();
                    if (loadingTheme)
                    {
                        _uiLoading.ApplyTheme(loadingTheme);
                    }

                    _uiLoading.SetProgress(0f);
                }
                
                await UniTask.Delay(TimeSpan.FromSeconds(startDelaySec));
                if (CurrentMatchType == MatchType.Quick)
                    await QuickMatch.CloseRoomPanel();
                else if (CurrentMatchType == MatchType.Private)
                    await PrivateMatch.CloseRoomPanel();
                
                await Manager.UI.ShowPopupUI(_uiLoading);
            }
        }

        #endregion

        #region Abort

        public async void OnAnyPlayerLeft(Player other)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room == null || !PhotonNetwork.InRoom || _leavingByAbort) return;

            var stateObj = room.CustomProperties != null
                ? room.CustomProperties[Define_LDH.RoomProps.MatchState]
                : null;
            var stateStr = stateObj as string;
            // 매칭 완료 상태가 되어 게임 시작이 되어버린 상태(Complete)에서 한 명이라도 이탈하면 → 모두 즉시 방 나가기
            if (string.Equals(stateStr, Define_LDH.MatchState.Complete.ToString(), StringComparison.Ordinal))
            {
                _leavingByAbort = true;

                // 시작 작업들 즉시 중지
                _startCts?.Cancel();

                await Manager.UI.CloseAllPopupUI();

                var quitGameUI = Manager.UI.CreatePopupUI<UI_Popup_QuitGame>();
                Manager.UI.ShowPopupUI(quitGameUI);
                // Manager.UI.EnqueueToast(ToastType.Error, "플레이어 이탈로 게임 시작을 취소합니다.");

                if (CurrentMatchType == MatchType.Quick)
                {
                    Debug.Log("<color=green> 빠른 매칭 취소. 구독 해제 및 정리</color>");
                    QuickMatch.OnClickMatchCancel();
                }

                else if (CurrentMatchType == MatchType.Private)
                {
                    Debug.Log("<color=green> 비공개 매칭 취소. 구독 해제 및 정리</color>");
                    PrivateMatch.OnClickLeaveRoom();
                }
                _leavingByAbort = false;
            }
        }

        #endregion
    }
}