using System.Collections.Generic;
using UnityEngine;

namespace LDH_Util
{
    [CreateAssetMenu(fileName = "UrlConfig", menuName = "Config/URL Config", order = 0)]
    public class UrlConfig : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string key;
            public string url;
        }
        public List<Entry> entries = new();

        public string Get(string key)
        {
            var entry = entries.Find(x => x.key == key);
            
            return string.IsNullOrEmpty(entry.url) ? null : entry.url;

        }
    }
}