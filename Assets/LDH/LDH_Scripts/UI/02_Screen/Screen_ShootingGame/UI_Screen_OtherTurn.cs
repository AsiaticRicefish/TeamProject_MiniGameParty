using System;
using Cysharp.Threading.Tasks;
using Managers;
using ShootingScene;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Screen_OtherTurn : UI_Screen
    {
        [SerializeField] private TMP_Text playerName;

        public void SetCurrentPlayerName(bool isMyTurn, int currentTurnIndex)
        {

            if (isMyTurn)
            {
                Manager.UI.CloseScreenUI(this).Forget();
            }


            else
            {
                Manager.UI.ShowScreenUI(this).Forget();

                var currentPlayer = TurnManager.Instance.GetCurrentTurnPlayer();
                if (currentPlayer != null)
                    playerName.text = currentPlayer.Nickname;
                else
                {
                    Debug.Log("[UI_Screen_OtherTurn] currentPlayer가 null 입니다.");
                    playerName.text = "error";
                }
            }
                
        }
    }
    }
