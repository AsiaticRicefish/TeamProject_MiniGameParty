using System.Collections;
using LDH_MainGame;                  // PhotonViewSync
using LDH.LDH_Scripts.Network;      // PhotonViewCoordinator
using Photon.Pun;
using UnityEngine;

namespace YG
{

    /// <summary>
    /// BaseGameSceneController 상속 구현체(프로젝트 표준 초기화 파이프라인).
    /// - 모든 매니저 준비 후 게임 시작 알림 → MeteorTapMiniGame.OnGameStart()
    /// </summary>
    public class MeteorTapSceneController : BaseGameSceneController
    {
        [Header("Scene Binding")] [SerializeField]
        private PhotonViewCoordinator coordinator; // 필수

        [SerializeField] private MeteorTapMiniGame game; // 필수
        
        [SerializeField] private MeteorCardManager card;   // 전용 카드 매니저

        protected override string GameType => "MeteorTap";

        protected override IEnumerator WaitForManagersAwake()
        {
            yield return new WaitUntil(() => coordinator != null && game != null);
        }

        protected override IEnumerator InitializeSequentialManagers()
        {
            game.SafeInitialize();
            yield return null;
        }

        protected override IEnumerator InitializeParallelManagers()
        {
            yield return null;
        }

        protected override void NotifyGameStart()
        {
            // ★ 게임은 바로 시작하지 않고, 카드 선택 UI를 열어 순서부터 확정
            card.OpenSelectionUI();  
            
            InGameUIManager.Instance?.Show(false); // 인게임 UI 숨김 (추가)
            
            // TurnManager 이벤트 구독
            TurnManager.Instance.OnOrderInitialized += HandleOrderReady;
        }
        
        private void HandleOrderReady(int[] order)
        {
            // ★ 한 번만
            TurnManager.Instance.OnOrderInitialized -= HandleOrderReady;

            if (PhotonNetwork.IsMasterClient)
            {
                // 라운드 0의 EndCount를 마스터가 선택
                var range = new Vector2Int(20, 30); // round1 범위 (원한다면 game.GetRoundRange(0)로 노출)
                int endCount = Random.Range(range.x, range.y + 1);

                // ★ 전원에게 “같은 값” 브로드캐스트
                game.photonView.RPC("RPC_StartRound", RpcTarget.AllBuffered, 0, endCount);
            }
        }
    }
}