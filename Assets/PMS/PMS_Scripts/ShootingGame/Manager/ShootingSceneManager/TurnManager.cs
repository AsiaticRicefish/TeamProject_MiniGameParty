using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using DesignPattern;
using LDH.LDH_Scripts.ShootingGame;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using ShootingScene.ShootingGame;

namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        //public Transform eggSpawnPoint; 
        //public UnimoEgg currentUnimoEgg;

        private TurnOrder _turnOrder = new();
        
        public int currentTurnIndex = 0;
        public int currentRoundIndex = 0;
        private int totalRounds = 3;

        public bool IsTurnEnd;
        private Coroutine TurnCorutine;

        public event Action<UnimoEgg> OnTurnChanged;
        public event Action<bool, int> OnSetCurrentTurn;

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - TurnManager 초기화 완료");
        }

        #region Turn Linked List 관련 로직 - API

        public void InitTurnOrder(int[] actorOrder)
        {
            _turnOrder.InitFromActorOrder(actorOrder);
        }

        public void MoveToNextTurn()
        {
            _turnOrder.MoveToNext();
        }

        public GamePlayer GetCurrentTurnPlayer() => _turnOrder.Current;

        #endregion
  
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 현재 턴 알 비활성화
            EggManager.Instance.photonView.RPC("ClearCurrentEgg", RpcTarget.All);

            var nextNode = _turnOrder.NextNode;
            if (nextNode == null)
            {
                Debug.LogWarning("[TunManager] 다음 턴 대상이 없습니다.");
                return;
            }
            currentTurnIndex = nextNode.Value.ShootingData.myTurnIndex;
            
            if (currentTurnIndex > PhotonNetwork.CurrentRoom.PlayerCount) // PhotonNetwork.CurrentRoom.PlayerCount 추후 변경
            {
                currentTurnIndex = 1; //1이 시작
                currentRoundIndex++;
                if (currentRoundIndex > totalRounds)
                {
                    Debug.Log("[TurnManager] - 마스터 클라이언트만 보임 / 게임 종료!");
                    // TODO : 게임종료처리가 아니라 우승자 정하는 게임 상태로 넘어감
                    RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State,
                        "CheckGameWinnderState");
                    return;
                }
            }

            BroadcastCurrentTurn();
        }

        public void BroadcastCurrentTurn()
        {
            var props = new Dictionary<string, object>
            {
                { ShootingGamePropertyKeys.Turn, this.currentTurnIndex },
                { ShootingGamePropertyKeys.Round, this.currentRoundIndex }
            };

            RoomPropertyObserver.Instance.SetRoomProperties(props);

            //보장이 될 수 있나?
            //photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, this.currentTurnIndex, this.currentRoundIndex);
        }

        // 턴 인덱스로 플레이어 UID 찾기
        private string GetPlayerUidByTurnIndex(int turnIndex)
        {
            foreach (var kv in ShootingGameManager.Instance.players)
            {
                if (kv.Value.myTurnIndex == turnIndex)
                {
                    return kv.Key;
                }
            }

            return null;
        }

        public void StartFirstTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            currentTurnIndex = 1; // 0번부터 시작
            BroadcastCurrentTurn();
        }

        //네트워크 콜백 되는 함수
        public IEnumerator SetCurrentTurn()
        {
            string myUid = PMS_Util.PMS_Util.GetMyUid();
            if (string.IsNullOrEmpty(myUid))
            {
                Debug.LogWarning("[TurnManager] - UID를 가져올 수 없습니다.");
                yield break;
            }

            GamePlayer myPlayer = PlayerManager.Instance.GetPlayer(myUid);
            if (myPlayer == null)
            {
                Debug.LogWarning("[TurnManager] - Player 객체를 찾을 수 없습니다.");
                yield break;
            }

            bool isMyTurn = (currentTurnIndex == myPlayer.ShootingData.myTurnIndex);

            //현재 턴이 설정되었다는 이벤트 알림
            OnSetCurrentTurn?.Invoke(isMyTurn, currentTurnIndex);

            Debug.Log($"[TurnManager] 현재 라운드 = {currentRoundIndex}, 현재 턴 = {currentTurnIndex}, 내턴인가? = {isMyTurn}");

            if (isMyTurn)
            {
                Debug.Log("내 턴 입니다!");
                UnimoEgg newEgg = EggManager.Instance.SpawnEgg(myUid);
                newEgg.ShooterUid = PMS_Util.PMS_Util.GetMyUid();

                var localInput = newEgg.GetComponent<LocalPlayerInput>();
                if (localInput != null)
                {
                    yield return ShootingUIManager.Instance.PlayMyTurnUI();

                    localInput.EnableInput(); // 해당 유니모 Input 활성화 시킴
                    //StartCoroutine(TurnRoutine(localInput));            //입력 코루틴 실행
                }
            }
            else
            {
                Debug.Log("상대방 턴 입니다");
            }

            //StartTurnCorutine(10.0f);
            ShootingNetworkManager.Instance.SetTurnCoroutine = null;
        }

        public void StartTurnCorutine(float delay)
        {
            if (TurnCorutine != null) return;
            TurnCorutine = StartCoroutine(TurnChangeDelay(delay));
        }

        private IEnumerator TurnChangeDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            TurnCorutine = null;
            NextTurn();
        }

        public void EndTurn()
        {
        }

        /*private void SpawnEgg(string uid)
        {
            if (currentUnimoEgg != null)
            {
                Debug.Log("[TurnManager] 이미 알이 존재합니다. 스폰하지 않음.");
                return;
            }

            GameObject eggObj = PhotonNetwork.Instantiate("UnimoEggPrefab", eggSpawnPoint.position, Quaternion.identity);
            UnimoEgg newEgg = eggObj.GetComponent<UnimoEgg>();
            newEgg.ShooterUid = uid;

            // 2. 내 턴 알 참조 업데이트
            currentUnimoEgg = newEgg;

            // 3. 모든 클라이언트에 ViewID 전달
            photonView.RPC(nameof(RPC_SetCurrentEggView), RpcTarget.Others, newEgg.photonView.ViewID);
        }

        [PunRPC]
        public void NullToCurrentUnimo()
        {
            if (currentUnimoEgg != null)
            {
                currentUnimoEgg = null;
            }
        }

        [PunRPC]
        public void RPC_SetCurrentEggView(int viewID)
        {
            if (currentUnimoEgg == null)
            {
                // 다른 클라이언트는 ViewID로 currentUnimoEgg 연결
                currentUnimoEgg = PhotonView.Find(viewID).GetComponent<UnimoEgg>();
            }
        }
        */

        // 마스터가 턴을 넘기는 부분
        [PunRPC]
        private void RequestTurnEnd(PhotonMessageInfo info)
        {
            // 요청 보낸 사람 디버깅
            Debug.Log($"[TurnManager] - 턴 종료 요청 보낸 사람: {info.Sender.NickName}");

            //if (!PhotonNetwork.IsMasterClient) return;

            // 요청 보낸 사람의 플레이어 프로퍼티 값 가져오기
            int targetIndex = (int)info.Sender.CustomProperties[ShootingGamePlayerPropertyKeys.MyTurnIndex];

            // 실제 턴 주인인지 확인
            if (TurnManager.Instance.currentTurnIndex == targetIndex)
            {
                Debug.Log($"[턴 종료 승인] {info.Sender.NickName}의 턴 종료 요청");
                StartCoroutine(WaitForTurnDelay());
                Debug.Log($"{targetIndex}");
            }
            else
            {
                Debug.LogWarning($"[턴 종료 거절] {info.Sender.NickName}은 현재 턴이 아님");
                Debug.Log(
                    $"턴 불일치{targetIndex},{RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.Turn)}");
            }
        }

        private IEnumerator WaitForTurnDelay(float delay = 2.0f)
        {
            yield return new WaitForSeconds(delay);
            NextTurn();
        }

        #region PunCallback

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (otherPlayer.CustomProperties.TryGetValue("uid", out object value) && value is string uid &&
                string.IsNullOrEmpty(uid))
                _turnOrder.RemovePlayer(uid);
            else
            {
                Debug.Log("[TurnManager] 플레이어의 uid 프로퍼티를 찾을 수 없습니다.");
            }
        }

        #endregion
        
  
    }
}