using System;
using System.Collections;
using System.Linq;
using LDH_MainGame;
using Managers;
using TMPro;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class MainGameDebugPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI totalRoundText;
        
        

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => MainGameManager.Instance != null);

            
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
            MainGameManager.Instance.OnLoadingMiniGame += () => gameObject.SetActive(false);
            MainGameManager.Instance.OnEndMiniGame += () => gameObject.SetActive(true);
            
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

    }
}