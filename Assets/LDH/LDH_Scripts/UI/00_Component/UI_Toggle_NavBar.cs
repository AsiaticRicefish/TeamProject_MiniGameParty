using System;
using Cinemachine;
using LDH_Camera;
using LDH_Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Toggle_NavBar : MonoBehaviour
    {
        private Toggle _toggle;
        [Header("Virtual Camera")]
        [SerializeField] private VirtualCamera_Lobby myVcam;

        
        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }

        private void Start()
        {
            Subscribe();
        }
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            _toggle.onValueChanged.AddListener(OnValueChanged);
        }


        private void Unsubscribe()
        {
            _toggle.onValueChanged.RemoveAllListeners();
        }


        private void OnValueChanged(bool isOn)
        {
            if (!isOn) return; // 꺼질 때 콜백 무시
         Debug.Log("toggle is one");
            
            LobbyNavigationController.Instance?.RequestFocus(myVcam.cameraID);
        }
        
    }
}