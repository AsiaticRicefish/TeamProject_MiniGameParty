using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Firebase.Database;
using UnityEngine;

namespace Data
{
    public class RealTimeUserDataRepository
    {
        private readonly FirebaseDatabase _database;
        private readonly DatabaseReference _root;

        // 생성자
        public RealTimeUserDataRepository(FirebaseDatabase database, DatabaseReference root)
        {
            _database = database;
            _root = root;
        }

        #region RealTimeDataBase - path helper

        public DatabaseReference UserRef(string uid) => _root.Child("users").Child(uid);
        public DatabaseReference CustRef(string uid) => UserRef(uid).Child("customization");
        public DatabaseReference CurrencyRef(string uid) => UserRef(uid).Child("currency");

        #endregion


        #region Load Logic

        public async UniTask<UserData> LoadOrCreateAsync(string uid)
        {
            var userSnap = await UserRef(uid).GetValueAsync();
            bool isNewUser = !userSnap.Exists;
            
            if (isNewUser)
            {
                //------ 서버에 저장된 데이터가 없는 상황 -----//
                Debug.Log("[UserDataRepository] 데이터베이스에 저장된 데이터가 없습니다.");
                
                // 2) 트랜잭션으로 '없을 때만' 기본값 생성
                var (cOk, _) = await SaveCustomizationAsync(uid, cur => (true, CustomizationData.CreateDefault()), initializeIfMissing: true);
                var (mOk, _) = await SaveCurrencyAsync(uid, cur => (true, CurrencyData.CreateDefault()), initializeIfMissing: true);

                if (!cOk || !mOk)
                    throw new Exception("[UserDataRepository] 신규 유저 생성 실패");

                // 서버 타임 포함해서 다시 읽기
                userSnap = await UserRef(uid).GetValueAsync();
                
            }
            else
            {
                Debug.Log("[UserDataRepository] 데이터베이스에 저장된 데이터가 있습니다.");
            }

            return ParseUserData(userSnap);
        }

        public async UniTask<CustomizationData> LoadCustomAsync(string uid)
        {
            var snap = await CustRef(uid).GetValueAsync();
            return ParseCustomizationData(snap);
        }


        public async UniTask<CurrencyData> LoadCurrencyAsync(string uid)
        {
            var snap = await CurrencyRef(uid).GetValueAsync();
            return ParseCurrencyData(snap);
        }

        #endregion


        #region Save Logic

        public async UniTask<(bool committed, CustomizationData latest)> SaveCustomizationAsync(string uid,
            Func<CustomizationData, (bool ok, CustomizationData next)> mutator,
            bool initializeIfMissing = false
            )
        {

            int attempts = 0;
            try
            {
                // 2) 트랜잭션 실행. 대상 노드는 CustRef(uid)
                var result = await CustRef(uid).RunTransaction(
                    mutable =>
                    {
                        attempts++;
                        Debug.Log($"[SaveCustomizationAsync] Transaction body entered! : attempts : {attempts}");
                        Debug.Log($"Current mutable.Value: {mutable?.Value}");
    
                        
                        // 3) 서버가 현재 값을 mutable.Value로 넘겨준다.
                        //      RTDB의 JSON은 C#에선 보통 Dictionary<string,object>로 전달된다.
                        //      넘어온 mutable 데이터를 나만의 규칙(델리게이트 = mutator) 에 맞게 계산해서 서버로 돌려줘야한다.
                        var dict = mutable.Value as Dictionary<string, object>;
                        if (dict == null)
                        {
                            if (initializeIfMissing)
                            {
                                // 신규 유저거나 노드가 없는 경우 → 기본값 주입
                                var def = CustomizationData.CreateDefault();
                                mutable.Value = new Dictionary<string, object>
                                {
                                    ["characterId"] = def.characterId,
                                    ["equipId"] = def.equipId,
                                    ["ownedCharacters"] = HashToBoolMap(def.ownedCharacters),
                                    ["ownedEquips"] = HashToBoolMap(def.ownedEquips),
                                    ["updatedAt"] = ServerValue.Timestamp
                                };
                                return TransactionResult.Success(mutable);
                            }
                            else
                            {
                                if (attempts <= 2)
                                {
                                    if (attempts == 1) Debug.Log($"[Tx] custom not loaded yet; retrying…");
                                    return TransactionResult.Success(mutable);
                                }
                                
                                return TransactionResult.Abort();
                            }
                         
                        }

                        // 4) 현재 값을 모델(CurrencyData)에 맞게 파싱한다.
                        var cur = new CustomizationData(dict);

                        // 5) 전달한 mutator(나만의 규칙)으로 연산 수행
                        // bool : 유효성 검증
                        // currency data : mutator에 의해 바뀐 새로운 currency data
                        var (ok, next) = mutator(cur);
                        if (!ok) return TransactionResult.Abort();

                        // 6) 성공이면 새 값 + 서버시간으로 교체
                        var newDict = new Dictionary<string, object>
                        {
                            ["characterId"] = next.characterId,
                            ["equipId"] = next.equipId,
                            ["ownedCharacters"] = HashToBoolMap(next.ownedCharacters),
                            ["ownedEquips"] = HashToBoolMap(next.ownedEquips),
                            ["updatedAt"] = ServerValue.Timestamp
                        };

                        // 7) mutable.Value를 새 값으로 교체하고 성공 리턴
                        mutable.Value = newDict;
                        return TransactionResult.Success(mutable);
                    });

                // 8) 서버 응답 반영
                bool committed = result != null;
                CustomizationData latest = committed ? ParseCustomizationData(result) : null; // 기존 파서 재사용

                return (committed, latest);
            }
            catch (Exception e)
            {
                // 네트워크 오류/권한 문제 등
                Debug.LogError($"RunTransaction failed: {e}");
                return (false, null);
            }
        }

        // 세 재화에 대한 증/감을 트랜젝션으로 처리하여 동시성 안전
        // 동시에 건드려도 값이 꼬이지 않도록 처리
        public async UniTask<(bool committed, CurrencyData latest)> SaveCurrencyAsync(
            string uid,
            Func<CurrencyData, (bool ok, CurrencyData next)> mutator,
            bool initializeIfMissing = false
            )
        {
            int attempts = 0;
            
            try
            {
                // 2) 트랜잭션 실행. 대상 노드는 CurrencyRef(uid).
                var result = await CurrencyRef(uid).RunTransaction(
                    mutable =>
                    {
                        
                        attempts++;
                        Debug.Log($"[SaveCurrencyAsync] Transaction body entered! : attempts : {attempts}");
                        Debug.Log($"Current mutable.Value: {mutable?.Value}");

                        // 3) 서버가 현재 값을 mutable.Value로 넘겨준다.
                        //      RTDB의 JSON은 C#에선 보통 Dictionary<string,object>로 전달된다.
                        //      넘어온 mutable 데이터를 나만의 규칙(델리게이트 = mutator) 에 맞게 계산해서 서버로 돌려줘야한다.
                        
                        var dict = mutable.Value as Dictionary<string, object>;
                        if (dict == null)
                        {
                            
                            if (initializeIfMissing)
                            {
                                // 신규 유저거나 노드가 없는 경우 → 기본값 주입
                                var def = CurrencyData.CreateDefault();
                                mutable.Value = new Dictionary<string, object>
                                {
                                    ["currency1"] = def.currency1,
                                    ["currency2"] = def.currency2,
                                    ["currency3"] = def.currency3,
                                    ["updatedAt"] = ServerValue.Timestamp
                                };
                                return TransactionResult.Success(mutable);
                            }
                            else
                            {
                                if (attempts <= 2)
                                {
                                    if (attempts == 1) Debug.Log($"[Tx] currency not loaded yet; retrying…");
                                    return TransactionResult.Success(mutable);
                            
                                }
                            
                                return TransactionResult.Abort();
                            }
                        }

                        // 4) 현재 값을 모델(CurrencyData)에 맞게 파싱한다.
                        var cur = new CurrencyData(dict);

                        // 5) 전달한 mutator(나만의 규칙)으로 연산 수행
                        // bool : 유효성 검증
                        // currency data : mutator에 의해 바뀐 새로운 currency data
                        var (ok, next) = mutator(cur);
                        if (!ok) return TransactionResult.Abort();

                        // 6) 성공이면 새 값 + 서버시간으로 교체
                        var newDict = new Dictionary<string, object>
                        {
                            ["currency1"] = next.currency1,
                            ["currency2"] = next.currency2,
                            ["currency3"] = next.currency3,
                            ["updatedAt"] = ServerValue.Timestamp
                        };

                        // 7) mutable.Value를 새 값으로 교체하고 성공 리턴
                        mutable.Value = newDict;
                        return TransactionResult.Success(mutable);
                    });

                // 8) 서버 응답 반영
                bool committed = result != null;
                CurrencyData latest = ParseCurrencyData(result);

                return (committed, latest);
            }
            catch (Exception e)
            {
                // 네트워크 오류/권한 문제 등
                Debug.LogError($"RunTransaction failed: {e}");
                return (false, null);
            }
        }

        public async UniTask<(bool committed, DataSnapshot snapshot)> RunUserTransactionAsync(
            string uid,
            Func<Firebase.Database.MutableData, TransactionResult> mutator)
        {
            var result = await UserRef(uid).RunTransaction(
                mutable =>
                {
                    try
                    {
                        return mutator(mutable);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Repo] Tx delegate exception: {e}");
                        return TransactionResult.Abort();
                    }
                });

            bool committed = result != null;
            return (committed, result);
        }

        #endregion


        #region Snapshot Parser

        private UserData ParseUserData(DataSnapshot snap)
        {
            var user = new UserData();

            // 1) 커스텀 데이터
            var custSnap = snap.Child("customization");
            user.customization = ParseCustomizationData(custSnap);

            // 2) 재화 데이터
            var curSnap = snap.Child("currency");
            user.currency = ParseCurrencyData(curSnap);

            return user;
        }

        private CustomizationData ParseCustomizationData(DataSnapshot customSnap)
        {
            var c = new CustomizationData();
            c.characterId = customSnap.Child("characterId").Value as string;
            c.equipId = customSnap.Child("equipId").Value as string;
            c.ownedCharacters = ParseToHashSet(customSnap.Child("ownedCharacters"));
            c.ownedEquips = ParseToHashSet(customSnap.Child("ownedEquips"));
            c.updatedAt = ReadTime(customSnap.Child("updatedAt").Value);

            return c;
        }

        private CurrencyData ParseCurrencyData(DataSnapshot currencySnap)
        {
            var c = new CurrencyData();
            c.currency1 = Convert.ToInt32(currencySnap.Child("currency1").Value);
            c.currency2 = Convert.ToInt32(currencySnap.Child("currency2").Value);
            c.currency3 = Convert.ToInt32(currencySnap.Child("currency3").Value);
            c.updatedAt = ReadTime(currencySnap.Child("updatedAt").Value);
            return c;
        }

        public static HashSet<string> ParseToHashSet(DataSnapshot mapSnap)
        {
            var hashSet = new HashSet<string>();
            if (!mapSnap.Exists) return hashSet;
            foreach (var child in mapSnap.Children)
            {
                if (Convert.ToBoolean(child.Value)) hashSet.Add(child.Key);
            }

            return hashSet;
        }

        public static HashSet<string> ParseToHashSet(object mapObj)
        {
            var hashSet = new HashSet<string>();
            if (mapObj is Dictionary<string, object> map)
            {
                foreach (var kv in map)
                {
                    try
                    {
                        if (Convert.ToBoolean(kv.Value))
                            hashSet.Add(kv.Key);
                    }
                    catch
                    {
                        /* 불리언 변환 실패는 무시 */
                    }
                }
            }

            return hashSet;
        }

        /// <summary>
        /// HashSet(string)을 Dictionary(string, bool)로 변환
        /// </summary>
        /// <param name="hashSet"></param>
        /// <returns></returns>
        private Dictionary<string, object> HashToBoolMap(HashSet<string> hashSet)
        {
            if (hashSet == null) return new();
            return hashSet.ToDictionary(x => x, _ => (object)true);
        }

        public static long ReadTime(object serverTime)
        {
            if (serverTime == null) return 0;
            try
            {
                return Convert.ToInt64(serverTime);
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}