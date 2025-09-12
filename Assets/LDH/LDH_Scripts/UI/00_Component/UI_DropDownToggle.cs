using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_DropDownToggle : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private UI_DropDownMenu menu; // ShowMenu/HideMenu 제공
        [SerializeField] private Button toggleCloseArea;
        
        
        private bool _busy;

        private void Awake()
        {
            toggle.onValueChanged.AddListener(OnToggle);
        }

        private async void OnToggle(bool isOn)
        {
            if (_busy) return;
            _busy = true;
            toggle.interactable = false;
            try
            {
                if (isOn) await menu.ShowMenu();
                else await menu.HideMenu();
            }
            finally
            {
                toggleCloseArea.gameObject.SetActive(isOn);
                toggle.interactable = true;
                _busy = false;
            }
        }
    }
}