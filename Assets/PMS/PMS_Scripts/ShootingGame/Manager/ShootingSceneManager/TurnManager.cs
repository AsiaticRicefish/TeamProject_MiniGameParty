using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

//�ٸ� �������� �ϸŴ����� ���� �� �����ϱ� namespaceó�� 
namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        public Transform eggSpawnPoint; // �� ���� ��ġ
        public UnimoEgg currentUnimoEgg;

        //private List<int> turnOrder = new List<int>();
        private int currentTurnIndex = 0; // ���� �� ���� ��
        private int currentRound = 1;     // ���� ����
        private int totalRounds = 3;      // �ݺ��� �� ���� ��

        public event Action<UnimoEgg> OnTurnChanged;        //�Ϻ��� �̺�Ʈ ȣ��

        protected override void OnAwake()
        {
            isPersistent = false;
        }

        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - ���� ���� TurnManager �ʱ�ȭ");
        }

        //�ܺο��� ��� - ���� Card���� ����� List�� ���� �� ����
        public void SetupTurn()     //List<int> sorted;
        {
            //���� CardManager���� �޾ƿ� Turn ������ ���� playerList �����ϱ�
            if (!PhotonNetwork.IsMasterClient) return;

            /*turnOrder.Clear();
            turnOrder.AddRange(sortedList);  // ī�� �̱� ��� �ݿ�

            if (turnOrder.Count == 0) return;*/

            currentTurnIndex = 1;
            currentRound = 1;
            BroadcastCurrentTurn();
        }

        //�׽�Ʈ �ڵ� - �ӽ÷� ��� �÷��̾ ������ TurnIndex�� ���������� �ο�
        public void TestSetupTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int idx = 0; // 0-based
            foreach (var kv in ShootingGameManager.Instance.players)
            {
                kv.Value.myTurnIndex = idx++;
            }

            // ù ���� 1�� �÷��̾�
            currentTurnIndex = 1;
            currentRound = 1;
            BroadcastCurrentTurn();
        }

        // TurnManager (�����͸� ����)
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            currentTurnIndex++;

            // ���� ���� �������� üũ
            if (currentTurnIndex > PhotonNetwork.CurrentRoom.PlayerCount) // �� �ε����� 1���� ����, ���� ������ ������ ������ ����µ� ����Ʈ�� �������ϳ�?
            {
                currentTurnIndex = 1; // �ٽ� 1������ ����
                currentRound++;

                if (currentRound > totalRounds)
                {
                    Debug.Log("[TurnManager] - ��� ���� ����!");
                    // TODO - ���� ���� ó�� �Ǵ� ���� ���·� ��ȯ
                    return;
                }
            }

            BroadcastCurrentTurn();
        }

        //���� �� ���� ��ü Ŭ���̾�Ʈ�� ����
        private void BroadcastCurrentTurn()
        {
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.All, this.currentTurnIndex, this.currentRound);
        }
        
        public void StartFirstTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            currentTurnIndex = 0;                 // 0번부터 시작
            BroadcastCurrentTurn(currentTurnIndex);
        }

        [PunRPC]
        private void RPC_SetCurrentTurn(int turnIndex,int roundIndex )
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

            Debug.Log($"[TurnManager] ���� {roundIndex}, CurrentTurn={turnIndex}, �� ��={isMyTurn}");

            if (isMyTurn)
            {
                Debug.Log("�� �� ����!");
                SpawnEggForMe(myUid);
                InputManager.Instance.EnableInput();
            }
            else
            {
                Debug.Log("�ٸ� ��� ���Դϴ�");
                InputManager.Instance.DisableInput();
            }

            OnTurnChanged?.Invoke(currentUnimoEgg);
        }

        private void SpawnEggForMe(string uid)
        {
            // Photon���� �� ����
            GameObject eggObj = PhotonNetwork.Instantiate("UnimoEggPrefab", eggSpawnPoint.position, Quaternion.identity);
            currentUnimoEgg = eggObj.GetComponent<UnimoEgg>();
            currentUnimoEgg.ShooterUid = uid;
        }
    }
}
