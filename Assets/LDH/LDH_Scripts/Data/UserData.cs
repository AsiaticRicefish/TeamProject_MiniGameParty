using UnityEngine;

namespace Data
{
    public class UserData
    {
        public CustomizationData customization;
        public CurrencyData     currency;


        public UserData()
        {
            customization = null;
            customization = null;
        }

        public UserData(CustomizationData customization, CurrencyData currency)
        {
            this.customization = customization;
            this.currency = currency;
        }
    }
    
    
}