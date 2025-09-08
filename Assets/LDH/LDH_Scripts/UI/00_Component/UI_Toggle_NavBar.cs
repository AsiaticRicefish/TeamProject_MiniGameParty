using System;
using Cinemachine;
using LDH_Camera;
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

        private void Start() => Subscribe();
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
            myVcam.VCam.Priority = isOn ? myVcam.FocusPriority : myVcam.OffPriority;
            
            Debug.Log($"vcam priority : {myVcam.VCam.Priority}");
        }
        
    }
}