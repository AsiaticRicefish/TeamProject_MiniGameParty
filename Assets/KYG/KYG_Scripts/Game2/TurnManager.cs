using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using LDH.LDH_Scripts;
using PMS_Legacy;

namespace YG
{

    /// <summary>
    /// 카드 선택 결과로 받은 actorOrder를 바탕으로 마스터가 턴을 지휘하고 RPC로 전파.
    /// - Singleton: 씬에 1개
    /// - GameOver: currentActor == -1 브로드캐스트
    /// </summary>
    public class TurnManager : MonoBehaviourPunCallbacks
    {
        public static TurnManager Instance { get; private set; }

        [SerializeField] private bool showLog = true;
        private readonly List<int> order = new();
        private int curIndex = -1;

        public int CurrentActor => (curIndex >= 0 && curIndex < order.Count) ? order[curIndex] : -1;
        public System.Action<int,int> OnTurnChanged;    // (prev, curr)
        public System.Action<int[]> OnOrderInitialized; // ★ 추가: 턴 순서 확정 알림

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        public void InitTurnOrder(int[] actorOrder)
        {
            order.Clear(); order.AddRange(actorOrder);
            curIndex = order.Count > 0 ? 0 : -1;

            // ★ 턴 순서 확정되었음을 먼저 알림 (게임 시작 트리거로 사용)
            OnOrderInitialized?.Invoke(actorOrder);

            if (PhotonNetwork.IsMasterClient && order.Count > 0)
                photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.AllBuffered, -1, order[curIndex]);
        }
        
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient || order.Count == 0) return;
            int prev = CurrentActor;
            curIndex = (curIndex + 1) % order.Count;
            photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.AllBuffered, prev, order[curIndex]);
        }

        public void Eliminate(int actorNumber)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            int idx = order.IndexOf(actorNumber);
            if (idx < 0) return;

            order.RemoveAt(idx);
            if (idx <= curIndex) curIndex = Mathf.Max(0, curIndex - 1);

            if (order.Count == 1)
            {
                // 우승 1명 남음 → currentActor = -1로 종료 신호
                photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.AllBuffered, actorNumber, -1);
                return;
            }

            NextTurn();
        }
        
        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (order.Count == 0) return;

            // 새 마스터만 현재 턴 상태를 즉시 재브로드캐스트 (버퍼 포함)
            if (PhotonNetwork.IsMasterClient)
            {
                int prev = -2; // 디버깅용 태그 값
                photonView.RPC(nameof(RPC_SetCurrentTurn), RpcTarget.AllBuffered, prev, CurrentActor);
                if (showLog) Debug.Log($"[Turn] Master switched → rebroadcast current={CurrentActor}");
            }
        }

        [PunRPC]
        private void RPC_SetCurrentTurn(int prevActor, int currentActor)
        {
            if (showLog) Debug.Log($"[TurnManager] Turn → {prevActor} ▶ {currentActor}");
            OnTurnChanged?.Invoke(prevActor, currentActor);
        }
    }
}