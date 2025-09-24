using System;
using System.Collections.Generic;
using Firebase.Database;
using LDH_Util;
using UnityEngine;

namespace Data
{
    //사용자 데이터
    [Serializable]
    public class CurrencyData
    {
        public long currency1;
        public long currency2;
        public long currency3;
        public long updatedAt; // 서버 시각(ms)
        
        
        // ---- 생성자 ---- //
        // 기본 생성자
        public CurrencyData()
        {
            currency1 = default;
            currency2 = default;
            currency3 = default;
            updatedAt = 0;
        }
        
        // 딕셔너리 -> 구조에 맞게 파싱 (파이어베이스용)
        public CurrencyData(Dictionary<string, object> dict)
        {
            currency1 =
                dict != null && dict.TryGetValue("currency1", out var v1) ? Convert.ToInt64(v1) : 0;
            currency2 =
                dict != null && dict.TryGetValue("currency2", out var v2) ? Convert.ToInt64(v2) : 0;
            currency3 = dict != null && dict.TryGetValue("currency3", out var v3)
                ? Convert.ToInt64(v3)
                : 0;
            updatedAt = dict != null && dict.TryGetValue("updatedAt", out var time)
                ? RealTimeUserDataRepository.ReadTime(time)
                : 0;
        }

        public CurrencyData(CurrencyData data)
        {
            currency1 = data.currency1;
            currency2 = data.currency2;
            currency3 = data.currency3;
            updatedAt = data.updatedAt;
        }
        
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
        
        
        public long GetCurrencyByType(Define_LDH.CurrencyType currencyType)
        {
            return currencyType switch
            {
                Define_LDH.CurrencyType.Currency1 => currency1,
                Define_LDH.CurrencyType.Currency2 => currency2,
                Define_LDH.CurrencyType.Currency3 => currency3,
                _ => 0
            };
        }
    }
}