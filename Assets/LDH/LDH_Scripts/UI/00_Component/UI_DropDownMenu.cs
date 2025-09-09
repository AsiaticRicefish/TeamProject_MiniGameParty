using Cysharp.Threading.Tasks;
using Managers;
using UnityEngine;

namespace LDH_UI
{
    public class UI_DropDownMenu : MonoBehaviour
    {
        public void ShowPopupUI(UI_Popup uiPopup)
        {
            var popup = Manager.UI.CreatePopupUI<UI_Popup>(uiPopup.name);
            Manager.UI.ShowPopupUI(popup).Forget();
        }
    }
}