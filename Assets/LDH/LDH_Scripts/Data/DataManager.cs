using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DesignPattern;
using Firebase.Database;
using LDH_Util;
using Store;
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
            Util_LDH.ConsoleLog(this, $"데이터 테스트 - id : {item.Id}, name : {item.Name}, price : {item.Price}, enabled : {item.Enabled}");
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

        #region 구매(Purchase) API

        public async UniTask<Store.PurchaseResult> TryPurchaseAsync(
            long[] totalsByCurrency, 
            Action<MutableData> applyOnSuccess)
        {
            if (_userRepo == null || string.IsNullOrEmpty(_uid))
                return new Store.PurchaseResult { Error = Store.PurchaseError.Network, Message = "로그인이 필요합니다." };

            if (totalsByCurrency == null || totalsByCurrency.Length < CurrencyCount)
                return new Store.PurchaseResult { Error = Store.PurchaseError.ItemNotFound, Message = "가격 정보가 올바르지 않습니다." };

            
            //트랜잭션 전에 캐시 채우기
            await _userRepo.UserRef(_uid).GetValueAsync();
            
            
            bool notEnough = false;
            var currencyKeys = CatalogProvider.Currency.GetCurrencyKeys();
            
            int attempts = 0;
            
            try
            {
                //mutable : 서버가 보내준 현재 값
                // (C#에선 보통 Dictionary<string, object> or 기본형으로 옴)
                await _userRepo.RunUserTransactionAsync(_uid, mutable =>
                {
                    attempts++;

                    // 값 읽고 → 계산하고 → mutable.Value에 "새 값" 세팅
                    // 1) 잔액 로드
                    var cur = mutable.Child("currency");
                    
                    if (cur.Value == null || cur.ChildrenCount == 0)
                    {
                        if (attempts <= 2)
                        {
                            if (attempts == 1) Debug.Log($"[Tx] currency not loaded yet; retrying… / attempts : {attempts}");
                            return TransactionResult.Success(mutable);
                        }
                        // 3번 이상 비어있으면 경로/권한/초기화 문제로 보고 실패
                        return TransactionResult.Abort();
                    }
                    
                    long[] bal = new long[CurrencyCount];
                    for (int i = 0; i < CurrencyCount; i++)
                        bal[i] = (long)(cur.Child(currencyKeys[i]).Value);
                    

                    // 2) 잔액이 부족한지 체크
                    for (int i = 0; i < CurrencyCount; i++)
                    {
                        long cost = totalsByCurrency[i];
                        if (cost > 0 && bal[i] < cost)
                        {
                            notEnough = true;
                            Debug.LogWarning($"[Tx] not enough currency{i+1}: have={bal[i]} need={cost}");

                            return TransactionResult.Abort();
                        }
                    }
                    
                    // 3) 차감 반영
                    // 바로 mutable에 쓰기
                    for (int i = 0; i < CurrencyCount; i++)
                    {
                        long cost = totalsByCurrency[i];
                        if (cost <= 0) continue;
                        bal[i] -= cost;
                        cur.Child($"currency{i + 1}").Value = bal[i];
                        
                    }
                    
                    // 4) 성공 변이(커스텀 로직) 적용
                    applyOnSuccess?.Invoke(mutable);

                    return TransactionResult.Success(mutable);
                    
                });

                if (notEnough)
                    return new Store.PurchaseResult
                    {
                        Error = Store.PurchaseError.NotEnoughCurrency,
                        Message = "재화가 부족합니다."
                    };

                // 6) 트랜잭션 성공 → 최신 데이터 로컬로 다시 로드(이벤트도 여기서 쏴짐)
                await LoadOrCreatedUserDataAsync();
                
                var spent = new List<(Define_LDH.CurrencyType, long)>();
                for (int i = 0; i < CurrencyCount; i++)
                    if (totalsByCurrency[i] > 0)
                        spent.Add(((Define_LDH.CurrencyType)i, totalsByCurrency[i]));

                return new Store.PurchaseResult
                {
                    Error = Store.PurchaseError.None,
                    Message = "구매가 완료되었습니다.",
                    Spent = spent,
                };
                
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataManager] Tx body exception: {e}");
                return new Store.PurchaseResult
                {
                    Error = Store.PurchaseError.Network,
                    Message = "구매에 실패했습니다. 잠시 후 다시 시도해주세요."
                };
            }
        }
        
        public static Action<MutableData> BuildCustomizationMutation(GrantPatch patch)
        {
            if (patch == null) return null;
            return mutable =>
            {
                var cust = mutable.Child("customization");

                var ownedChars = cust.Child("ownedCharacters");
                foreach (var id in patch.AddOwnedCharacters)
                    ownedChars.Child(id).Value = true;

                var ownedEquips = cust.Child("ownedEquips");
                foreach (var id in patch.AddOwnedEquips)
                    ownedEquips.Child(id).Value = true;

                if (!string.IsNullOrEmpty(patch.EquipCharacterId))
                    cust.Child("characterId").Value = patch.EquipCharacterId;

                if (!string.IsNullOrEmpty(patch.EquipEquipId))
                    cust.Child("equipId").Value = patch.EquipEquipId;
            };
        }

        #endregion
        
        
        
        #region Helper API

        // ----- user data 관련 helper ------ //
        public bool HasCharacter(string id) => Custom.ownedCharacters.Contains(id);
        public bool HasEquip(string id) => Custom.ownedEquips.Contains(id);

        public long GetCurrencyByType(Define_LDH.CurrencyType currencyType)
        {
            return Currency.GetCurrencyByType(currencyType);
        }

        
        // ------ item data 관련 Helper ------ //
        public (CurrencyType, long) GetItemPrice(ItemType itemType, string itemId)
        {
            ItemData itemData = itemType switch
            {
                ItemType.Character => _characterItemDict.GetValueOrDefault(itemId),
                ItemType.Equip => _equipItemDict.GetValueOrDefault(itemId),
                _ => null
            };

            if (itemData == null) return (default, 0);

            
            Debug.Log($"<color=blue> item id : {itemId}, currency type : {itemData.CurrencyType}, price : {itemData.Price}</color>");
            return (itemData.CurrencyType, itemData.Price);
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