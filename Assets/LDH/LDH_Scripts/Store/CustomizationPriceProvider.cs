using LDH_Util;
using UnityEngine;

namespace Store
{
    public class CustomizationPriceProvider : IPriceProvider
    {
        private readonly Data.DataManager _data;
        public CustomizationPriceProvider(Data.DataManager data) => _data = data;


        public bool Supports(ItemKind kind) => kind == ItemKind.Character || kind == ItemKind.Equip;

        public bool TryGetPriceAndOwnership(PurchaseLine line, out (Define_LDH.CurrencyType type, long price) price, out bool owned)
        {
            price = default; 
            owned  = false;

            var itemUnit = line.ItemUnit;
            switch (itemUnit.Kind)
            {
                case ItemKind.Character:
                    owned = _data.HasCharacter(itemUnit.Id);
                    if (!owned) price = _data.GetItemPrice(LDH_Util.Define_LDH.ItemType.Character, itemUnit.Id);
                    return true;
                case ItemKind.Equip:
                    owned = _data.HasEquip(itemUnit.Id);
                    if (!owned) price = _data.GetItemPrice(LDH_Util.Define_LDH.ItemType.Equip, itemUnit.Id);
                    return true;
                default:
                    return false;
            }
        }
    }
}