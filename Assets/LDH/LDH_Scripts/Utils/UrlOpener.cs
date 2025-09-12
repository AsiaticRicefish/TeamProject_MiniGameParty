using UnityEngine;

namespace LDH_Util
{
    public static class UrlOpener
    {
        private static UrlConfig _config;
        private const string urlConfigPath = "Data/UrlConfig";

        private static UrlConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = Resources.Load<UrlConfig>(urlConfigPath);
                }

                return _config;
            }
        }
        
        /// <summary>
        /// url 직접 열기
        /// </summary>
        /// <param name="url"></param>
        public static void Open(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.Log("[UrlOpener] url is empty string. 빈 url은 열 수 없습니다.");
                return;
            }
            
            //url 열기
            Application.OpenURL(url);
        }

        /// <summary>
        /// 키 기반 url 열기
        /// </summary>
        /// <param name="key"></param>
        public static void OpenByKey(string key)
        {
            string url = _config?.Get(key);

            if (string.IsNullOrWhiteSpace(url))
            {
                // 폴백: 상수 테이블 매핑
                url = key switch
                {
                    "terms"   => Define_LDH.Urls.Terms,
                    "privacy" => Define_LDH.Urls.Privacy,
                    "support" => Define_LDH.Urls.Support,
                    _         => null
                };
            }
            
            Open(url);
        }
    }
}