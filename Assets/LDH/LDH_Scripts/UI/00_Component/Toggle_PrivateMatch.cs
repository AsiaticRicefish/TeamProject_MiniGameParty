using System;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class Toggle_PrivateMatch : MonoBehaviour
    {
        [Header("Component UI")]
        [SerializeField] private GameObject _optionObj;
        
        private Toggle _toggle;
        

        private void Awake() => Init();
        private void OnDestroy() => Unsubscribe();

        private void Init()
        {
            //참조
            _toggle = GetComponent<Toggle>();
            
            //이벤트 구독 처리
            Subscribe();
            
            //초기 설정
            _toggle.isOn = false;
            _optionObj?.SetActive(false);
        }


        #region Event Subscribe / Unsubscribe

        private void Subscribe()
        {
            _toggle?.onValueChanged.AddListener(ActivePrivateMatchOption);
        }

        private void Unsubscribe()
        {
            _toggle?.onValueChanged.RemoveListener(ActivePrivateMatchOption);
        }

        #endregion


        private void ActivePrivateMatchOption(bool isOn)
        {
            _optionObj.SetActive(isOn);
        }
        
    }
}