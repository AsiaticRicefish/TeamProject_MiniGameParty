using LDH_Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_ItemPurchase : UI_Popup
    {
        [Header("Price")]
        [SerializeField] private Image priceImage;
        [SerializeField] private TMP_Text priceText;
        
        [Header("Button")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button purchaseButton;
        
        
        protected override void Init()
        { 
            base.Init();
            closeButton.onClick.AddListener(RequestClose);
            
            //todo: 구매버튼
        }

        public void SetData(Sprite priceSprite, int price)
        {
            priceImage.sprite = priceSprite;
            priceText.text = price.ToString();
        }
    }
}