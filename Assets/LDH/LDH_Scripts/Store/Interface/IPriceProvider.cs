namespace Store
{
    public interface IPriceProvider
    {
        bool Supports(ItemKind kind);
        bool TryGetPriceAndOwnership(Store.PurchaseLine line,
            out (LDH_Util.Define_LDH.CurrencyType type, long price) price,
            out bool owned);
        
    }
}