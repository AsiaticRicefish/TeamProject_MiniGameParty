using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Firebase.Database;
using UnityEngine;

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


        public async UniTask<UserData> LoadOrCreateAsync(string uid)
        {
            var userSnap = await UserRef(uid).GetValueAsync();

            if (!userSnap.Exists)
            {
                //------ 서버에 저장된 데이터가 없는 상황 -----//
                var customData = CustomizationData.CreateDefault();
                var currencyData = CurrencyData.CreateDefault();

                var customTask = SaveCustomizationAsync(uid, customData);
                var currencyTask = SaveCurrencyAsync(uid, currencyData);
                await UniTask.WhenAll(customTask, currencyTask);

                // 서버타임 포함해 다시 읽어오기
                userSnap = await UserRef(uid).GetValueAsync();
            }
            
            return ParseUserData(userSnap);
        }


        #region Save Logic

        public async UniTask SaveCustomizationAsync(string uid, CustomizationData c)
        {
            var map = new Dictionary<string, object>
            {
                ["characterId"]     = c.characterId,
                ["equipId"]         = c.equipId,
                ["ownedCharacters"] = HashToBoolMap(c.ownedCharacters),
                ["ownedEquips"]     = HashToBoolMap(c.ownedEquips),
                ["updatedAt"]     = ServerValue.Timestamp
            };
            await CustRef(uid).UpdateChildrenAsync(map);
        }

        public async UniTask SaveCurrencyAsync(string uid, CurrencyData c)
        {
            var map = new Dictionary<string, object>
            {
                ["currency1"] = c.currency1,
                ["currency2"] = c.currency2,
                ["currency3"] = c.currency3,
                ["updatedAt"]     = ServerValue.Timestamp
            };
            await CustRef(uid).UpdateChildrenAsync(map);
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
            c.updatedAt = ReadTime(customSnap.Child("updateAt").Value);

            return c;
        }
        
        private CurrencyData ParseCurrencyData(DataSnapshot currencySnap)
        {
            var c = new CurrencyData();
            c.currency1 = (int)currencySnap.Child("currency1").Value;
            c.currency2 = (int)currencySnap.Child("currency2").Value;
            c.currency3 = (int)currencySnap.Child("currency3").Value;
            c.updatedAt = ReadTime(currencySnap.Child("updateAt").Value);
            return c;
        }

        private HashSet<string> ParseToHashSet(DataSnapshot mapSnap)
        {
            var hashSet = new HashSet<string>();
            if (!mapSnap.Exists) return hashSet;
            foreach (var child in mapSnap.Children)
            {
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