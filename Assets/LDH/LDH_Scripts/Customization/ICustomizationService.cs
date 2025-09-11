using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Customization
{
    public struct UnimoCombo
    {
        public string characterId;
        public string equipId;

        public UnimoCombo(string characterId, string equipId)
        {
            this.characterId = characterId;
            this.equipId = equipId;
        }
    }

    public interface ICustomizationService
    {
        bool IsReady { get; }
        UnimoCombo GetEquippedLocal();
        bool HasCharacter(string id);
        bool HasEquip(string id);

        UniTask<bool> UpdateComboAsync(string characterId, string equipId);        // 소유/호환 검증 + 저장(+멀티 동기화)
        UniTask<bool> UpdateCharacterAsync(string characterId);
        UniTask<bool> UpdateEquipAsync(string equipId);

        
        UniTask ApplyToAvatarAsync(AvatarStruct avatarStruct, UnimoCombo combo);  // characterRoot/equipRoot 스왑
        UniTask ApplyToAvatarAsync(AvatarStruct avatarStruct, string characterId = null, string mountId = null); // 부분 적용
        UniTask ApplyCharacterToAvatarAsync(AvatarStruct avatarStruct, string characterId);
        UniTask ApplyEquipToAvatarAsync(AvatarStruct avatarStruct, string equipId);
        
        
        UniTask<Sprite> GetIconAsync(string characterId); // 2D 프로필 조회
        event Action<UnimoCombo> OnEquippedChanged;                        // UI용
        void DisposePools();                        
    }
}