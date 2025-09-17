using Firebase.Firestore;
using LDH_Util;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// firestore 구조
    /// items/{typeDoc}
    ///  |----- entries/{itemId}
    /// </summary>
    public class ItemData
    {
        public string Id { get; }
        public Define_LDH.ItemType Type { get; }
        public string Name { get; }
        public Define_LDH.CurrencyType CurrencyType { get; }
        public long Price { get; }
        public bool Enabled { get; }

        public ItemData(string id, Define_LDH.ItemType type, string name, Define_LDH.CurrencyType currencyType,
            long price, bool enabled)
        {
            Id = id;
            Type = type;
            Name = name ?? string.Empty;
            CurrencyType = currencyType;
            Price = price;
            Enabled = enabled;
        }

        public static ItemData From(DocumentSnapshot doc, Define_LDH.ItemType type)
        {
            //아이디
            doc.TryGetValue<string>("id", out string id);
            
            //이름
            doc.TryGetValue<string>("name", out string name);
            
            
            // currencyType
            Define_LDH.CurrencyType currencyType = default;
            if (doc.TryGetValue<string>("currencyType", out string curStr))
            {
                if (!System.Enum.TryParse(curStr, true, out currencyType))
                    currencyType = default;
            }
            else if (doc.TryGetValue<long>("currencyType", out var curNum))
            {
                currencyType = (Define_LDH.CurrencyType)curNum;
            }
            
            // price
            long price = 0;
            if (doc.TryGetValue<long>("price", out var pL)) price = pL;
            else if (doc.TryGetValue<double>("price", out var pD)) price = (long)pD;

            //enabeld
            bool enabled = false;
            doc.TryGetValue<bool>("enabled", out enabled);
            

            // long updatedMs = 0;
            // if (d.TryGetValue("updatedAt", out var u))
            // {
            //     if (u is Timestamp ts) updatedMs = ts.Seconds * 1000L;
            //     else if (u is long ul) updatedMs = ul;
            // }

            return new ItemData(id, type, name, currencyType, price, enabled);
        }


        public static ItemData From(DocumentSnapshot doc)
        {
            Define_LDH.ItemType type = default;
            if (doc.TryGetValue<string>("type", out var typeStr))
            {
                System.Enum.TryParse(typeStr, true, out type);
            }
            else if (doc.TryGetValue<long>("type", out var typeNum))
            {
                var tmp = (Define_LDH.ItemType)typeNum;
                type = System.Enum.IsDefined(typeof(Define_LDH.ItemType), tmp) ? tmp : default;
            }

            return From(doc, type);
        }
    }
}