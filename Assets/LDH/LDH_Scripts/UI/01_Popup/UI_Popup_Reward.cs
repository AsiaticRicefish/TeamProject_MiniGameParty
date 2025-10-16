using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using LDH_MainGame;
using LDH_Util;
using Managers;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_Reward : UI_Popup
    {
        [Header("UI")] [SerializeField] private Image currencyIcon;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button okButton;
        [SerializeField] private Button adsButton;

        [Header("Sound")] [SerializeField] private Define_LDH.SfxKey sfxKey = Define_LDH.SfxKey.Main_Reward;

        private int reward;
        private Define_LDH.CurrencyType rewardType;
        private bool claimed; // 최종 수령 완료 플래그
        private bool requesting; // 어떤 비동기 작업(광고/수령) 중인지

        protected override async void Init()
        {
            base.Init();

            await Manager.Ads.LoadRewardedAsync();
            
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnClickClaim);

            adsButton.onClick.RemoveAllListeners();
            adsButton.onClick.AddListener(OnClickAds);
            
            // 버튼 활성화 처리
            SetButtonsInteractable(true);
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
            requesting = false;

            await UniTask.Yield();
        }

        private void SetButtonsInteractable(bool v)
        {
            if (okButton) okButton.interactable = v;
            if (adsButton) adsButton.interactable = v && Manager.Ads.IsRewardedReady;
        }


        private async void OnClickClaim()
        {
            if (requesting || claimed || reward <= 0) return;

            requesting = true;
            SetButtonsInteractable(false);
            try
            {
                await ClaimAsync(); // 성공 시 내부에서 메인 게임 씬 종료 처리 & 로비로 이동
            }
            finally
            {
                // 실패했을 때를 대비해 복구
                if (!claimed) SetButtonsInteractable(true);
                requesting = false;
            }
        }

        private async void OnClickAds()
        {
            if (requesting || claimed || reward <= 0) return;

            requesting = true;
            SetButtonsInteractable(false);

            try
            {
                bool earned = false; // 광고 시청 완료 여부

                // 광고 표시 & 보상 콜백
                bool closed = await Manager.Ads.ShowRewardedAsync(rewardObj =>
                {
                    earned = true;
                });

                // 창은 닫혔지만 보상 자격(earned)이 없을 수 있음(중도 종료)
                if (!closed || !earned)
                {
                    // 실패/중도취소 → 다시 시도 가능하게 버튼 복구
                    Manager.UI.EnqueueToast(Define_LDH.ToastType.Notify, "광고 시청이 완료되지 않았습니다.");
                    // 그러면 여기서 다시 시도 가능하게 버튼을 복구해야하는데 return해버려서 finally 실행 안되는거 아니야?
                    return;
                }
                
                Debug.Log("<color=red>광고 시청 완료</color>");

                // 보상 2배
                reward *= 2;
                // 수령
                await ClaimAsync();
            }
            finally
            {
                if (!claimed) SetButtonsInteractable(true);
                requesting = false;
            }
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
                Manager.UI.EnqueueToast(Define_LDH.ToastType.Check, $"보상 {reward} 수령 완료!", 2f);
                await UniTask.Delay(TimeSpan.FromSeconds(1.5f));


                if (MainGameManager.Instance != null)
                {
                    MainGameManager.Instance.EndGameAsync(false).Forget();
                }
                else if(MainGameManager.Instance == null && !PhotonNetwork.IsConnected)
                {
                    
                    Debug.Log("Photon Connection Loss. MainGameManager is null -> Try to reconnect server");
                    await Manager.UI.CloseAllPopupUI();
                    await UniTask.WaitUntil(() => Photon.Pun.PhotonNetwork.IsConnectedAndReady);
                }
               
            }
            else
            {
                claimed = false;
                okButton.interactable = true;
                Manager.UI.EnqueueToast(Define_LDH.ToastType.Error, "보상 수령에 실패했습니다. 다시 시도해 주세요.");

            }
            
            
        }


        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            SoundManager.Instance.PlaySFX(sfxKey.ToString());
            await base.OnShowAsync(ct);
        }
    }
}