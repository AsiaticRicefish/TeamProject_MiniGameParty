using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data;
using LDH_Util;

namespace Store
{
    //구매 가능한(파는) 상품 카테고리
    public enum ItemKind
    {
        Character,
        Equip,
    }

    public readonly struct ItemUnit : IEquatable<ItemUnit>
    {
        public ItemKind Kind { get; }
        public string Id { get; }

        //생성자
        public ItemUnit(ItemKind s, string id)
        {
            Kind = s;
            Id = id;
        }

        public bool Equals(ItemUnit other) =>
            Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);

        public static bool operator ==(ItemUnit a, ItemUnit b) => a.Equals(b);
        public static bool operator !=(ItemUnit a, ItemUnit b) => !a.Equals(b);
    }


    //구매 항목에 대한 정의가 필요함
    public readonly struct PurchaseLine
    {
        public ItemUnit ItemUnit { get; }
        public int Quantity { get; }

        public PurchaseLine(ItemUnit itemUnit, int q = 1)
        {
            ItemUnit = itemUnit;
            Quantity = q;
        }
    }

    public enum PurchaseError
    {
        None,
        AlreadyOwned, NotEnoughCurrency, ItemNotFound,
        Conflict,
        Network,
        Unknown
    }

    public struct PurchaseQuote
    {
        public List<PurchaseLine> Lines;
        public long[] TotalsByCurrency;
        public Dictionary<ItemUnit, (Define_LDH.CurrencyType type, long price)> ItemsPrices;
        public bool IsEmpty => Lines == null || Lines.Count == 0;
    }


    public struct PurchaseResult
    {
        public bool Success => Error == PurchaseError.None;
        public PurchaseError Error;
        public string Message;
        public List<(Define_LDH.CurrencyType type, long amount)> Spent;
        public List<ItemUnit> GrantedItems;
    }

    public interface IPurchaseService
    {
        UniTask<PurchaseQuote> QuoteAsync(IEnumerable<PurchaseLine> lines);
        UniTask<PurchaseResult> PurchaseAsync(IEnumerable<PurchaseLine> lines, bool autoEquip = false);
    }
}