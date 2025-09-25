using System;
using System.Collections.Generic;
using System.Linq;
using Customization;
using Cysharp.Threading.Tasks;
using Data;
using LDH_Util;
using Managers;
using Store;
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


        private PurchaseQuote _quote;

        public Func<List<ItemUnit>, UniTask> OnPurchaseSuccess; 
        
        
        protected override void Init()
        { 
            base.Init();
            closeButton.onClick.AddListener(RequestClose);
            purchaseButton.onClick.AddListener(Purchase);
        }

        public void SetData(PurchaseQuote quote)
        {
            _quote = quote;
            var totals = quote.TotalsByCurrency;
            
            // 가격 UI
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
        
        private void ClearRows()
        {
            Util_LDH.RemoveAllChildren(priceRowsParent);
        }
        
        
        // 구매
        private async void Purchase()
        {
            PurchaseResult result = await Manager.Purchase.PurchaseAsync(_quote.Lines, true);
            
            //결과에 대한 UI 반영
            if (result.Success)
            {
                if (OnPurchaseSuccess != null)
                {
                    // 아바타 적용 끝날 때까지 대기
                    await OnPurchaseSuccess(result.GrantedItems);
                }
            }
           
            Manager.UI.EnqueueToast(result.Success? Define_LDH.ToastType.Check: Define_LDH.ToastType.Error ,result.Message);
            
            //UI 닫기
            RequestClose();
        }
        
    }
}