using System;
using LDH_Util;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Button_LinkOpen : MonoBehaviour
    {
        [SerializeField] private string key;
        
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void Start()
        {
            _button.onClick.AddListener(OpenUrl);
        }

        private void OpenUrl()
        {
            UrlOpener.OpenByKey(key);
        }
    }
}