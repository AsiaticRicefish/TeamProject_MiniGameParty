// LocalMiniGameBoot.cs
using UnityEngine;

namespace KYG
{
    public class LocalMiniGameBoot : MonoBehaviour
    {
        [SerializeField] int debugRound = 1;
        [SerializeField, Range(1,4)] int alivePlayers = 2;
        [SerializeField] bool iStartFirst = true; // 내 턴으로 시작

        void Start()
        {
            var mini = FindObjectOfType<MeteorTapMiniGame>();
            if (mini != null)
            {
                mini.InitTurn(iStartFirst, debugRound, alivePlayers); // 로컬 시작!
                Debug.Log("[LocalMiniGameBoot] MeteorTapMiniGame.InitTurn called.");
            }
            else
            {
                Debug.LogWarning("[LocalMiniGameBoot] MeteorTapMiniGame not found in scene.");
            }
        }
        
        public void StartByCardOrder(int firstOwnerIndex, int alivePlayers)
        {
            var mini = FindObjectOfType<MeteorTapMiniGame>();
            if (mini == null) return;

            // 로컬 규칙: P0이 ‘나’로 가정
            bool iStartFirst = (firstOwnerIndex == 0);
            mini.InitTurn(iStartFirst, 1, alivePlayers);
            Debug.Log($"[LocalMiniGameBoot] StartByCardOrder → iStartFirst={iStartFirst}, alive={alivePlayers}");
        }
    }
}