using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace YG
{
    
    public class MeteorTapUI : MonoBehaviour
    {
        [Header("Banners")]
        [SerializeField] private GameObject myTurnBanner;     // 로컬 전용
        [SerializeField] private GameObject nextBanner;  // 전원 공통

        [Header("Counters")]
        [SerializeField] private TMP_Text sharedCountText;
        [SerializeField] private TMP_Text roundInfoText;
        [SerializeField] private Slider progressBar;          // 0~1

        [Header("Buttons")]
        [SerializeField] private Button tapButton;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;
        
        [Header("Timings")]
        [SerializeField, Tooltip("턴 알림 배너가 잠깐 표시되는 시간(초)")]
        private float bannerShowSec = 0.8f;
        
        private Coroutine _myTurnCo;
        private Coroutine _nextCo;

        /// <summary>
        /// “내 턴 시작”과 “턴 전환(NEXT)”을 짧게 보여주고 자동으로 숨김.
        /// - prevActor: 이전 턴 주인
        /// - currActor: 현재 턴 주인
        /// </summary>
        public void ShowTurnTransition(int prevActor, int currActor)
        {
            bool isMine = currActor == PhotonNetwork.LocalPlayer.ActorNumber;

            // 내 턴 배너 : 당사자에게만 짧게 표시
            if (myTurnBanner)
            {
                if (_myTurnCo != null) StopCoroutine(_myTurnCo);
                _myTurnCo = StartCoroutine(Co_Flash(myTurnBanner, isMine ? bannerShowSec : 0f));
            }

            // NEXT 배너 : 모든 클라이언트에서 동일하게 잠깐 표시
            if (nextBanner)
            {
                if (_nextCo != null) StopCoroutine(_nextCo);
                _nextCo = StartCoroutine(Co_Flash(nextBanner, bannerShowSec));
            }
        }
        
        private IEnumerator Co_Flash(GameObject go, float sec)
        {
            if (!go) yield break;
            go.SetActive(false);
            if (sec <= 0f) yield break;

            go.SetActive(true);
            yield return new WaitForSeconds(sec);
            go.SetActive(false);
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

        /// <summary>
        /// “나만” 보는 게임오버 창 On (다른 사람 화면엔 뜨지 않음)
        /// </summary>
        public void ShowGameOverLocalOnly()
        {
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }
    }
}