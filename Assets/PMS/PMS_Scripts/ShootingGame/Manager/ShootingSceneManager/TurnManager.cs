using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

//�ٸ� �������� �ϸŴ����� ���� �� �����ϱ� namespaceó�� 
namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    //���� ���� ����Ʈ ���  �� -> ������ �̵�
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        //private List<int> turnOrder = new List<int>();
        private int currentTurnIndex = 0; // ���� �� ���� ��

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - ���� ���� TurnManager �ʱ�ȭ");
        }

        //�ܺο��� ��� - ���� Card���� ����� List�� ���� �� ����
        /*public void SetupTurn(List<int> sortedList)
        {
            //���� CardManager���� �޾ƿ� Turn ������ ���� playerList �����ϱ�
            if (!PhotonNetwork.IsMasterClient) return;

            turnOrder.Clear();
            turnOrder.AddRange(sortedList);  // ī�� �̱� ��� �ݿ�

            if (turnOrder.Count == 0) return;

            currentTurnIndex = 1;
            //BroadcastCurrentTurn(turnOrder[currentTurnIndex]);
        }*/

        //�׽�Ʈ �ڵ� - �ӽ÷� ��� �÷��̾ ������ TurnIndex�� ���������� �ο�
        public void TestSetupTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int idx = 0; // 0-based
            foreach (var kv in ShootingGameManager.Instance.players)
            {
                kv.Value.myTurnIndex = idx++;
            }
            currentTurnIndex = 0;
            BroadcastCurrentTurn(currentTurnIndex);
        }

        // TurnManager (�����͸� ����)
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            currentTurnIndex = (currentTurnIndex + 1) % PhotonNetwork.CurrentRoom.PlayerCount;                  // TODO - Magic Number 4 -> ���� ���� ���� �÷��̸� �ϰ� �ִ� �÷��̾��� ��
            BroadcastCurrentTurn(currentTurnIndex);
        }

        //���� �� ���� ��ü Ŭ���̾�Ʈ�� ����
        private void BroadcastCurrentTurn(int currentTurnIndex)
        {
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, currentTurnIndex);
        }
        
        public void StartFirstTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            currentTurnIndex = 0;                 // 0번부터 시작
            BroadcastCurrentTurn(currentTurnIndex);
        }

        [PunRPC]
        private void RPC_SetCurrentTurn(int turnIndex)
        {
            string myUid = PMS_Util.PMS_Util.GetMyUid();
            if (string.IsNullOrEmpty(myUid))
            {
                Debug.LogWarning("[TurnManager] - UID�� ã�� ���߽��ϴ�.");
                return;
            }

            GamePlayer myPlayer = PlayerManager.Instance.GetPlayer(myUid);
            if (myPlayer == null)
            {
                Debug.LogWarning("[TurnManager] - Player ��ü�� ã�� ���߽��ϴ�.");
                return;
            }

            bool isMyTurn = (turnIndex == myPlayer.ShootingData.myTurnIndex);

            Debug.Log($"[TurnManager] - CurrentTurn={turnIndex}, MyTurnIndex={myPlayer.ShootingData.myTurnIndex}, IsMyTurn={isMyTurn}");

            if (isMyTurn)
            {
                Debug.Log("�� �� ����!");
                InputManager.Instance.EnableInput();
            }
            else
            {
                Debug.Log("�ٸ� ��� ���Դϴ�");
                InputManager.Instance.DisableInput();
            }
        }
    }
}
