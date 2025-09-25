using System.Collections.Generic;
using LDH_Util;
using UnityEngine;

namespace LDH_UI
{
    [CreateAssetMenu(menuName = "UI/Toast Style Table", fileName = "ToastStyleTable")]
    public class ToastStyleTable : ScriptableObject
    {
        [System.Serializable]
        public class ToastStyle
        {
            public Define_LDH.ToastType toastType;
            public Sprite icon;
            public Color backgroundColor;
            public Color textColor;
            public AudioClip sfx;
        }
        
        [SerializeField] private List<ToastStyle> styles;
       
        private Dictionary<Define_LDH.ToastType, ToastStyle> _lookup;

        public void Init()
        {
            if (_lookup != null) return;
            _lookup = new();
            foreach (var style in styles)
                _lookup[style.toastType] = style;
        }

        public ToastStyle GetStyle(Define_LDH.ToastType type)
        {
            Init();
            return _lookup.TryGetValue(type, out var style) ? style : null;
        }


    }
}