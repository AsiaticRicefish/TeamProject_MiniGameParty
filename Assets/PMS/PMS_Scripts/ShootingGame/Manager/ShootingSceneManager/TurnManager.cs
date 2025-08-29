using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        //public Transform eggSpawnPoint; 
        //public UnimoEgg currentUnimoEgg;

        //private List<int> turnOrder = new List<int>();
        public int currentTurnIndex = 0; 
        public int currentRoundIndex = 0;    
        private int totalRounds = 1;

        public bool IsTurnEnd;
        private Coroutine TurnCorutine;

        public event Action<UnimoEgg> OnTurnChanged;
        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - TurnManager 초기화 완료");
        }

        //마스터 클라이언트만 호출하도록 - 룸프로퍼티 변경할 수 있게
        //public void SetupTurn()     //List<int> sorted;
        //{
        //    Debug.Log("[TurnManager] SetupTurn 호출됨");
        //    if (!PhotonNetwork.IsMasterClient) return;


        //    currentTurnIndex = 0;
        //    currentRoundIndex = 1;
        //    RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
        //}

        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 현재 턴 알 비활성화
            EggManager.Instance.photonView.RPC("ClearCurrentEgg", RpcTarget.All);

            currentTurnIndex++;
            if (currentTurnIndex > PhotonNetwork.CurrentRoom.PlayerCount) // PhotonNetwork.CurrentRoom.PlayerCount 추후 변경
            {
                currentTurnIndex = 1; //1이 시작
                currentRoundIndex++;
                if (currentRoundIndex > totalRounds)
                {
                    Debug.Log("[TurnManager] - 마스터 클라이언트만 보임 / 게임 종료!");
                    // TODO : 게임종료처리가 아니라 우승자 정하는 게임 상태로 넘어감
                    RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "CheckGameWinnderState");
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
            currentTurnIndex = 1;                 // 0번부터 시작
            BroadcastCurrentTurn();
        }

        //네트워크 콜백 되는 함수
        public void SetCurrentTurn()
        {
            string myUid = PMS_Util.PMS_Util.GetMyUid();
            if (string.IsNullOrEmpty(myUid))
            {
                Debug.LogWarning("[TurnManager] - UID를 가져올 수 없습니다.");
                return;
            }

            GamePlayer myPlayer = PlayerManager.Instance.GetPlayer(myUid);
            if (myPlayer == null)
            {
                Debug.LogWarning("[TurnManager] - Player 객체를 찾을 수 없습니다.");
                return;
            }

            bool isMyTurn = (currentTurnIndex == myPlayer.ShootingData.myTurnIndex);

            Debug.Log($"[TurnManager] 현재 라운드 = {currentRoundIndex}, 현재 턴 = {currentTurnIndex}, 내턴인가? = {isMyTurn}");

            if (isMyTurn)
            {
                Debug.Log("내 턴 입니다!");
                UnimoEgg newEgg = EggManager.Instance.SpawnEgg(myUid);
                newEgg.ShooterUid = PMS_Util.PMS_Util.GetMyUid();

                var localInput = newEgg.GetComponent<LocalPlayerInput>();
                if (localInput != null)
                {
                    localInput.EnableInput();                           // 해당 유니모 Input 활성화 시킴
                    //StartCoroutine(TurnRoutine(localInput));            //입력 코루틴 실행
                }
            }
            else
            {
                Debug.Log("상대방 턴 입니다");
            }

            //StartTurnCorutine(10.0f);
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

        //Local에서 처리가 완료 되었으면 마스터 클라이언트한테 요청
        public void RequestMyTurnEnd()
        {
            // 마스터에게 턴 종료 요청
            photonView.RPC("RequestTurnEnd", RpcTarget.MasterClient);
        }

        // 마스터가 턴을 넘기는 부분
        [PunRPC]
        private void RequestTurnEnd()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            NextTurn();
        }
    }
}
