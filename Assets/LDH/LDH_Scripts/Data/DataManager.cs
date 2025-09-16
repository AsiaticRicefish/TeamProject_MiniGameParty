using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DesignPattern;
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
            
            // 메모리 갱신
            Custom.characterId = newCharId;
            Custom.equipId     = newEquipId;

            // 서버 저장 (Repo)
            await _repo.SaveCustomizationAsync(_uid, Custom);

            // 이벤트
            OnCustomizationChanged?.Invoke(Custom);
            OnUserDataChanged?.Invoke(User);

            await UniTask.Yield();
            return true;
        }
        
        // 커스터마이징 데이터 메모리 업데이트 & 서버에 저장
        // public async UniTask<bool> UpdateCurrencyDataAsync(int d1, int d2, int d3) 
        // {
        //     if (User == null) return false;
        //     
        //     // 메모리 갱신
        //     User.currency = latest;
        //     OnCurrencyChanged?.Invoke(latest);
        //     OnUserDataChanged?.Invoke(User);
        //     
        //     await UniTask.Yield();
        //     return true;
        // }

        

        #endregion

        
        
        
        public bool HasCharacter(string id) => Custom.ownedCharacters.Contains(id);
        public bool HasEquip(string id) => Custom.ownedEquips.Contains(id);
        
    }
}