using System;
using System.Collections.Generic;
using LDH_Util;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName = "Currency", fileName = "CurrencyCatalog")]

    public class CurrencyCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public Define_LDH.CurrencyType type;
            public string displayName;   // "골드", "보석" 등 (로컬라이즈 키로 써도 OK)
            public Sprite icon;
            public int sortOrder = 0;    // UI에서 표시 순서
            public string numberFormat = "N0"; // 1,234 처럼
        }
        
        [SerializeField] private Entry[] entries; // 인스펙터에서 채움
        private Entry[] _byType;

        public void Init()
        {
            if (_byType != null) return;
            _byType = new Entry[(int)Define_LDH.CurrencyType.Count];
            if (entries == null) return;
            
            foreach (var e in entries)
            {
                if (e == null) continue;
                int idx = (int)e.type;
                if ((uint)idx < (uint)_byType.Length)
                    _byType[idx] = e;
            }
        }
        
        public bool TryGet(Define_LDH.CurrencyType type, out Entry entry)
        {
            if (_byType == null) Init();
            var arr = _byType;
            int idx = (int)type;
            if (arr != null && (uint)idx < (uint)arr.Length && (entry = arr[idx]) != null)
                return true;
            entry = null;
            return false;
        }
        
        public IEnumerable<Entry> AllSorted()
        {
            if (_byType == null) Init();
            // 정렬 정보 그대로 사용
            var list = new List<Entry>(_byType.Length);
            for (int i = 0; i < _byType.Length; i++)
                if (_byType[i] != null) list.Add(_byType[i]);
            list.Sort((a,b) => a.sortOrder.CompareTo(b.sortOrder));
            return list;
        }
    }
}