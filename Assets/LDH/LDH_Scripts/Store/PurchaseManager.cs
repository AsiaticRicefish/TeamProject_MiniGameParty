using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_Util;
using Managers;
using UnityEngine;
using static LDH_Util.Define_LDH;

namespace Store
{
    public class PurchaseManager : CombinedSingleton<PurchaseManager>, IPurchaseService
    {
        private readonly List<IPriceProvider> _pricing = new();
        private readonly List<IGrantHandler> _grants = new();

        protected override void OnAwake()
        {
            RegisterDefaultCustomizationPlugins();
        }

        #region Register Plugin

        /// <summary>가격 제공자 등록 (아이템 종류별로 구현 추가)</summary>
        private void AddPricingProvider(IPriceProvider provider)
        {
            if (provider != null) _pricing.Add(provider);
        }

        /// <summary>지급/장착 핸들러 등록 (아이템 종류별로 구현 추가)</summary>
        private void AddGrantHandler(IGrantHandler handler)
        {
            if (handler != null) _grants.Add(handler);
        }

        /// <summary>커스터마이징 플러그인 일괄 등록</summary>
        private void RegisterDefaultCustomizationPlugins()
        {
            AddPricingProvider(new CustomizationPriceProvider(Manager.Data));
            AddGrantHandler(new CustomizationGrantHandler());
        }

        #endregion


        /// <summary>
        /// 견적
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async UniTask<PurchaseQuote> QuoteAsync(IEnumerable<PurchaseLine> lines)
        {
            List<PurchaseLine> resultItems = new();
            Dictionary<ItemUnit, (Define_LDH.CurrencyType type, long price)> itemsPrices = new();
            long[] totalPrice = new long[CurrencyCount];

            //구매 요청 목록이 없는 경우
            if (lines != null)
            {
                foreach (PurchaseLine line in lines)
                {
                    // 가격 제공자 찾기
                    var provider = _pricing.FirstOrDefault(p => p.Supports(line.ItemUnit.Kind));

                    if (provider == null)
                        continue;

                    // 가격, 소유 여부
                    if (!provider.TryGetPriceAndOwnership(line,
                            out (Define_LDH.CurrencyType type, long price) unitPrice,
                            out bool alreadyOwned))
                        //가격 조회 실패
                        continue;

                    // 이미 소유면 결제 대상에서 제외
                    if (alreadyOwned)
                        continue;

                    // 금액 = 단가 * 수량
                    long lineAmount = Math.Max(1, line.Quantity) * unitPrice.price;

                    // 통화별 합산
                    int idx = (int)unitPrice.type;
                    if ((uint)idx < (uint)totalPrice.Length)
                        totalPrice[idx] = Util_LDH.SafeAdd(totalPrice[idx], lineAmount, MaxCurrencyValue);

                    resultItems.Add(line);
                    itemsPrices[line.ItemUnit] = (unitPrice.type, lineAmount);
                }
            }

            await UniTask.Yield();

            return new PurchaseQuote { Lines = resultItems, TotalsByCurrency = totalPrice, ItemsPrices = itemsPrices };
        }


        /// <summary>
        /// 구매
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="autoEquip"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async UniTask<PurchaseResult> PurchaseAsync(IEnumerable<PurchaseLine> lines, bool autoEquip = false)
        {
            // 1) 견적
            var quote = await QuoteAsync(lines);
            if (quote.IsEmpty)
            {
                return new PurchaseResult
                {
                    Error = PurchaseError.AlreadyOwned,
                    Message = "이미 보유 중입니다.",
                    Spent = new List<(CurrencyType, long)>()
                };
            }

            //2) 구매 성공시 지급 받을 Patch 만들기
            var patch = new GrantPatch();
            foreach (var line in quote.Lines)
            {
                foreach (var g in _grants)
                {
                    if (g.Supports(line.ItemUnit.Kind))
                        g.Accumulate(patch, line, autoEquip);
                }
            }

            //3) 트랜잭션 실행
            #if TEST_WITHOUT_LOGIN
                PurchaseResult purchaseResult = await Manager.Data.TryPurchaseLocalAsync( quote.TotalsByCurrency, patch);
            #else
                var mutation = Data.DataManager.BuildCustomizationMutation(patch);
                PurchaseResult purchaseResult = await Manager.Data.TryPurchaseAsync( quote.TotalsByCurrency, mutation);
            #endif
            if (purchaseResult.Success)
                purchaseResult.GrantedItems = quote.Lines.Select(l => l.ItemUnit).ToList();

            return purchaseResult;
        }
    }
}