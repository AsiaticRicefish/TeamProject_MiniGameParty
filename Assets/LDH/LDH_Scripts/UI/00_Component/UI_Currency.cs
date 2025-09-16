using System;
using Data;
using LDH_Util;
using Managers;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Currency : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Define_LDH.CurrencyType currencyType;


        private void Start()
        {
            Manager.Data.OnCurrencyChanged += UpdateCurrencyValue;
            
            //초기값 반영
            UpdateCurrencyValue(Manager.Data.GetCurrencyByType(currencyType));
        }

        private void OnDestroy()
        {
            if(Manager.Data!=null)
                Manager.Data.OnCurrencyChanged -= UpdateCurrencyValue;
        }

        private void UpdateCurrencyValue(CurrencyData currencyData)
        {
            long value = currencyData.GetCurrencyByType(currencyType);
            UpdateCurrencyValue(value);
        }

        private void UpdateCurrencyValue(long value)
        {
            valueText.text = value.ToString();
        }
        
    }
}