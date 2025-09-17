using System;
using Cysharp.Threading.Tasks;
using Data;
using LDH_Util;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Currency : MonoBehaviour
    {
        [SerializeField] private Image currencyIcon;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Define_LDH.CurrencyType currencyType;
        
        private string _format = "N0";


        private async void Start()
        {
            await Init();
            
            await UniTask.WaitUntil(() => Manager.Data != null);
            Manager.Data.OnCurrencyChanged += UpdateCurrencyValue;
            
            // 초기값 반영
            var v = Manager.Data?.GetCurrencyByType(currencyType) ?? 0;
            UpdateValue(v);
        }

        private void OnDestroy()
        {
            if (Manager.Data != null)
                Manager.Data.OnCurrencyChanged -= UpdateCurrencyValue;
        }


        private async UniTask Init()
        {
            await UniTask.WaitUntil(() => CatalogProvider.IsReady);

            CurrencyCatalog.Entry meta = null;
            CatalogProvider.TryGetCurrency(currencyType, out meta);
            if (meta == null)
            {
                Debug.LogWarning("CatalogProvider.TryGetCurrency(currencyType, out meta); is failed");
                return;
            }
            
            if ( currencyIcon != null)
            {
                currencyIcon.sprite = meta.icon;
                currencyIcon.enabled = true;
            }

            _format = string.IsNullOrEmpty(meta.numberFormat) ? "N0" : meta.numberFormat;

        }
        
        private void UpdateCurrencyValue(CurrencyData currencyData)
        {
            long value = currencyData.GetCurrencyByType(currencyType);
            UpdateValue(value);
        }

        
        private void UpdateValue(long value)
        {
            if (valueText)
                valueText.text = value.ToString(_format);
        }
        
    }
}