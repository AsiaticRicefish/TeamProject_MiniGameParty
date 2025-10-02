using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YG
{
    public class MeteorTapUI : MonoBehaviour
    {
        [Header("Banners")]
        [SerializeField] private GameObject myTurnBanner;    // 내 턴일 때(나만)
        [SerializeField] private GameObject nextBanner;      // 다음 턴 주자(그 사람만)
        [SerializeField] private GameObject waitBanner;      // 둘 다 아닐 때(나머지 모두)

        [Header("Counters")]
        [SerializeField] private TMP_Text sharedCountText;
        [SerializeField] private TMP_Text roundInfoText;
        [SerializeField] private Slider progressBar;

        [Header("Buttons")]
        [SerializeField] private Button tapButton;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;

        private void SetBannerState(bool mineOn, bool nextOn, bool waitOn)
        {
            if (myTurnBanner) myTurnBanner.SetActive(mineOn);
            if (nextBanner)   nextBanner.SetActive(nextOn);
            if (waitBanner)   waitBanner.SetActive(waitOn);
        }

        /// <summary>
        /// 현재/다음 턴 주인 정보를 받아, 로컬 유저 기준으로
        /// - 내 턴이면 MyTurn만
        /// - 다음 턴 주자면 Next만
        /// - 나머지는 Wait만 켠다.
        /// </summary>
        public void UpdateTurnBanners(int currentActor, int nextActor)
        {
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            bool isMine = (currentActor == me);
            bool isNext = (!isMine && nextActor == me);
            bool isWait = (!isMine && !isNext);

            SetBannerState(isMine, isNext, isWait);
        }

        public void SetTapInteractable(bool allowed)
        {
            if (tapButton) tapButton.interactable = allowed;
        }

        public void SetSharedCount(int cur, int end)
        {
            if (sharedCountText) sharedCountText.text = $"{cur} / {end}";
        }

        public void SetRound(int roundIndex1, int endCount)
        {
            if (roundInfoText) roundInfoText.text = $"Round {roundIndex1} • End {endCount}";
        }

        public void SetProgress01(float p01)
        {
            if (progressBar) progressBar.value = p01;
        }

        /// <summary>“나만” 보는 게임오버 창 On</summary>
        public void ShowGameOverLocalOnly()
        {
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }
    }
}
