using System.Collections.Generic;

namespace Store
{

    public sealed class GrantPatch
    {
        public readonly HashSet<string> AddOwnedCharacters = new();
        public readonly HashSet<string> AddOwnedEquips     = new();
        
        public string EquipCharacterId; // null 아니면 장착
        public string EquipEquipId;     // null 아니면 장착
    }
    
    public interface IGrantHandler
    {
        bool Supports(ItemKind kind);
        void Accumulate(GrantPatch patch , Store.PurchaseLine line, bool autoEquip);
    }
}