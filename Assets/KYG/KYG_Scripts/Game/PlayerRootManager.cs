using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace KYG
{
    public class PlayerRootManager : MonoBehaviour
    {
        [Header("자식 이름: '1P','2P','3P','4P' 를 정확히 맞추세요")]
        public Transform[] playerRoots; // 0:1P, 1:2P, 2:3P, 3:4P

        [Header("디버깅: 전부 보이기 강제 (턴 진행 안돼도 확인 가능)")]
        public bool showAllForDebug = true;

        // actorNumber -> slotIndex(0~3) 매핑
        private readonly Dictionary<int, int> _actorToSlot = new();

        void Awake()
        {
            TryAutoScanChildren();
            if (showAllForDebug) SetAllActive(true);
            DebugDump("Awake");
        }

        // === Public API ===

        /// <summary>턴 순서 결과(예: [3,1,4,2])를 받아 actorNumber→슬롯 매핑 구성</summary>
        public void ApplyActorOrder(int[] actorOrder)
        {
            _actorToSlot.Clear();
            if (actorOrder == null || actorOrder.Length == 0)
            {
                Debug.LogWarning("[PRM] ApplyActorOrder: actorOrder is null/empty");
                return;
            }

            for (int slot = 0; slot < actorOrder.Length && slot < 4; slot++)
            {
                int actor = actorOrder[slot];
                _actorToSlot[actor] = slot;
            }

            RefreshVisibility_AllExceptEliminated(System.Array.Empty<int>());
            DebugDump("ApplyActorOrder");
        }

        /// <summary>탈락자만 숨기고 나머지는 전부 보이기</summary>
        public void RefreshVisibility_AllExceptEliminated(int[] eliminatedActors)
        {
            if (playerRoots == null) return;

            // 1) 우선 전부 보이기
            SetAllActive(true);

            // 2) 탈락한 액터가 매핑된 슬롯만 숨김
            if (eliminatedActors != null)
            {
                foreach (var actor in eliminatedActors)
                {
                    if (_actorToSlot.TryGetValue(actor, out int slot))
                    {
                        if (slot >= 0 && slot < playerRoots.Length && playerRoots[slot])
                            playerRoots[slot].gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>오버로드: 탈락자 정보 없을 때 기본 동작</summary>
        public void RefreshVisibility_AllExceptEliminated() =>
            RefreshVisibility_AllExceptEliminated(System.Array.Empty<int>());

        /// <summary>내 로컬 플레이어 기준으로 슬롯만 남기고 나머지는 숨김</summary>
        public void ApplyForLocalPlayer()
        {
            if (playerRoots == null || playerRoots.Length == 0)
                TryAutoScanChildren();

            if (playerRoots == null || playerRoots.Length == 0) return;

            if (showAllForDebug)
            {
                SetAllActive(true);
                DebugDump("ApplyForLocalPlayer(showAllForDebug)");
                return;
            }

            int mySlot = ResolveMySlotIndex();

            if (mySlot < 0 || mySlot >= playerRoots.Length)
            {
                // fallback: 아무 슬롯이나 ON
                int fallback = System.Array.FindIndex(playerRoots, r => r != null);
                for (int i = 0; i < playerRoots.Length; i++)
                    SafeSetActive(playerRoots[i], i == fallback);
                DebugDump($"ApplyForLocalPlayer(fallback={fallback})");
                return;
            }

            // 내 슬롯만 보이고 나머지는 끔
            for (int i = 0; i < playerRoots.Length; i++)
                SafeSetActive(playerRoots[i], i == mySlot);

            DebugDump($"ApplyForLocalPlayer(mySlot={mySlot})");
        }

        // === Helpers ===

        /// <summary>Hierarchy에서 1P~4P를 자동 스캔해서 playerRoots 채움</summary>
        private void TryAutoScanChildren()
        {
            var found = new Transform[4];
            for (int i = 1; i <= 4; i++)
            {
                string name = $"{i}P";
                var t = transform.Find(name);
                found[i - 1] = t;
            }
            playerRoots = found;

            string[] arr = playerRoots.Select(r => r ? r.name : "null").ToArray();
            Debug.Log($"[PRM] AutoScan: {arr.Length} roots → [{string.Join(", ", arr)}]");
        }

        /// <summary>모든 슬롯 활성/비활성</summary>
        private void SetAllActive(bool on)
        {
            if (playerRoots == null) return;
            foreach (var r in playerRoots)
                SafeSetActive(r, on);
        }

        /// <summary>내 로컬 ActorNumber → 슬롯 인덱스 계산</summary>
        private int ResolveMySlotIndex()
        {
#if PHOTON_UNITY_NETWORKING
            if (!Photon.Pun.PhotonNetwork.IsConnected) return -1;
            int myActor = Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber;
            if (_actorToSlot.TryGetValue(myActor, out int slot)) return slot;
#endif
            return -1;
        }

        /// <summary>null 가드 포함 SetActive</summary>
        private void SafeSetActive(Transform t, bool on)
        {
            if (t && t.gameObject.activeSelf != on)
                t.gameObject.SetActive(on);
        }

        /// <summary>현재 상태 덤프</summary>
        public void DebugDump(string tag)
        {
            string roots = (playerRoots == null) ? "null"
                : string.Join(",", playerRoots.Select((r, i) =>
                    $"{i}:{(r ? r.name : "null")}({(r && r.gameObject.activeInHierarchy ? "ON" : "off")})"));
            string map = string.Join(",", _actorToSlot.Select(kv => $"{kv.Key}->{kv.Value}"));
            Debug.Log($"[PRM][{tag}] roots=[{roots}] map=[{map}]");
        }
    }
}
