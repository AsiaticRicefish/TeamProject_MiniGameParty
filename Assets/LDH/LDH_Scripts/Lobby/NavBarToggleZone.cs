using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LDH_Lobby
{
    [RequireComponent(typeof(Collider))]
    public class NavBarToggleZone : MonoBehaviour,  IPointerClickHandler
    {
        [SerializeField] private Toggle targetToggle;
        [SerializeField] private SFX_UI clickSfx = SFX_UI.SFX_Btn1;
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!targetToggle) return;
            if (!targetToggle.isOn)
            {
                SoundManager.Instance.PlaySFX_UI(clickSfx);
                targetToggle.isOn = true;
            }
        }
    }
}