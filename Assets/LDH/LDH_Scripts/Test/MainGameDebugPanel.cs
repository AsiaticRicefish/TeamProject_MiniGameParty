using System;
using System.Collections;
using System.Linq;
using LDH_MainGame;
using LDH_UI;
using Managers;
using TMPro;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class MainGameDebugPanel : UI_Screen
    {
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI totalRoundText;


        protected override void Init()
        {
            
            Debug.Log("[Debug Panel] MainGameManager Event Subscribe start");
            MainGameManager.Instance.OnGameStart += () =>
            {
                SetLogText("Game Start!");
            };

            MainGameManager.Instance.OnPicking += () =>
            {
                SetLogText("Selecting a random mini-game...");
            };
            MainGameManager.Instance.OnPicked += () =>
            {
                SetLogText("Mini-game selected.");
            };

            MainGameManager.Instance.OnRoundChanged += (round) =>
            {
                roundText.text = $"Current Round : {round}";
            };

            MainGameManager.Instance.OnWaitAllReady += () => logText.gameObject.SetActive(false);
            // MainGameManager.Instance.OnLoadingMiniGame += () =>
            // {
            //     Debug.Log("[MainGameDebugPanel] 미니게임 진입. 디버그 패널을 안보이게 설정합니다.");
            //     gameObject.SetActive(false);
            // };
            // MainGameManager.Instance.OnEndMiniGame += () => gameObject.SetActive(true);
            
            MainGameManager.Instance.OnEndGame += () =>
            {
                SetLogText("End Game");
            };
            
            totalRoundText.text = $"Total Round : {MainGameManager.Instance.TotalRound}";
        }

        

        private void SetLogText(string logText)
        {
            this.logText.gameObject.SetActive(true);
            this.logText.text = logText;
        }

        public void SetActiveDebugPanel(bool active)
        {
           gameObject.SetActive(active);

        }
        

    }
}