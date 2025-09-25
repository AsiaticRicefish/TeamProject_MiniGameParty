using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YG
{
    
    public class MeteorTapUI : MonoBehaviour
    {
        [Header("Banners")]
        [SerializeField] private GameObject myTurnBanner;     // 로컬 전용
        [SerializeField] private GameObject otherTurnBanner;  // 전원 공통

        [Header("Counters")]
        [SerializeField] private TMP_Text sharedCountText;
        [SerializeField] private TMP_Text roundInfoText;
        [SerializeField] private Slider progressBar;          // 0~1

        [Header("Buttons")]
        [SerializeField] private Button tapButton;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;

        public void ShowTurnBanners(int nowActor)
        {
            bool isMine = nowActor == PhotonNetwork.LocalPlayer.ActorNumber;

            if (myTurnBanner)    myTurnBanner.SetActive(isMine);
            if (otherTurnBanner) otherTurnBanner.SetActive(!isMine);

            // 원한다면 0.8초만 켠 후 자동 off 코루틴 추가 가능
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

        public void ShowGameOver()
        {
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }
    }
}
