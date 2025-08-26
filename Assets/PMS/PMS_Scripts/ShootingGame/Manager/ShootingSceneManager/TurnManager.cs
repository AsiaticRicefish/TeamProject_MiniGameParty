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
        private int currentTurnIndex = 0; 
        private int currentRound = 1;    
        private int totalRounds = 3;

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

        //마스터 클라이언트만 호출하도록
        public void SetupTurn()     //List<int> sorted;
        {
            Debug.Log("[TurnManager] SetupTurn 호출됨");
            if (!PhotonNetwork.IsMasterClient) return;


            currentTurnIndex = 0;
            currentRound = 1;

            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
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

            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "GamePlayState");
        }

        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 현재 턴 알 비활성화
            EggManager.Instance.photonView.RPC("ClearCurrentEgg", RpcTarget.All);

            currentTurnIndex++;
            if (currentTurnIndex > PhotonNetwork.CurrentRoom.PlayerCount) // PhotonNetwork.CurrentRoom.PlayerCount 추후 변경
            {
                currentTurnIndex = 1; //1이 시작
                currentRound++;
                if (currentRound > totalRounds)
                {
                    Debug.Log("[TurnManager] - 마스터 클라이언트만 보임 / 게임 종료!");
                    // TODO : 게임종료처리
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
            // 마스터 클라이언트만 알 관리
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, this.currentTurnIndex, this.currentRound);
        }

        // 턴 인덱스로 플레이어 UID 찾기 (헬퍼 메서드)
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
                PlayerInputManager.Instance.EnableInput();
                EggManager.Instance.SpawnEgg(myUid);
            }
            else
            {
                Debug.Log("상대방 턴 입니다");
                PlayerInputManager.Instance.DisableInput();
                Debug.Log($"[TurnManager] - {myPlayer.ShootingData.myTurnIndex}");
            }
        }

        public void StartTurnCorutine(float delay)
        {
            if (TurnCorutine != null) return;
            TurnCorutine = StartCoroutine(TurnChangeDelay(delay));
        }

        private IEnumerator TurnChangeDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            NextTurn();
            TurnCorutine = null;
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
    }
}
