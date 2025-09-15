using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace LDH_Util
{
    public class InputLockController : MonoBehaviour
    {
        private static InputLockController _instance;
        public static InputLockController Instance => _instance;
        
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule uiModule;
        [SerializeField] private PhysicsRaycaster physicsRaycaster;

        
        private void Awake()
        {
            if(_instance==null)
                _instance = this;
            else
                Destroy(this);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }


        public void Lock()
        {
            if (uiModule) uiModule.enabled = false;
            else if (eventSystem) eventSystem.enabled = false;

            if (physicsRaycaster) physicsRaycaster.enabled = false;
        }

        public void Unlock()
        {
            if (uiModule) uiModule.enabled = true;
            else if (eventSystem) eventSystem.enabled = true;

            if (physicsRaycaster) physicsRaycaster.enabled = true;
        }
        

    }
}