using System;
using Cysharp.Threading.Tasks;
using LDH_MainGame;
using Managers;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_QuitGame : UI_Popup
    {
        [SerializeField] private Button okButton;

        private void Awake()
        {
            okButton?.onClick.AddListener(QuitGame);
        }

        private void QuitGame()
        {
            Debug.Log("Quit Game");
            okButton.interactable = false;
            // StartCoroutine(MainGameManager.Instance.Co_EndGame(true));
            MainGameManager.Instance.EndGameAsync().Forget();
        }
    }
}