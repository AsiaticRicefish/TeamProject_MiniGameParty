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
        public Transform eggSpawnPoint; 
        public UnimoEgg currentUnimoEgg;

        //private List<int> turnOrder = new List<int>();
        private int currentTurnIndex = 0; 
        private int currentRound = 1;    
        private int totalRounds = 3;     

        public event Action<UnimoEgg> OnTurnChanged;        

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - TurnManager 초기화 완료");
        }

        public void SetupTurn()     //List<int> sorted;
        {
            Debug.Log("[TurnManager] SetupTurn 호출됨");
            if (!PhotonNetwork.IsMasterClient) return;

            /*turnOrder.Clear();
            turnOrder.AddRange(sortedList);  

            if (turnOrder.Count == 0) return;*/

            currentTurnIndex = 1;
            currentRound = 1;

            if (PhotonNetwork.IsMasterClient)
            {
                RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
            }

            //BroadcastCurrentTurn();
        }

        //테스트용 코드
        public void TestSetupTurn()
        {
            Debug.Log("[TurnManager] TestSetupTurn 호출됨");

            int idx = 1; // 1-based
            foreach (var kv in ShootingGameManager.Instance.players)
            {
                kv.Value.myTurnIndex = idx++;
            }

            if (!PhotonNetwork.IsMasterClient) return;

            currentTurnIndex = 1;
            currentRound = 1;


            if (PhotonNetwork.IsMasterClient)
            {
                RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
            }
            //BroadcastCurrentTurn();
        }

        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            currentTurnIndex++;

            if (currentTurnIndex > PhotonNetwork.CurrentRoom.PlayerCount) // PhotonNetwork.CurrentRoom.PlayerCount 추후 변경
            {
                currentTurnIndex = 1; //1이 시작
                currentRound++;

                if (currentRound > totalRounds)
                {
                    Debug.Log("[TurnManager] - 게임 종료!");
                    // TODO - 게임 종료 관련 추가
                    return;
                }
            }

            BroadcastCurrentTurn();
        }

        public void BroadcastCurrentTurn()
        {
            /*
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.Turn, this.currentTurnIndex);
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.Round, this.currentRound);
            */
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, this.currentTurnIndex, this.currentRound);
        }
        
        public void StartFirstTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            currentTurnIndex = 0;                 // 0번부터 시작
            BroadcastCurrentTurn();
        }

        [PunRPC]
        private void RPC_SetCurrentTurn(int turnIndex,int roundIndex)
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

            bool isMyTurn = (turnIndex == myPlayer.ShootingData.myTurnIndex);

            Debug.Log($"[TurnManager] 현재 라운드 = {roundIndex}, 현재 턴 = {turnIndex}, 내턴인가? ={isMyTurn}");

            if (isMyTurn)
            {
                Debug.Log("내 턴 입니다!");
                SpawnEggForMe(myUid);
                PlayerInputManager.Instance.EnableInput();
            }
            else
            {
                Debug.Log("상대방 턴 입니다");
                PlayerInputManager.Instance.DisableInput();
                Debug.Log($"[TurnManager] - {myPlayer.ShootingData.myTurnIndex}");
            }

            OnTurnChanged?.Invoke(currentUnimoEgg);
        }

        private void SpawnEggForMe(string uid)
        {
            if (currentUnimoEgg != null)
            {
                Debug.Log("[TurnManager] 이미 알이 존재합니다. 스폰하지 않음.");
                return;
            }

            GameObject eggObj = PhotonNetwork.Instantiate("UnimoEggPrefab", eggSpawnPoint.position, Quaternion.identity);
            currentUnimoEgg = eggObj.GetComponent<UnimoEgg>();
            currentUnimoEgg.ShooterUid = uid;
        }
    }
}
