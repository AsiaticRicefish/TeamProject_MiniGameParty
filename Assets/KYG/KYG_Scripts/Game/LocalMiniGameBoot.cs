// LocalMiniGameBoot.cs
using UnityEngine;

namespace KYG
{
    public class LocalMiniGameBoot : MonoBehaviour
    {
        [SerializeField] int debugRound = 1;
        [SerializeField, Range(2,4)] int defaultAlivePlayers = 2;
        [SerializeField] bool autoStartWithoutOrder = false; // ★ 새 플래그

        private int[] _turnOrder;   // 카드에서 받은 순서(0..N-1) — 0이 '나'
        private int   _orderPtr;
        private int   _alivePlayers;

        void Start()
        {
            // ★ 카드 뽑기 전 자동 시작 금지
            if (!autoStartWithoutOrder) return;

            // (순서가 없는 단독 테스트용일 때만)
            var mini = FindObjectOfType<MeteorTapMiniGame>();
            if (mini != null)
            {
                _alivePlayers = defaultAlivePlayers;
                mini.InitTurn(isMine: true, debugRound, _alivePlayers);
            }
        }

        public void SetOrder(int[] order, int alivePlayers)
        {
            _turnOrder = order;
            _alivePlayers = alivePlayers;
            _orderPtr = 0;
        }

        public void StartOrder()
        {
            if (_turnOrder == null || _turnOrder.Length == 0) return;
            var mini = FindObjectOfType<MeteorTapMiniGame>();
            if (mini == null) return;

            bool isMine = (_turnOrder[_orderPtr] == 0); // 0이 ‘나’
            mini.InitTurn(isMine, debugRound, _alivePlayers);
        }

        public void NextLocalTurn()
        {
            if (_turnOrder == null || _turnOrder.Length == 0)
            {
                // (폴백) 순서 없으면 토글 테스트
                var mini = FindObjectOfType<MeteorTapMiniGame>();
                if (mini == null) return;
                mini.InitTurn(!mini.IsMyTurn, debugRound, defaultAlivePlayers);
                return;
            }

            _orderPtr = (_orderPtr + 1) % _turnOrder.Length;
            var m = FindObjectOfType<MeteorTapMiniGame>();
            if (m == null) return;

            bool isMine = (_turnOrder[_orderPtr] == 0);
            m.InitTurn(isMine, debugRound, _alivePlayers);
        }
    }
}
