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

        [SerializeField] private bool isOnOnStart;
        
        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }

        private void Start()
        {
            Subscribe();
            if (isOnOnStart) _toggle.isOn = true;
            Debug.Log(_toggle.isOn);
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
            myVcam.VCam.Priority = isOn ? myVcam.FocusPriority : myVcam.OffPriority;
            Debug.Log($"{myVcam.cameraID} isOn : {isOn} /  priority : {myVcam.VCam.Priority}");
        }
        
    }
}