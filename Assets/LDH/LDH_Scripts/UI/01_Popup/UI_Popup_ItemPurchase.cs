using System.Collections.Generic;
using System.Linq;
using Customization;
using Data;
using LDH_Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_ItemPurchase : UI_Popup
    {
        [Header("Rows")]
        [SerializeField] private Transform priceRowsParent;             // Vertical/Horizontal Layout이 붙은 부모
        [SerializeField] private UI_PriceRow priceRowPrefab; 
        
        [Header("Button")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button purchaseButton;
        
        
        protected override void Init()
        { 
            base.Init();
            closeButton.onClick.AddListener(RequestClose);
            
            //todo: 구매버튼
        }

        public void SetData(long[] totals)

        {
            ClearRows();
            if (CatalogProvider.Currency == null) return;
            
            foreach (var entry in CatalogProvider.Currency.AllSorted())
            {
                int idx = (int)entry.type;
                if((uint)idx >= (uint)totals.Length) continue;

                long amount = totals[idx];
                if(amount <= 0) continue;

                UI_PriceRow row = Util_LDH.Instantiate(priceRowPrefab, priceRowsParent);
                var format = string.IsNullOrEmpty(entry.numberFormat) ? "N0" : entry.numberFormat;
                row.Setup(entry.icon, amount.ToString(format));

            }
        }
        
        
        private int GetSortOrder(Define_LDH.CurrencyType t)
            => CatalogProvider.TryGetCurrency(t, out var e) ? e.sortOrder : int.MaxValue;

        private string FormatAmount(long value, string fmt)
            => value.ToString(string.IsNullOrEmpty(fmt) ? "N0" : fmt);

        private void ClearRows()
        {
            Util_LDH.RemoveAllChildren(priceRowsParent);
        }
    }
}