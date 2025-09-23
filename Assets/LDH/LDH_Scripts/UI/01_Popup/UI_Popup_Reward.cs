using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_Reward : UI_Popup
    {

        [SerializeField] private Image currencyIcon;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button okButton;
        [SerializeField] private Button adsButton;
        
        
        public async UniTask SetData(GamePlayer localPlayer)
        {
        }
    }
}