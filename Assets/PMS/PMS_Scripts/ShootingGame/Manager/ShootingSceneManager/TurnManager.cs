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
        private int _round = 0;

        public int currentRoundIndex
        {
            get => _round;
            set
            {
                Debug.Log($"라운드 값 변경 - 기존 값 : {_round}  / 변경될 값 : {value}");
                _round = value;
            }
        }
        private int totalRounds = 3;

        public bool IsTurnEnd;
        private bool skipRoundIncrease = false;
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

        public bool IsMyTurn() => GetCurrentTurnPlayer().PlayerId == PhotonNetwork.LocalPlayer.UserId;

        #endregion

        #region Turn 넘기기 / 결과 알리기 
        private IEnumerator NextTurn(float delay = 1.0f)
        {
            Debug.Log($"[TurnManager] NextTurn 호출. {delay} 동안 잠시 대기합니다.");
            yield return new WaitForSeconds(delay);

            if (!PhotonNetwork.IsMasterClient)
            {
                TurnCorutine = null;
                yield break;
            }

            // 현재 턴 알 비활성화
            EggManager.Instance.photonView.RPC("ClearCurrentEgg", RpcTarget.All);
            
            
            //----- 다음 턴 계산 시작 -----//
            var nextNode = _turnOrder.NextNode;
            Debug.Log($"next turn - next node는? {nextNode?.Value.ShootingData.myTurnIndex}");
            
            if (nextNode == null)
            {
                Debug.LogWarning("[TunManager] 다음 턴 대상이 없습니다.");
                TurnCorutine = null;
                yield break;
            }
            
            currentTurnIndex = nextNode.Value.ShootingData.myTurnIndex;
            
            // 한 라운드 완료를 체크하는 조건
            if (_turnOrder.IsFirstNode(nextNode) && !skipRoundIncrease)
            {
                Debug.Log("라운드를 증가시킵니다.");
                currentRoundIndex++; //1부터 시작
                
                if (currentRoundIndex > totalRounds)
                {
                    Debug.Log("[TurnManager] - 마스터 클라이언트만 보임 / 게임 종료!");
                    // TODO : 게임종료처리가 아니라 우승자 정하는 게임 상태로 넘어감
                    RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State,
                        "CheckGameWinnderState");

                    TurnCorutine = null;
                    yield break;
                }
            }

            skipRoundIncrease = false; //초기화

            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
            TurnCorutine = null;
        }
        
        /// <summary>
        /// 턴 넘기기 API
        /// </summary>
        public void TurnCheck()
        {
            if (TurnCorutine != null)
            {
                Debug.LogWarning("Turn Coroutine != null");
                return;
            }

            TurnCorutine = StartCoroutine(NextTurn());
        } 

        
        public void BroadcastCurrentTurn()
        {
            var props = new Dictionary<string, object>
            {
                { ShootingGamePropertyKeys.Turn, this.currentTurnIndex },
                { ShootingGamePropertyKeys.Round, this.currentRoundIndex }          //콜백 - 무조건 -> 유저들 턴을 넘긴것을 알 수 있다. 
            };

            RoomPropertyObserver.Instance.SetRoomProperties(props);
  
            //보장이 될 수 있나?
            //photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, this.currentTurnIndex, this.currentRoundIndex);
        }


        private IEnumerator SafeRemoveNode(string uid)
        {
            yield return new WaitUntil(() => TurnCorutine == null);
            _turnOrder.RemovePlayer(uid);
        }
   

        #endregion
        
        
        #region RPC / 네트워크 콜백 관련(현재 턴 진행)
        
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

            Debug.Log($"[TurnManager] 현재 라운드 = {currentRoundIndex}, 현재 턴 = {currentTurnIndex}, 내턴인가? = {isMyTurn}");

            //현재 턴이 설정되었다는 이벤트 알림
            OnSetCurrentTurn?.Invoke(isMyTurn, currentTurnIndex);
            
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
                RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "TurnCheckState");
            }
            else
            {
                Debug.LogWarning($"[턴 종료 거절] {info.Sender.NickName}은 현재 턴이 아님");
                Debug.Log(
                    $"턴 불일치{targetIndex},{RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.Turn)}");
            }
        }



        #endregion

       
      
        #region Legacy
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
        

        #endregion
    

       

        #region PunCallback

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            //현재 게임 상태를 가져온다(룸 프로퍼티)
            var stateValue = RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.State);
            Debug.Log($"플레이어 나감 콜백 - 플레이어가 나갔을 때 게임 STATE : {stateValue}");

            //현재 게임 상태가 game play state가 아니라면 처리할 필요가 없음
            if (stateValue != null && stateValue is string && (stateValue.ToString().Equals("GamePlayState") || stateValue.ToString().Equals("TurnCheckState")))
            {
                if (otherPlayer.CustomProperties.TryGetValue("uid", out object value) 
                    && value is string uid && !string.IsNullOrEmpty(uid))
                {
                    
                    // 조건 체크를 위한 캐싱
                    var leftPlayerTurnIndex = _turnOrder.GetPlayerTurnIndex(uid);
                    skipRoundIncrease = _turnOrder.IsFirstNode(uid);
                    
                    Debug.Log($"나간 플레이어의 myturnindex : {leftPlayerTurnIndex} / 현재 턴 인덱스 {currentTurnIndex} / 나간 플레이어가 첫번째 순서였는가 : {skipRoundIncrease}");
                    
                    // 턴에서 제거
                    StartCoroutine(SafeRemoveNode(uid));
                    
                    //현재 턴인 플레이어가 나갔고, 턴 종료 요청을 하지 못해서 game play state에 멈춰있는 경우 -> 강제로 턴을 넘깁니다.
                    if (PhotonNetwork.IsMasterClient 
                        && stateValue.Equals("GamePlayState") && leftPlayerTurnIndex == currentTurnIndex)
                    {
                        Debug.Log("현재 턴 플레이어가 나감 && 현재 상태가 게임 플레이 상태이기 때문에 강제로 턴을 넘깁니다.");
                        StartCoroutine(NextTurn());
                       

                    }
                    else
                    {
                        Debug.Log("나간 플레이어가 현재 턴이 아니므로 그냥 둡니다.");

                    }
                }
                   
                else
                {
                    Debug.Log("[TurnManager] 플레이어의 uid 프로퍼티를 찾을 수 없습니다.");
                }
            }
        }


        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (!newMasterClient.IsLocal) return;
            
            // 새로운 마스터는 현재 게임의 state가 game play state에 있는 경우
            // 이전 마스터가 턴 종료 요청을 승인하고 다음 턴을 계산해서 GamePlayState로 넘겨줘야하는데 이걸 완료하지 못하고 나간 것 -> 턴이 멈추게 된다.
            // 따라서 새로운 마스터는 턴 계산을 다시해서 반영해줘야 한다.
            var currentStateValue = RoomPropertyObserver.Instance.GetRoomProperty(ShootingGamePropertyKeys.State);
            Debug.Log($"마스터 변경 콜백 - 플레이어가 나갔을 때 게임 STATE : {currentStateValue}");
            if (currentStateValue is string && currentStateValue.Equals("TurnCheckState"))
            {
                Debug.Log($"마스터 변경 콜백 - 턴 체크 상태이고,새로운 마스터가 NEXT TURN을 다시 실행시킴");
                StartCoroutine(NextTurn());
            }
            else
            {
                currentStateValue.Equals("TurnCheckState");
            }
        }
        
        #endregion
        
  
    }
}
