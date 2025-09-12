using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

namespace KYG
{
    [RequireComponent(typeof(PhotonView))]
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        //public Transform eggSpawnPoint; 
        //public UnimoEgg currentUnimoEgg;

        //private List<int> turnOrder = new List<int>();
        
        [SerializeField] private bool nextTurnDrivenByMiniGame = true; // 미니게임에서 턴을 넘길지 여부
        private int currentTurnIndex = 0; 
        private int currentRound = 1;    
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
                    // TODO : 게임종료처리가 아니라 우승자 정하는 게임 상태로 넘어감
                    RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "CheckGameWinnderState");
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

        [PunRPC]
        private void RPC_SetCurrentTurn(int turnIndex, int roundIndex)
        {
            // 내 CustomProperties에서 turnIndex 읽기
            int myTurnIdx = -1;
            if (PhotonNetwork.LocalPlayer.CustomProperties != null &&
                PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("turnIndex", out var v))
            {
                myTurnIdx = v is int i ? i : -1;
            }

            bool isMyTurn = (turnIndex == myTurnIdx);
            Debug.Log($"[TurnManager] 현재 라운드={roundIndex}, 턴={turnIndex}, 내턴?={isMyTurn}");

            if (isMyTurn)
            {
                var egg = EggManager.Instance.SpawnEgg(PMS_Util.PMS_Util.GetMyUid());
                egg.ShooterUid = PMS_Util.PMS_Util.GetMyUid();

                var localInput = egg.GetComponent<LocalPlayerInput>();
                if (localInput != null) localInput.EnableInput();
            }
            else
            {
                Debug.Log("상대방 턴 입니다");
            }

            // 타이머/미니게임 호출 부분은 그대로 유지
            if (PhotonNetwork.IsMasterClient && !nextTurnDrivenByMiniGame)
                StartTurnCorutine(10.0f);

            var mini = FindObjectOfType<MeteorTapMiniGame>();
            if (mini != null)
            {
                int aliveCount = PhotonNetwork.CurrentRoom.PlayerCount;
                mini.InitTurn(isMyTurn, roundIndex, aliveCount);
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
    }
}
