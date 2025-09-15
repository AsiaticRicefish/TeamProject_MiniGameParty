using System;
using System.Collections.Generic;
using System.Linq;
using Customization;
using Firebase.Database;
using LDH_Util;
using UnityEngine;

namespace Data
{
    [Serializable]
    public class CustomizationData
    {
        public string characterId;
        public string equipId;
        public HashSet<string> ownedCharacters;
        public HashSet<string> ownedEquips;
        public long updatedAt; // 서버 시각(ms)

        public IReadOnlyList<string> OwnedCharacters => ownedCharacters.ToList();
        public IReadOnlyList<string> OwnedEquips => ownedEquips.ToList();
        public UnimoCombo CurrentCombo => new UnimoCombo(characterId, equipId);
        
        public static CustomizationData CreateDefault()
        {
            var defaultCharacterId =  Define_LDH.DefaultData.DefaultCharacter;
            var defaultEquipId     = Define_LDH.DefaultData.DefaultEquip;
            
            return new CustomizationData
            {
                characterId = defaultCharacterId,
                equipId = defaultEquipId,
                ownedCharacters = new() {  defaultCharacterId},
                ownedEquips = new() { defaultEquipId },
                updatedAt  = 0 // 저장 시 서버타임으로 채움
            };
        }
    }
}