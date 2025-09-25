using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using KYG.Framework;   // IGameComponent
using DesignPattern;  // PunSingleton

namespace KYG
{
    /// <summary>
    /// 턴 진행의 단일 권위(마스터).
    /// - 현재 턴/라운드 브로드캐스트 (+ 엔딩카운트 동기화)
    /// - 3탭 패스 / 최종카운트 도달 시 탈락 처리
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class TurnManager : PunSingleton<TurnManager>, IGameComponent
    {
        // ======= 게임 진행 파라미터 =======
        [SerializeField] private int sharedEndingCount = 18; // 라운드 엔딩 카운트(브로드캐스트됨)
        [SerializeField] private int currentRoundIndex = 1;

        // ======= 내부 상태 =======
        private readonly List<int> actorOrder = new(); // 순서대로 ActorNumber
        private int currentTurnIndex = 0;              // actorOrder 인덱스
        private bool _turnChanging = false;

        [Header("Retry Guard")]
        [SerializeField] private float broadcastRetryGap = 0.1f;
        [SerializeField] private int   broadcastRetryMax = 10;

        public void Initialize()
        {
            // 첫 진입 시 액터 순서 구성(마스터 기준)
            BuildActorOrderIfNeeded();
            Debug.Log("[TurnManager] Initialize");
        }

        // ======= 외부 트리거 =======

        /// <summary>마스터만: 첫 턴 세팅(필요 시 외부에서 호출)</summary>
        public void SetupTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            BuildActorOrderIfNeeded();
            ClampPointerToAlive();
            BroadcastCurrentTurnWithRetry();
        }

        /// <summary>마스터만: 다음 턴으로</summary>
        public void NextTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_turnChanging) return;
            StartCoroutine(CoNextTurn());
        }

        /// <summary>마스터만: 현재 턴 플레이어 탈락 → 다음 턴 or 게임 종료</summary>
        public void EndTurn()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_turnChanging) return;
            StartCoroutine(CoEndTurn());
        }

        // ======= RPC (클라이언트 → 마스터 요청) =======
        [PunRPC] private void RPC_RequestPassTurn(int reason)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            NextTurn();
        }
        [PunRPC] private void RPC_RequestEndTurn(int reason)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            EndTurn();
        }

        public void RequestPassTurnFromClient(int reason = 0)
            => photonView.RPC(nameof(RPC_RequestPassTurn), RpcTarget.MasterClient, reason);
        public void RequestEndTurnFromClient(int reason = 0)
            => photonView.RPC(nameof(RPC_RequestEndTurn), RpcTarget.MasterClient, reason);

        // ======= 코루틴 =======
        private IEnumerator CoNextTurn()
        {
            _turnChanging = true;

            AdvancePointerToNextAlive();

            // 브로드캐스트 재시도 가드
            for (int i = 0; i < broadcastRetryMax; i++)
            {
                if (GetCurrentTurnActor() > 0)
                {
                    BroadcastCurrentTurn();
                    _turnChanging = false;
                    yield break;
                }
                yield return new WaitForSeconds(broadcastRetryGap);
            }

            Debug.LogWarning("[TurnManager] NextTurn: actor resolve timeout → force broadcast");
            BroadcastCurrentTurn();
            _turnChanging = false;
        }

        private IEnumerator CoEndTurn()
        {
            _turnChanging = true;

            var actor = GetCurrentTurnActor();
            Debug.Log($"[TurnManager] EndTurn by Master. currentActor={actor}");

            // 탈락 처리
            if (actor > 0 && ShootingGameManager.Instance != null)
                ShootingGameManager.Instance.Eliminate(actor);

            // 게임 오버?
            if (ShootingGameManager.Instance != null && ShootingGameManager.Instance.IsGameOver())
            {
                Debug.Log("[TurnManager] Game Over.");
                _turnChanging = false;
                yield break;
            }

            // 짧은 텀 후 다음 턴
            yield return new WaitForSeconds(0.35f);
            AdvancePointerToNextAlive();

            // 브로드캐스트 재시도 가드
            for (int i = 0; i < broadcastRetryMax; i++)
            {
                if (GetCurrentTurnActor() > 0)
                {
                    BroadcastCurrentTurn();
                    _turnChanging = false;
                    yield break;
                }
                yield return new WaitForSeconds(broadcastRetryGap);
            }

            Debug.LogWarning("[TurnManager] EndTurn: actor resolve timeout → force broadcast");
            BroadcastCurrentTurn();
            _turnChanging = false;
        }

        // ======= 브로드캐스트 / 유틸 =======
        private void BroadcastCurrentTurnWithRetry()
        {
            StartCoroutine(CoNextTurn()); // 포인터 유지 + 브로드캐스트만 수행
        }

        private void BroadcastCurrentTurn()
        {
            int actor = GetCurrentTurnActor();
            if (actor <= 0)
            {
                Debug.LogWarning("[TurnManager] BroadcastCurrentTurn: invalid actor");
                return;
            }

            int round = currentRoundIndex;
            int alive = (ShootingGameManager.Instance != null)
                ? ShootingGameManager.Instance.ActiveCount
                : (PhotonNetwork.PlayerList?.Length ?? 0);
            int ending = sharedEndingCount;

            // 각 클라의 MeteorMiniGame에 전달
            var minis = Object.FindObjectsOfType<MeteorTapMiniGame>(true);
            foreach (var mm in minis)
            {
                int my = PhotonNetwork.LocalPlayer.ActorNumber;
                bool mine = (my == actor);
                mm.InitTurnWithEnding(mine, actor, round, alive, ending);
            }

            Debug.Log($"[TurnManager] BroadcastCurrentTurn actor={actor}, round={round}, alive={alive}, ending={ending}");
        }

        private void BuildActorOrderIfNeeded()
        {
            if (actorOrder.Count > 0) return;
            var players = PhotonNetwork.PlayerList;
            actorOrder.Clear();
            actorOrder.AddRange(players.OrderBy(p => p.ActorNumber).Select(p => p.ActorNumber));
            currentTurnIndex = 0;
        }

        private int GetCurrentTurnActor()
        {
            if (actorOrder.Count == 0) BuildActorOrderIfNeeded();
            if (actorOrder.Count == 0) return -1;
            if (currentTurnIndex < 0 || currentTurnIndex >= actorOrder.Count) currentTurnIndex = 0;
            return actorOrder[currentTurnIndex];
        }

        private void AdvancePointerToNextAlive()
        {
            if (actorOrder.Count == 0) BuildActorOrderIfNeeded();
            if (actorOrder.Count == 0) return;

            // 다음 인덱스로 이동하며 탈락자 스킵
            for (int step = 0; step < actorOrder.Count; step++)
            {
                currentTurnIndex = (currentTurnIndex + 1) % actorOrder.Count;
                int candidate = actorOrder[currentTurnIndex];

                bool alive = ShootingGameManager.Instance == null
                             || !ShootingGameManager.Instance.IsEliminated(candidate);
                if (alive) break;
            }
        }

        private void ClampPointerToAlive()
        {
            if (actorOrder.Count == 0) return;
            int safeGuard = actorOrder.Count;
            while (safeGuard-- > 0)
            {
                int actor = GetCurrentTurnActor();
                bool alive = ShootingGameManager.Instance == null
                             || !ShootingGameManager.Instance.IsEliminated(actor);
                if (alive) break;
                currentTurnIndex = (currentTurnIndex + 1) % actorOrder.Count;
            }
        }
    }
}
