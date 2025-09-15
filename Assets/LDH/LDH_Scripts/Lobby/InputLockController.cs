using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace LDH_Util
{
    public class InputLockController : MonoBehaviour
    {
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule uiModule;
        [SerializeField] private PhysicsRaycaster physicsRaycaster;

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