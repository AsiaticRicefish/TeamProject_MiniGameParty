using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

//다른 곳에서도 턴매니저가 있을 수 있으니깐 namespace처리 
namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        public Transform eggSpawnPoint; // 알 생성 위치
        public UnimoEgg currentUnimoEgg;

        //private List<int> turnOrder = new List<int>();
        private int currentTurnIndex = 0; // 아직 턴 시작 전
        private int currentRound = 1;     // 현재 라운드
        private int totalRounds = 3;      // 반복할 총 라운드 수

        public event Action<UnimoEgg> OnTurnChanged;        //턴변경 이벤트 호출

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - 슈팅 게임 TurnManager 초기화");
        }

        //외부에서 사용 - 추후 Card에서 저장된 List에 따른 턴 셋팅
        public void SetupTurn()     //List<int> sorted;
        {
            //추후 CardManager에서 받아온 Turn 정보를 토대로 playerList 설계하기
            if (!PhotonNetwork.IsMasterClient) return;

            /*turnOrder.Clear();
            turnOrder.AddRange(sortedList);  // 카드 뽑기 결과 반영

            if (turnOrder.Count == 0) return;*/

            currentTurnIndex = 1;
            currentRound = 1;
            BroadcastCurrentTurn();
        }

        //테스트 코드 - 임시로 모든 플레이어를 뒤져서 TurnIndex를 순차적으로 부여
        public void TestSetupTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int index = 1;

            foreach (var player in ShootingGameManager.Instance.players)
            {
                player.Value.myTurnIndex = index;
                index++;
            }

            // 첫 턴은 1번 플레이어
            currentTurnIndex = 1;
            currentRound = 1;
            BroadcastCurrentTurn();
        }

        // TurnManager (마스터만 관리)
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            currentTurnIndex++;

            // 현재 라운드 끝났는지 체크
            if (currentTurnIndex > PhotonNetwork.CurrentRoom.PlayerCount) // 턴 인덱스가 1부터 시작, 추후 유저가 나가면 문제가 생기는데 리스트를 만들어야하나?
            {
                currentTurnIndex = 1; // 다시 1번부터 시작
                currentRound++;

                if (currentRound > totalRounds)
                {
                    Debug.Log("[TurnManager] - 모든 라운드 종료!");
                    // TODO - 게임 종료 처리 또는 다음 상태로 전환
                    return;
                }
            }

            BroadcastCurrentTurn();
        }

        //현재 턴 정보 전체 클라이언트에 전달
        private void BroadcastCurrentTurn()
        {
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, this.currentTurnIndex, this.currentRound);
        }

        [PunRPC]
        private void RPC_SetCurrentTurn(int turnIndex,int roundIndex )
        {
            string myUid = PMS_Util.PMS_Util.GetMyUid();
            if (string.IsNullOrEmpty(myUid))
            {
                Debug.LogWarning("[TurnManager] - UID를 찾지 못했습니다.");
                return;
            }

            GamePlayer myPlayer = PlayerManager.Instance.GetPlayer(myUid);
            if (myPlayer == null)
            {
                Debug.LogWarning("[TurnManager] - Player 객체를 찾지 못했습니다.");
                return;
            }

            bool isMyTurn = (turnIndex == myPlayer.ShootingData.myTurnIndex);

            Debug.Log($"[TurnManager] 라운드 {roundIndex}, CurrentTurn={turnIndex}, 내 턴={isMyTurn}");

            if (isMyTurn)
            {
                Debug.Log("내 턴 시작!");
                SpawnEggForMe(myUid);
                InputManager.Instance.EnableInput();
            }
            else
            {
                Debug.Log("다른 사람 턴입니다");
                InputManager.Instance.DisableInput();
            }

            OnTurnChanged?.Invoke(currentUnimoEgg);
        }

        private void SpawnEggForMe(string uid)
        {
            // Photon으로 알 생성
            GameObject eggObj = PhotonNetwork.Instantiate("UnimoEggPrefab", eggSpawnPoint.position, Quaternion.identity);
            currentUnimoEgg = eggObj.GetComponent<UnimoEgg>();
            currentUnimoEgg.ShooterUid = uid;
        }
    }
}
