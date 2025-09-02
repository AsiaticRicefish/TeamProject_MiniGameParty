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
                Manager.UI.ShowScreenUI(this).Forget();

            else
                Manager.UI.CloseScreenUI(this).Forget();
            
        }
    }
    }