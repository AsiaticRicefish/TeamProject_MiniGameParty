using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_Util;
using Unity.VisualScripting;
using UnityEngine;

namespace Data
{
    public class DataManager : CombinedSingleton<DataManager>
    {
        private UserDataRepository _repo;
        private string _uid;
        
        // data base
        public UserData User { get; private set; }
        public CustomizationData Custom => User.customization;
        public CurrencyData Currency => User.currency;
        
        
        // action
        public event Action<UserData> OnUserDataChanged;
        public event Action<CustomizationData> OnCustomizationChanged;
        public event Action<CurrencyData> OnCurrencyChanged;


        #region Init Logic

        protected override void OnAwake()
        {
            isPersistent = true;
            base.OnAwake();
        }
        
        public void BindRepository(UserDataRepository repo, string uid)
        {
            _repo = repo;
            _uid  = uid;
        }

        #endregion
    

        #region Load Data
        // 전체 유저 데이터 load or create
        public async UniTask LoadOrCreatedUserDataAsync()
        {
            if (_repo == null) throw new Exception("Repository not bound.");
            User = await _repo.LoadOrCreateAsync(_uid);
            OnUserDataChanged?.Invoke(User);
            OnCustomizationChanged?.Invoke(User.customization);
            OnCurrencyChanged?.Invoke(User.currency);
        }
        
        // 커스터마이징 데이터 메모리 업데이트 & 서버에 저장
        public async UniTask<bool> UpdateCustomizationAsync(string newCharId, string newEquipId)
        {
            if (User == null) return false;
            
            // 소유 여부 검증
            if (!HasCharacter(newCharId) || !HasEquip(newEquipId))
            {
                Debug.LogWarning($"[DataManager] Do not have {newCharId } or {newEquipId}. Fail to update customization data.");
                return false;
            }

            // 트랜잭션으로 서버 저장
            var (committed, latest) = await _repo.SaveCustomizationAsync(_uid, cur =>
            {
                if (cur.ownedCharacters == null || !cur.ownedCharacters.Contains(newCharId))
                    return (false, cur);
                if (cur.ownedEquips == null || !cur.ownedEquips.Contains(newEquipId))
                    return (false, cur);

                cur.characterId = newCharId;
                cur.equipId = newEquipId;

                return (true, cur);
            });
            
            if (!committed || latest == null)
            {
                Debug.LogWarning("[DataManager] Customization transaction aborted or failed.");
                return false;
            }
            
            // 성공시 로컬 데이터 업데이트
            User.customization = latest;
            // 이벤트
            OnCustomizationChanged?.Invoke(latest);
            OnUserDataChanged?.Invoke(User);
            await UniTask.Yield();
            return true;
        }
   
        

        #endregion


        #region Helper API

        public bool HasCharacter(string id) => Custom.ownedCharacters.Contains(id);
        public bool HasEquip(string id) => Custom.ownedEquips.Contains(id);

        public long GetCurrencyByType(Define_LDH.CurrencyType currencyType)
        {
            return Currency.GetCurrencyByType(currencyType);
        }

        #endregion

    }
}