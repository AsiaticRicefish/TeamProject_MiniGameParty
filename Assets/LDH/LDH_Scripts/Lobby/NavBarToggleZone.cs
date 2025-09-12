using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LDH_Lobby
{
    [RequireComponent(typeof(Collider))]
    public class NavBarToggleZone : MonoBehaviour,  IPointerClickHandler
    {
        [SerializeField] private Toggle targetToggle;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!targetToggle) return;
            if (!targetToggle.isOn) targetToggle.isOn = true;
        }
    }
}