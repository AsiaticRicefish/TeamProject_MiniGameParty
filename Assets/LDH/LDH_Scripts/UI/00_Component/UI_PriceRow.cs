using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_PriceRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amount;

        public void Setup(Sprite s, string text)
        {
            icon.sprite = s;
            amount.text = text;
            gameObject.SetActive(true);
        }
    }
}