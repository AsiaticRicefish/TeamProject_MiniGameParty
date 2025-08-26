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
        
        

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => MainGameManager.Instance != null);

            
            MainGameManager.Instance.OnGameStart += () =>
            {
                SetLogText("Game Start!");
            };

            MainGameManager.Instance.OnPikcing += () =>
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
        }

        private void SetLogText(string logText)
        {
            if (!this.logText.gameObject.activeSelf)
            {
                this.logText.gameObject.SetActive(true);
            }
            this.logText.text = logText;
        }
    }
}