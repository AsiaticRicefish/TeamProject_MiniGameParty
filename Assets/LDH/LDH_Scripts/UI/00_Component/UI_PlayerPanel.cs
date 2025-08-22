using System;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_PlayerPanel : MonoBehaviour
    {
        [SerializeField] private Button readyButton;

        [SerializeField] private string readyMessage = "Ready";
        [SerializeField] private string notReadyMessage = "Not Ready";
        [SerializeField] private Color readyColor;
        [SerializeField] private Color notReadyColor;


        private void Start()
        {
           
        }


        public void SetButtonState(bool isReady)
        {
            readyButton.GetComponentInChildren<TextMeshProUGUI>().text = isReady ? readyMessage : notReadyMessage;
            readyButton.image.color = isReady ? readyColor : notReadyColor;
        }

    }
}