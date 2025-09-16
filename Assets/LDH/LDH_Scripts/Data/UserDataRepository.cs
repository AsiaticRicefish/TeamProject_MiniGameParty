using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Firebase.Database;
using LDH_Util;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

namespace Data
{
    public class UserDataRepository
    {
        private FirebaseDatabase _database;
        private DatabaseReference _root;
        private string _databaseUrl;


        // 생성자
        public UserDataRepository(FirebaseDatabase database, DatabaseReference root, string databaseUrl)
        {
            _database = database;
            _root = root;
            _databaseUrl = databaseUrl;
        }

        #region RealTimeDataBase - path helper

        private DatabaseReference UserRef(string uid) => _root.Child("users").Child(uid);
        private DatabaseReference CustRef(string uid) => UserRef(uid).Child("customization");
        private DatabaseReference CurrencyRef(string uid) => UserRef(uid).Child("currency");

        #endregion


        #region Load Logic

        public async UniTask<UserData> LoadOrCreateAsync(string uid)
        {
            var userSnap = await UserRef(uid).GetValueAsync();

            if (!userSnap.Exists)
            {
                //------ 서버에 저장된 데이터가 없는 상황 -----//
                Debug.Log("[UserDataRepository] 데이터베이스에 저장된 데이터가 없습니다.");
                var customData = CustomizationData.CreateDefault();
                var currencyData = CurrencyData.CreateDefault();

                var customTask = SaveCustomizationAsync(uid, customData);
                var currencyTask = SaveCurrencyAsync(uid, currencyData);
                await UniTask.WhenAll(customTask, currencyTask);

                // 서버타임 포함해 다시 읽어오기
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

        public async UniTask SaveCustomizationAsync(string uid, CustomizationData c)
        {
            var map = new Dictionary<string, object>
            {
                ["characterId"] = c.characterId,
                ["equipId"] = c.equipId,
                ["ownedCharacters"] = HashToBoolMap(c.ownedCharacters),
                ["ownedEquips"] = HashToBoolMap(c.ownedEquips),
                ["updatedAt"] = ServerValue.Timestamp
            };
            await CustRef(uid).UpdateChildrenAsync(map);
        }


        // 한번에 세가지 종류의 재화 동시에 저장
        public async UniTask SaveCurrencyAsync(string uid, CurrencyData c)
        {
            var map = new Dictionary<string, object>
            {
                ["currency1"] = c.currency1,
                ["currency2"] = c.currency2,
                ["currency3"] = c.currency3,
                ["updatedAt"] = ServerValue.Timestamp
            };
            await CurrencyRef(uid).UpdateChildrenAsync(map);
        }

        // 세 재화에 대한 증/감을 트랜젝션으로 처리하여 동시성 안전
        // 동시에 건드려도 값이 꼬이지 않도록 처리
        // public async UniTask<(bool committed, CurrencyData latest)> RunCurrencyTransactionAsync(
        //     string uid,
        //     Func<CurrencyData, (bool ok, CurrencyData next)> mutator)
        // {
        //     // 결과를 담을 로컬 변수 선언 및 초기화
        //     bool committed = false;
        //     CurrencyData latest = null;
        //
        //     // 트랜젝션 실행(인자로 받은 람다를 서버가 현재 값을 넣어서 호출한다. 실패시 자동 재시도한다)
        //     // CurrencyRef(uid) : 트랜잭션 대상 노드
        //     var result = await CurrencyRef(uid).RunTransaction(
        //         mutable =>
        //         {
        //             // 1) 현재 서버 값(MutableData - 트랜젝션 대상 노드의 현재 값) -> 모델로 파싱
        //             var dict = mutable.Value as Dictionary<string, object>;
        //             var cur = new CurrencyData
        //             {
        //                 currency1 =
        //                     dict != null && dict.TryGetValue("currency1", out var v1) ? Convert.ToInt32(v1) : 0,
        //                 currency2 =
        //                     dict != null && dict.TryGetValue("currency2", out var v2) ? Convert.ToInt32(v2) : 0,
        //                 currency3 = dict != null && dict.TryGetValue("currency3", out var v3)
        //                     ? Convert.ToInt32(v3)
        //                     : 0,
        //                 updatedAt = 0
        //             };
        //
        //             // 증감/검증 처리
        //             var (ok, next) = mutator(cur);
        //             if (!ok) return TransactionResult.Abort();
        //
        //             // 성공이면 새 값 + 서버시간으로 교체
        //             var newDict = new Dictionary<string, object>
        //             {
        //                 ["currency1"] = next.currency1,
        //                 ["currency2"] = next.currency2,
        //                 ["currency3"] = next.currency3,
        //                 ["updatedAt"] = ServerValue.Timestamp
        //             };
        //             mutable.Value = newDict;
        //             return TransactionResult.Success(mutable);
        //         });
        //     committed = result.Committed;
        //     if (result.Snapshot != null) {
        //         // 서버가 확정한 최종값을 다시 읽어 모델로
        //         latest = new CurrencyData {
        //             currency1 = Convert.ToInt32(result.Snapshot.Child("currency1").Value ?? 0),
        //             currency2 = Convert.ToInt32(result.Snapshot.Child("currency2").Value ?? 0),
        //             currency3 = Convert.ToInt32(result.Snapshot.Child("currency3").Value ?? 0),
        //             updatedAt = Convert.ToInt64(result.Snapshot.Child("updatedAt").Value ?? 0)
        //         };
        // }

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

        private HashSet<string> ParseToHashSet(DataSnapshot mapSnap)
        {
            var hashSet = new HashSet<string>();
            if (!mapSnap.Exists) return hashSet;
            foreach (var child in mapSnap.Children)
            {
                Debug.Log($"[UserDataRepository] {child.Key} - {child.Value}");
                if (Convert.ToBoolean(child.Value)) hashSet.Add(child.Key);
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

        private static long ReadTime(object serverTime)
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