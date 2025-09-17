using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DesignPattern;
using LDH_Util;
using UnityEngine;
using static LDH_Util.Define_LDH;

namespace Data
{
    public class DataManager : CombinedSingleton<DataManager>
    {
        //----- Real time data base ------//
        private string _uid;
        private RealTimeUserDataRepository _userRepo;
        private FirestoreItemRepository _itemRepo;
        
        //----- User Data ---- //
        public UserData User { get; private set; }
        public CustomizationData Custom => User.customization;
        public CurrencyData Currency => User.currency;
        
        //------ Firestore Item Data 캐싱 ----- //
        private Dictionary<string, ItemData> _characterItemDict = new();
        private Dictionary<string, ItemData> _equipItemDict = new();
        
        public IReadOnlyDictionary<string, ItemData> CharacterItemDict => _characterItemDict;
        public IReadOnlyDictionary<string, ItemData> EquipItemDict => _equipItemDict;


        
        // ----- action ------ //
        public event Action<UserData> OnUserDataChanged;
        public event Action<CustomizationData> OnCustomizationChanged;
        public event Action<CurrencyData> OnCurrencyChanged;
        public event Action OnItemCatalogChanged;


        #region Init Logic

        protected override void OnAwake()
        {
            isPersistent = true;
            base.OnAwake();
        }
        
        public void BindUserDataRepository(RealTimeUserDataRepository repo, string uid)
        {
            _userRepo = repo;
            _uid  = uid;
        }
        
        public void BindItemRepository(FirestoreItemRepository repo)
        {
            _itemRepo = repo;
        }

        #endregion
    

        #region Load Data
        
        // 전체 유저 데이터 load or create
        public async UniTask LoadOrCreatedUserDataAsync()
        {
            if (_userRepo == null) throw new Exception("Repository not bound.");
            User = await _userRepo.LoadOrCreateAsync(_uid);
            OnUserDataChanged?.Invoke(User);
            OnCustomizationChanged?.Invoke(User.customization);
            OnCurrencyChanged?.Invoke(User.currency);
        }

        public async UniTask LoadItemsDataAsync()
        {
            Debug.Log($"Auth user: {BackendManager.Auth?.CurrentUser?.UserId}");
            Debug.Log($"App.ProjectId: {Firebase.FirebaseApp.DefaultInstance.Options.ProjectId}");
            
            
            if (_itemRepo == null) return;
            
            var charTask = _itemRepo.LoadEntriesAsync(ItemType.Character);
            var equipTask = _itemRepo.LoadEntriesAsync(ItemType.Equip);
            
            var (charItems, equipItems) = await UniTask.WhenAll(charTask, equipTask);

            // …dict로 변환해서 DataManager에 저장
            _characterItemDict = ToDict(charItems);
            _equipItemDict = ToDict(equipItems);

            OnItemCatalogChanged?.Invoke();
            
            Util_LDH.ConsoleLog(this, $"complete loading item data - character : {charItems.Count}, equip - {equipItems.Count}");

            var item = _characterItemDict[Define_LDH.DefaultData.DefaultCharacter];
            Util_LDH.ConsoleLog(this, $"데이터 테스트 - id : {item.Id}, name : {item.Name}, price : {item.Price}, enabled : {enabled}");
        }

        
        #endregion

        #region Update(Save) Data

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
            var (committed, latest) = await _userRepo.SaveCustomizationAsync(_uid, cur =>
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


        #region ETC

        private Dictionary<string, ItemData> ToDict(List<ItemData> list)
        {
            var dict = new Dictionary<string, ItemData>(StringComparer.Ordinal);
            if (list == null) return dict;
            foreach (var it in list)
                if (!string.IsNullOrWhiteSpace(it.Id))
                    dict[it.Id] = it;
            return dict;
        }

        #endregion

    }
}