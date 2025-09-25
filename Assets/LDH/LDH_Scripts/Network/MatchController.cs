using System;
using System.Collections;
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
        
        
        [Header("Match Controller")]
        public QuickMatchController QuickMatch;
        public PrivateMatchController PrivateMatch;
        public float startDelaySec = 0.8f;
        
        
        private void Awake()
        {
            _instance = this;
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
        }

        private void Unsubscribe()
        {
            PhotonNetwork.NetworkingClient.StateChanged -= OnPhotonStateChanged;

        }


        public void SetMatching(MatchType type, bool isMatching)
        {
            IsMatching = isMatching;
            CurrentMatchType = type;
            
            MatchTypeChanged?.Invoke(type, isMatching);
            RefreshButtons();
        }

        #region PlayerCount

        

#if TEST_PLAYER_COUNT
        public void ShowPlayerCount(MatchType matchType)
        {
            if(IsMatching) return;
            
            SetMatching(matchType, true);
            
            var playerCount = Manager.UI.CreatePopupUI<UI_Popup_PlayerCount>();
            Manager.UI.ShowPopupUI(playerCount).Forget();
        }
        
        
        public void StartMatching()
        {
            switch (CurrentMatchType)
            {
                case MatchType.Quick :
                    QuickMatch.OnClickMatchingStart();
                    break;
                case MatchType.Private:
                    PrivateMatch.RequestCreatePrivateRoom();
                    break;
                default:
                    return;
            }
        }

#endif
       
        #endregion
        
        
        #region Matching Button Control

        private void OnPhotonStateChanged(ClientState prev, ClientState curr)
        {
            RefreshButtons();
        }   


        private bool ReadyToMatch() =>
            PhotonNetwork.IsConnected && PhotonNetwork.InLobby && !PhotonNetwork.InRoom && !IsMatching;

        private void RefreshButtons()
        {
            bool ready = ReadyToMatch();
            
            //Debug.Log($"[MatchController] isconnected :{PhotonNetwork.IsConnected} / inlobby : {PhotonNetwork.InLobby} / inroom : {PhotonNetwork.InRoom} / is maching : {IsMatching} ");
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

            Debug.Log("[MatchController] 매칭 완료 / 모든 플레이어 준비 완료. 게임을 시작합니다.");

            // 입장 차단 + 상태 전파
            room.IsOpen = false;
            room.SetCustomProperties(new Hashtable
                { { Define_LDH.RoomProps.MatchState, Define_LDH.MatchState.Complete.ToString() } });


            StartGameAsync().Forget();
        }
        
        /// 매칭 완료 연출 시간만큼 기다렸다가 씬 이동
        private async UniTask StartGameAsync()
        {
            Debug.Log("[MatchController] 마스터 클라이언트에서 게임을 시작합니다.");
            await UniTask.Delay(TimeSpan.FromSeconds(startDelaySec));
            
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

                QuickMatch.starting = false;
                PrivateMatch.starting = false;
                
            }
            
        }
        #endregion
    }
}