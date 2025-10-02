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

        public IReadOnlyCollection<string> OwnedCharacters => ownedCharacters;
        public IReadOnlyCollection<string> OwnedEquips => ownedEquips;
        public UnimoCombo CurrentCombo => new UnimoCombo(characterId, equipId);
        
        
        //----- 생성자 ----- //
        public CustomizationData()
        {
            characterId = default;
            equipId = default;
            ownedCharacters = new();
            ownedEquips = new HashSet<string>();
            updatedAt = 0;
        }

        public CustomizationData(Dictionary<string, object> dict)
        {
            characterId =
                dict != null && dict.TryGetValue("characterId", out var v1) ? v1.ToString() : "";
            equipId =
                dict != null && dict.TryGetValue("equipId", out var v2) ? v2.ToString() : "";
            ownedCharacters = dict != null && dict.TryGetValue("ownedCharacters", out var v3)
                ? RealTimeUserDataRepository.ParseToHashSet(v3): new();
            ownedEquips = dict != null && dict.TryGetValue("ownedEquips", out var v4)
                ? RealTimeUserDataRepository.ParseToHashSet(v4): new();
            updatedAt = dict != null && dict.TryGetValue("updatedAt", out var time)
                ? RealTimeUserDataRepository.ReadTime(time)
                : 0;
        }

        public static CustomizationData CreateDefault()
        {
            var defaultCharacterId =  Define_LDH.DefaultData.DefaultCharacter;
            var defaultEquipId     = Define_LDH.DefaultData.DefaultEquip;
            
            
            return new CustomizationData
            {
                characterId = defaultCharacterId,
                equipId = defaultEquipId,
                ownedCharacters = new HashSet<string>(Define_LDH.DefaultData.DefaultOwnedCharacters),
                ownedEquips     = new HashSet<string>(Define_LDH.DefaultData.DefaultOwnedEquips),
                updatedAt  = 0 // 저장 시 서버타임으로 채움
            };
        }
    }
}