using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_Util;
using Photon.Pun;
using UnityEngine;

namespace Customization
{
    public class CustomizationManager : CombinedSingleton<CustomizationManager>, ICustomizationService
    {
        [SerializeField] private Transform charPoolRegistry_Transform;
        [SerializeField] private Transform equipPoolRegistry_Transform;
        
        public bool IsReady { get; private set; }
    

        public event Action<UnimoCombo> OnEquippedChanged; // 장착이 변경되었을 때


        // 유저 상태
        readonly HashSet<string> _ownedCharacters = new();
        readonly HashSet<string> _ownedEquips     = new();
        private UnimoCombo _equipped;

        // 풀
        PrefabPoolRegistry  _charPools;
        PrefabPoolRegistry _equipPools;

     
        protected override void OnAwake()
        {
            isPersistent = true;
            _charPools = new(charPoolRegistry_Transform);
            _equipPools = new(equipPoolRegistry_Transform);
        }

        private void OnDisable()
        {
            DisposePools();
        }

        public async UniTask InitAsync(string uid = null)
        {
            // CatalogProvider.InitAsync()가 완료된 상태
            await LoadLocalCache(); 
            // TODO: Firestore로 치환 예정
            IsReady = true;
            await UniTask.Yield();
        }

        async UniTask LoadLocalCache()
        {
            if (!PlayerPrefs.HasKey(
                    Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId))
                && !PlayerPrefs.HasKey(
                    Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.EquipId)))
            {
                // 데모: 기본 보유/장착
                Debug.Log("저장된 커스텀 데이터 없음");
                _equipped = new UnimoCombo() { characterId = "unimo_ch_001", equipId = "unimo_equip_001" };
                _ownedCharacters.Add(_equipped.characterId);
                _ownedEquips.Add(_equipped.equipId);
                
            }

            else
            {
                Debug.Log("저장된 커스텀 데이터 있음");
               string charId = PlayerPrefs.GetString(Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId));
               string equipId =
                   PlayerPrefs.GetString(
                       Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.EquipId));
               _equipped = new UnimoCombo() { characterId = charId, equipId = equipId };
               _ownedCharacters.Add(_equipped.characterId);
               _ownedEquips.Add(_equipped.equipId);

            }
            
            SaveEquippedCombo(_equipped.characterId, _equipped.equipId);
            UpdatePhotonPlayerProps();
         
            await UniTask.Yield();
        }


        #region Interface 구현 - 커스터마이징 기능

        public UnimoCombo GetEquippedLocal() => _equipped;
        public bool HasCharacter(string id) => _ownedCharacters.Contains(id);
        public bool HasEquip(string id) => _ownedEquips.Contains(id);
        
        
      /// <summary>
      ///장착 상태를 바꾸는 메서드 (데이터를 업데이트)
      ///소유여부 검증
      ///장착 상태 업데이트 -> 파이어베이스 데이터 업데이트
      ///</summary>
      /// <param name="characterId"></param>
      /// <param name="equipId"></param>
      /// <returns></returns>
        public async UniTask<bool> UpdateComboAsync(string characterId, string equipId)
        {
            Debug.Log("update combo async");
            var newChar = characterId ?? _equipped.characterId;
            var newEquip = equipId ?? _equipped.equipId;
            Debug.Log($"Update Combo Async - new char : {newChar}, new equip : {newEquip}");
            //같은 데이터면 업데이트 하지 않음
            if (newChar.Equals(_equipped.characterId) && newEquip.Equals(_equipped.equipId))
            {
                Debug.Log($"Update Combo Async - 이미 동일함. 변경하지 않음");
                return false;
            }
            
            // todo: 소유/호환 검증(cbt에서 임시 주석 처리)
            //if (!HasCharacter(newChar) || !HasEquip(newEquip)) return false;

            _equipped = new UnimoCombo (newChar, newEquip);
            
            // TODO: Firestore 저장 or PlayerPrefs 캐시
            SaveEquippedCombo(_equipped.characterId, _equipped.equipId);
            UpdatePhotonPlayerProps();
            OnEquippedChanged?.Invoke(_equipped);
            await UniTask.Yield();
            return true;
        }

        public UniTask<bool> UpdateCharacterAsync(string characterId) => UpdateComboAsync(characterId, _equipped.equipId);

        public UniTask<bool> UpdateEquipAsync(string equipId) => UpdateComboAsync(_equipped.characterId,equipId);
        
        
        /// <summary>
        /// 장착 상태를 아바타(실제 오브젝트)에 적용하여 가시화하는 메서드. 프리팹을 이전꺼와 새로운걸 스왑
        /// 상점에서 미리보기 할 때, 룸에서 상대 플레이어의 아바타를 바꿀 때 등
        /// </summary>
        /// <param name="avatarStruct"></param>
        /// <param name="combo"></param>
        public async UniTask ApplyToAvatarAsync(AvatarStruct avatarStruct, UnimoCombo combo)
        {
            await ApplyToAvatarAsync(avatarStruct, combo.characterId, combo.equipId);
        }
     
       public async UniTask ApplyToAvatarAsync(AvatarStruct avatarStruct, string characterId = null, string equipId = null)
       {
           var targetChar  = characterId ?? _equipped.characterId;
           var targetMount = equipId ?? _equipped.equipId;
           
           // 캐릭터만 바뀐 경우에만 스왑
           //기존 캐릭터를 풀에 반납
           if (!string.IsNullOrEmpty(characterId) && avatarStruct.CurrentCharacterId != targetChar && CatalogProvider.TryGetCharacter(characterId, out var cDef))
           {
               if (avatarStruct.CurrentCharacter && !string.IsNullOrEmpty(avatarStruct.CurrentCharacterId))
               {
                   _charPools.ReleaseInstance(avatarStruct.CurrentCharacterId, avatarStruct.CurrentCharacter);
               }

               if (avatarStruct.characterRoot.childCount > 0)
                   Destroy(avatarStruct.characterRoot.GetChild(0).gameObject);
                
               //새로운 캐릭터를 꺼내와서 적용한다.
               var charObj = await _charPools.GetInstanceAsync(cDef.id, cDef.prefabRef, avatarStruct.characterRoot);
                
               avatarStruct.BindCharacter(charObj, cDef.id);
           }

           if (!string.IsNullOrEmpty(equipId) && avatarStruct.CurrentEquipId != equipId &&  CatalogProvider.TryGetEquip(equipId, out var eDef))
           {
               if (avatarStruct.CurrentEquip && !string.IsNullOrEmpty(avatarStruct.CurrentEquipId))
               {
                   _equipPools.ReleaseInstance(avatarStruct.CurrentEquipId, avatarStruct.CurrentEquip);
               }
                
               if (avatarStruct.equipRoot.childCount > 0)
                   Destroy(avatarStruct.equipRoot.GetChild(0).gameObject);
               
               //새로운 탈 것 적용
               var charObj = await _charPools.GetInstanceAsync(eDef.id, eDef.prefabRef, avatarStruct.equipRoot);
                
               avatarStruct.BindEquip(charObj, eDef.id);
           }
       }

       public UniTask ApplyCharacterToAvatarAsync(AvatarStruct avatarStruct, string characterId) =>
           ApplyToAvatarAsync(avatarStruct, characterId);
      public UniTask ApplyEquipToAvatarAsync(AvatarStruct avatarStruct, string equipId) =>  ApplyToAvatarAsync(avatarStruct,equipId:equipId);
          
        public async UniTask<Sprite> GetIconAsync(string characterId)
        {
            if(CatalogProvider.TryGetCharacter(characterId, out var def))
            {
                return await def.iconRef.LoadAssetAsync<Sprite>().Task;
            }
            return null;
        }


        
        //모든 풀 레지스트리 dispose
        public void DisposePools()
        {
            _charPools.DisposeAll();
            _equipPools.DisposeAll();
        }
        
        #endregion

        
        private void UpdatePhotonPlayerProps()
        {
            if (!Photon.Pun.PhotonNetwork.IsConnected) return;
            var table = new ExitGames.Client.Photon.Hashtable {
                { Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId),  _equipped.characterId ?? string.Empty },
                { Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.EquipId), _equipped.equipId    ?? string.Empty }
            };

            PhotonNetwork.LocalPlayer.SetCustomProperties(table);
        }


        private void SaveEquippedCombo(string charId, string equipId)
        {
            PlayerPrefs.SetString(Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId), charId);
            PlayerPrefs.SetString(Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.EquipId), equipId);
            
            Debug.Log($"[CustomizationManager] 커스텀 저장 완료 : {charId}, {equipId}");
        }
    }
}