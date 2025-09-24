using System;
using Cysharp.Threading.Tasks;
using Data;
using LDH_MainGame;
using LDH_Util;
using Managers;
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

        private int reward;
        private Define_LDH.CurrencyType rewardType;
        private bool claimed;

        protected override void Init()
        {
            base.Init();
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnClickClaim);


            //todo: ads button
        }


        public async UniTask SetData(GamePlayer localPlayer)
        {
            reward = localPlayer.Reward;
            rewardType = Define_LDH.DefaultData.DefaultRewardCurrency;

            if (CatalogProvider.TryGetCurrency(rewardType, out var meta))
            {
                currencyIcon.sprite = meta.icon;
                rewardText.text = reward.ToString(meta.numberFormat);
            }

            claimed = false;
            adsButton.interactable = false; // 광고 로직 붙이기 전 기본 꺼두기(todo: 수정 예정)
            await UniTask.Yield();
        }

        private async void OnClickClaim()
        {
            // Button.onClick은 async 직접 못 붙임 → 래핑해서 fire & forget
            await ClaimAsync();
        }

        private async UniTask ClaimAsync()
        {
            if (claimed || reward <= 0)
                return;

            okButton.interactable = false;
#if TEST_WITHOUT_LOGIN
            var result = await DataManager.Instance.UpdateCurrencyLocalAsync(rewardType, reward);
#else
            var result = await DataManager.Instance.UpdateCurrencyAsync(rewardType, reward);
#endif
            if (result)
            {
                claimed = true;
                Manager.UI.EnqueueToast(Define_LDH.ToastType.Check,$"보상 {reward} 수령 완료!");
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f));
                MainGameManager.Instance?.EndGameAsync(false).Forget();
            }
            else
            {
                claimed = false;
                okButton.interactable = true;
            }
        }
    }
}