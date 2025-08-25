using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

//다른 곳에서도 턴매니저가 있을 수 있으니깐 namespace처리 
namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    //원형 연결 리스트 사용  끝 -> 시작의 이동
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        //private List<int> turnOrder = new List<int>();
        private int currentTurnIndex = 0; // 아직 턴 시작 전

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - 슈팅 게임 TurnManager 초기화");
        }

        //외부에서 사용 - 추후 Card에서 저장된 List에 따른 턴 셋팅
        /*public void SetupTurn(List<int> sortedList)
        {
            //추후 CardManager에서 받아온 Turn 정보를 토대로 playerList 설계하기
            if (!PhotonNetwork.IsMasterClient) return;

            turnOrder.Clear();
            turnOrder.AddRange(sortedList);  // 카드 뽑기 결과 반영

            if (turnOrder.Count == 0) return;

            currentTurnIndex = 1;
            //BroadcastCurrentTurn(turnOrder[currentTurnIndex]);
        }*/

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

            // 첫 턴은 0번 플레이어
            currentTurnIndex = 0;
            //BroadcastCurrentTurn(turnOrder[currentTurnIndex]);
        }

        // TurnManager (마스터만 관리)
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            currentTurnIndex = (currentTurnIndex + 1) % PhotonNetwork.CurrentRoom.PlayerCount;                  // TODO - Magic Number 4 -> 현재 방의 게임 플레이를 하고 있는 플레이어의 수
            BroadcastCurrentTurn(currentTurnIndex);
        }

        //현재 턴 정보 전체 클라이언트에 전달
        private void BroadcastCurrentTurn(int currentTurnIndex)
        {
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, currentTurnIndex);
        }

        [PunRPC]
        private void RPC_SetCurrentTurn(int turnIndex)
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

            Debug.Log($"[TurnManager] - CurrentTurn={turnIndex}, MyTurnIndex={myPlayer.ShootingData.myTurnIndex}, IsMyTurn={isMyTurn}");

            if (isMyTurn)
            {
                Debug.Log("내 턴 시작!");
                InputManager.Instance.EnableInput();
            }
            else
            {
                Debug.Log("다른 사람 턴입니다");
                InputManager.Instance.DisableInput();
            }
        }
    }
}
