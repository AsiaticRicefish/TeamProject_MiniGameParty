using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DesignPattern;
using Firebase.Database;
using LDH_Util;
using Network;
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
        public string UID => _uid;
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


        
        private const string USER_FILE_NAME = "user.json";

        
        
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
        public async UniTask LoadOrCreatedUserDataAsync(Action<float> progressReport = null)
        {
            
            progressReport?.Invoke(0f);

#if TEST_WITHOUT_LOGIN
            // 로컬 파일 로드 시도
            if (!JsonStore.TryLoad<UserData>(USER_FILE_NAME, out var userData) || userData == null)
            {
                // 2) 파일이 없으면 기본값 생성
                var custom   = CustomizationData.CreateDefault();
                var currency = CurrencyData.CreateDefault();
                userData     = new UserData(custom, currency);
                
                // 3) 즉시 저장 (에러 무시 가능)
                try { await JsonStore.SaveAsync(USER_FILE_NAME, userData); }
                catch (Exception e) { Debug.LogWarning($"[UserData] initial save failed: {e.Message}"); }
            }
            User = userData;
#else
            if (_userRepo == null) throw new Exception("Repository not bound.");
            User = await _userRepo.LoadOrCreateAsync(_uid);
            progressReport?.Invoke(0.8f);
#endif
            OnUserDataChanged?.Invoke(User);
            OnCustomizationChanged?.Invoke(User.customization);
            OnCurrencyChanged?.Invoke(User.currency);

            
            progressReport?.Invoke(1f);
        }

        public async UniTask LoadItemsDataAsync(Action<float> progressReport = null)
        {
            progressReport?.Invoke(0f);

            if (_itemRepo == null) return;
            
            var charTask = _itemRepo.LoadEntriesAsync(ItemType.Character);
            var equipTask = _itemRepo.LoadEntriesAsync(ItemType.Equip);
            
            var (charItems, equipItems) = await UniTask.WhenAll(charTask, equipTask);
            progressReport?.Invoke(0.7f);

            
            // …dict로 변환해서 DataManager에 저장
            _characterItemDict = ToDict(charItems);
            _equipItemDict = ToDict(equipItems);

            progressReport?.Invoke(0.9f);
            OnItemCatalogChanged?.Invoke();
            
            Util_LDH.ConsoleLog(this, $"complete loading item data - character : {charItems.Count}, equip - {equipItems.Count}");
            progressReport?.Invoke(1f);

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
            
            bool notEnough = false;
            var currencyKeys = CatalogProvider.Currency.GetCurrencyKeys();
            
            int attempts = 0;
            
            try
            {
                //mutable : 서버가 보내준 현재 값
                // (C#에선 보통 Dictionary<string, object> or 기본형으로 옴)
                var (committed, _) =  await _userRepo.RunUserTransactionAsync(_uid, mutable =>
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

                // 트랜잭션 결과 해석
                
                //  5) 트랜잭션 실패
                if (!committed)
                {
                    if (notEnough)
                    {
                        return new Store.PurchaseResult
                        {
                            Error = Store.PurchaseError.NotEnoughCurrency,
                            Message = "재화가 부족합니다."
                        };
                    }
                    // 그 외 비커밋 사유(초기화/경합 등) → 네트워크 오류로 안내
                    return new Store.PurchaseResult
                    {
                        Error = Store.PurchaseError.Network,
                        Message = "구매 처리 중 문제가 발생했습니다. 잠시 후 다시 시도해주세요."
                    };
                }
                
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


        #region Local 저장용 (테스트용)
        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private async UniTask PersistLocalAsync()
        {
            if (User != null)
                await JsonStore.SaveAsync(USER_FILE_NAME, User);
        }

        public async UniTask<bool> UpdateCustomizationLocalAsync(string newCharId, string newEquipId)
        {
#if !TEST_WITHOUT_LOGIN
            Debug.LogWarning("[DataManager] UpdateCustomizationLocalAsync는 TEST_WITHOUT_LOGIN에서만 사용하세요.");
#endif
            if (User == null) return false;

            // 소유 검증
            if (!HasCharacter(newCharId) || !HasEquip(newEquipId))
            {
                Debug.LogWarning($"[DataManager] Do not have {newCharId} or {newEquipId}. Fail to update customization (local).");
                return false;
            }
            
            
            // 로컬 데이터 수정
            var cur = User.customization ??= new CustomizationData();
            cur.characterId = newCharId;
            cur.equipId     = newEquipId;
            cur.updatedAt   = NowMs();

            // 이벤트 & 저장
            OnCustomizationChanged?.Invoke(cur);
            OnUserDataChanged?.Invoke(User);
            await PersistLocalAsync();
            return true;
            
        }

        public async UniTask<Store.PurchaseResult> TryPurchaseLocalAsync(
            long[] totalsByCurrency, // 통화별 총 비용
            GrantPatch patch // 구매 성공 시 로컬에 적용할 변경(소유 추가/장착 등)
        )
        {
#if !TEST_WITHOUT_LOGIN
            Debug.LogWarning("[DataManager] TryPurchaseLocalAsync는 TEST_WITHOUT_LOGIN에서만 사용하세요.");
#endif
            if (User == null)
                return new Store.PurchaseResult { Error = Store.PurchaseError.Network, Message = "유저 데이터가 없습니다." };

            if (totalsByCurrency == null || totalsByCurrency.Length < CurrencyCount)
                return new Store.PurchaseResult { Error = Store.PurchaseError.ItemNotFound, Message = "가격 정보가 올바르지 않습니다." };
            
            var cur = User.currency ??= new CurrencyData();
            long[] bal =
            {
                cur.currency1,
                cur.currency2,
                cur.currency3
            };
            // 1) 잔액 체크
            for (int i = 0; i < CurrencyCount; i++)
            {
                long cost = totalsByCurrency[i];
                if (cost > 0 && bal[i] < cost)
                {
                    return new Store.PurchaseResult
                    {
                        Error = Store.PurchaseError.NotEnoughCurrency,
                        Message = "재화가 부족합니다."
                    };
                }
            }

            // 2) 차감 적용
            for (int i = 0; i < CurrencyCount; i++)
            {
                long cost = totalsByCurrency[i];
                if (cost <= 0) continue;
                bal[i] -= cost;
            }
            cur.currency1 = bal[0];
            cur.currency2 = bal[1];
            cur.currency3 = bal[2];
            cur.updatedAt = NowMs();

            // 3) 보상/장착 적용 (GrantPatch를 로컬 데이터에 직접 반영)
            ApplyGrantPatchLocal(patch);

            // 4) 이벤트 & 저장
            OnCurrencyChanged?.Invoke(cur);
            OnCustomizationChanged?.Invoke(User.customization);
            OnUserDataChanged?.Invoke(User);
            await PersistLocalAsync();

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

        private void ApplyGrantPatchLocal(GrantPatch patch)
        {
            if (patch == null) return;

            var cust = User.customization ??= new CustomizationData();
            cust.ownedCharacters ??= new HashSet<string>();
            cust.ownedEquips     ??= new HashSet<string>();

            // 소유 추가
            if (patch.AddOwnedCharacters != null)
                foreach (var id in patch.AddOwnedCharacters)
                    if (!string.IsNullOrEmpty(id))
                        cust.ownedCharacters.Add(id);

            if (patch.AddOwnedEquips != null)
                foreach (var id in patch.AddOwnedEquips)
                    if (!string.IsNullOrEmpty(id))
                        cust.ownedEquips.Add(id);

            // 장착 변경(입력 값이 비어있지 않은 경우에만)
            if (!string.IsNullOrEmpty(patch.EquipCharacterId))
                cust.characterId = patch.EquipCharacterId;

            if (!string.IsNullOrEmpty(patch.EquipEquipId))
                cust.equipId = patch.EquipEquipId;

            cust.updatedAt = NowMs();
        }

        #endregion
        
        
    }
}