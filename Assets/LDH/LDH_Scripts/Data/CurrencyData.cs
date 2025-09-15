using System;
using Firebase.Database;
using LDH_Util;
using UnityEngine;

namespace Data
{
    [Serializable]
    public class CurrencyData
    {
        public long currency1;
        public long currency2;
        public long currency3;
        public long  updatedAt; // 서버 시각(ms)
        
        public static CurrencyData CreateDefault()
        {
            return new CurrencyData()
            {
                currency1 =  Define_LDH.DefaultData.DefaultCurrency1,
                currency2 = Define_LDH.DefaultData.DefaultCurrency2,
                currency3 = Define_LDH.DefaultData.DefaultCurrency3,
                updatedAt = 0 // 저장 시 서버타임으로 채움
            };
        }
    }
}