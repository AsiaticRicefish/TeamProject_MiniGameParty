using System;
using System.Collections;
using LDH_Util;
using Managers;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Network
{
    public class MatchController : MonoBehaviour
    {
        private static MatchController _instance;
        public static MatchController Instance => _instance;

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
        }

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


            StartCoroutine(StartGameWithDelay());
        }
        
        /// 매칭 완료 연출 시간만큼 기다렸다가 씬 이동
        private IEnumerator StartGameWithDelay()
        {
            Debug.Log("[MatchController] 마스터 클라이언트에서 게임을 시작합니다.");
            yield return new WaitForSeconds(startDelaySec);
            
            
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
    }
}