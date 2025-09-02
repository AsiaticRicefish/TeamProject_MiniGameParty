using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Screen_Timer : UI_Screen
    {
        [SerializeField] private TMP_Text _timerText;
        

        private void OnDisable()
        {
            //reset timer
            SetTimerText("");
        }

        public void SetTimerText(string seconds)
        {
            Debug.Log($"timer text : {seconds}");
            _timerText.text = seconds;
        }
        
    }
}