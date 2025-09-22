using Data;
using UnityEngine;

namespace Store
{
    public class CustomizationGrantHandler : IGrantHandler
    {
        private readonly DataManager _data;

        public bool Supports(ItemKind k) => k == ItemKind.Character || k == ItemKind.Equip;
        public void Accumulate(GrantPatch patch, PurchaseLine line, bool autoEquip)
        {
            var id = line.ItemUnit.Id;
            if (line.ItemUnit.Kind == ItemKind.Character)
            {
                patch.AddOwnedCharacters.Add(id);
                if (autoEquip) patch.EquipCharacterId = id;
            }
            else if (line.ItemUnit.Kind == ItemKind.Equip)
            {
                patch.AddOwnedEquips.Add(id);
                if (autoEquip) patch.EquipEquipId = id;
            }
        }
    }
}