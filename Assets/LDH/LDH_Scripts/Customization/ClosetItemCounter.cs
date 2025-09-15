using System.Linq;
using Data;
using LDH_Util;
using Managers;
using TMPro;
using UnityEngine;

namespace Customization
{
    public class ClosetItemCounter : MonoBehaviour
    {
        [SerializeField] private TMP_Text countText;
        [SerializeField] private string countFormat = "{0} <#c5c8d0>/ {1}";

        private string _ownedCount;
        private string _totalCount;
        
        public void SetCounter(string id)
        {

            if (id.Equals(Define_LDH.ClosetCategory.Character.ToString()))
            {
                _ownedCount = DataManager.Instance.Custom.OwnedCharacters.Count.ToString();
                _totalCount = CatalogProvider.Characters.Count.ToString();
            }
            else if (id.Equals(Define_LDH.ClosetCategory.Equip.ToString()))
            {
                _ownedCount = DataManager.Instance.Custom.OwnedEquips.Count().ToString();
                _totalCount = CatalogProvider.Equips.Count().ToString();
            }
            else
            {
                _ownedCount = "0";
                _totalCount = "0";
            }
            
            countText.text = string.Format(countFormat, _ownedCount, _totalCount );
        }
        
        
    }
}