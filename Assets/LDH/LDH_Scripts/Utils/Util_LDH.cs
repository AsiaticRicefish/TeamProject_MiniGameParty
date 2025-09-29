using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Customization;
using Cysharp.Threading.Tasks;
using LDH_UI;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;


namespace LDH_Util
{
    public static class Util_LDH
    {

        #region Component

        /// <summary>
        /// GameObject에 해당 컴포넌트가 있으면 반환, 없으면 새로 추가해서 반환
        /// </summary>
        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out T component))
                return component;
            return go.AddComponent<T>();
        }

        public static Component GetOrAddComponent(GameObject go, Type type)
        {
            if (go.TryGetComponent(type, out var component))
                return component;

            return go.AddComponent(type);
        }

        #endregion

        #region Validation

        ///<summary>
        /// index가 리스트/배열 등 순차 컬렉션 내에서 유효한 범위인지 확인
        /// </summary>
        /// <param name="index">검사할 인덱스</param>
        /// <param name="list">인덱스를 가진 컬렉션 (IList)</param>
        /// <returns>list가 null이 아니고, index가 0 이상이고 Count 미만이면 true</returns>
        public static bool IsValidIndex<T>(int index, IList<T> list)
        {
            return list != null && index >= 0 && index < list.Count;
        }

        #endregion

        #region Resource

        public static T Instantiate<T>(string prefabPath, Transform parent = null) where T : UnityEngine.Object
        {
            T prefab = Resources.Load<T>(prefabPath);
            if (prefabPath == null)
            {
                Debug.Log($"[Util_LDH] 프리팹을 가져올 수 없습니다. : {prefabPath}");
                return null;
            }
            
            T go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;

            return go;
        }

        public static T Instantiate<T>(T prefab, Transform parent = null) where T : UnityEngine.Object
        {
            if (prefab == null) return null;
            
            T go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            
            return go;
        }
        
        

        #endregion


        #region Util

        // <summary>
        /// 초 단위 시간을 입력받아 HH:MM 형식의 문자열로 변환하여 반환한다.
        /// </summary>
        /// <param name="seconds">변환할 시간(초 단위)</param>
        /// <returns>HH : MM 형식 문자열</returns>
        public static string FormatTimeMS(float seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Minutes} : {ts.Seconds:D2}";
        }
        
        public static string Generate4DigitString()
        {
            int n = RandomNumberGenerator.GetInt32(0, 10000); // 0~9999 균등
            return n.ToString("D4"); // 0000 허용
        }

        /// <summary>
        /// [min, max) 범위 내의 랜덤한 정수 반환
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int GetRandomInt(int min, int max)
        {
            int n = RandomNumberGenerator.GetInt32(min, max);
            
            return n;
        }
        

        public static void ConsoleLog<T>(T type, string message) where T : MonoBehaviour
        {
            Debug.Log($"[{type.GetType().Name}] {message}");
        }
        
        public static void ConsoleLogWarning<T>(T type, string message) where T : MonoBehaviour
        {
            Debug.LogWarning($"[{type.GetType().Name}] {message}");
        }

        
        
        //모든 자식 파괴
        public static void RemoveAllChildren(Transform parent)
        {
            int childCount = parent.childCount;
            if (childCount== 0)
            {
                Debug.Log("[Util] 파괴할 자식이 없습니다.");
                return;
            }
            
            foreach (Transform child in parent)
            {
//                Debug.Log($"[Util] {child.gameObject.name}를 파괴합니다.");
                Object.Destroy(child.gameObject);
            }
            
//            Debug.Log($"[Util] {childCount}개의 자식을 파괴했습니다.");
        }

        public static long[] SumByCurrencyType(IEnumerable<(Define_LDH.CurrencyType type, long price)> priceInfo)
        {
            int currencyCount = Enum.GetValues(typeof(Define_LDH.CurrencyType)).Length;

            var totals = new long[currencyCount];
            foreach (var (t, p) in priceInfo)
            {
                if (p <= 0) continue;
                int index = (int)t;
                long next = totals[index] + p;
                if (next < totals[index]) next = long.MaxValue;      // overflow guard
                totals[index] = next;
            }

            for(int i=0; i<totals.Length; i++)
            {
                Debug.Log($"<color=green> currency type {i} total price : {totals[i]}</color>");
            }
            return totals;
        }

        public static long SafeAdd(long a, long b, long clampMax)
        {
            try
            {
                checked
                {
                    long s = a + b;
                    if (s > clampMax) return clampMax;
                    return s;
                }
            }
            catch (OverflowException)
            {
                return clampMax;
            }
        }

        public static string GetRankFormat(int rank)
        {
            return rank switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => rank.ToString().Trim() + "th"
            };
        }
        
        public static Dictionary<string, int> CalcTotalRank(Dictionary<string, int> totalScores)
        {
            Dictionary<string, int> totalRank = new(totalScores.Count);

            int i = 0; // 현재 인덱스
            int rank = 0;
            int prevScore = int.MinValue;

            // 전체 점수를 내림차순 정렬
            foreach (var kvPair in totalScores.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
            {
                i++; // 현재 인덱스(1부터 계산)
                if (kvPair.Value != prevScore)
                {
                    // 이전 점수와 다른 점수 = 현재 인덱스가 새로운 등수
                    rank = i;
                    prevScore = kvPair.Value;
                }

                totalRank[kvPair.Key] = rank;
            }

            return totalRank;
        }
        

        #endregion
        
        #region RectTransform Control

        /// <summary>
        /// 주어진 RectTransform의 anchor, pivot, anchoredPosition, sizeDelta를 설정합니다.
        /// basePosition은 anchoredPosition의 기준 위치이며, offset이 있다면 추가됩니다.
        /// </summary>
        /// <param name="rect">대상 RectTransform</param>
        /// <param name="anchorMin">Anchor Min 값</param>
        /// <param name="anchorMax">Anchor Max 값</param>
        /// <param name="pivot">Pivot 기준</param>
        /// <param name="basePosition">기준 위치 (anchoredPosition)</param>
        /// <param name="sizeDelta">UI 크기 (width, height). 생략 시 기존 값 유지</param>
        /// <param name="offset">basePosition에 추가로 더해질 오프셋</param>
        public static void SetRectTransform(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 basePosition,
            Vector2? sizeDelta = null,
            Vector2? offset = null
        )
        {
            if (rect == null) return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;

            // 위치 = 기준 위치 + 오프셋
            rect.anchoredPosition = basePosition + (offset ?? Vector2.zero);

            if (sizeDelta.HasValue)
                rect.sizeDelta = sizeDelta.Value;
        }

        /// <summary>
        /// 부모 영역 전체를 가득 채우는 Full Stretch UI로 설정합니다.
        /// (anchorMin = (0,0), anchorMax = (1,1), pivot = center)
        /// sizeDelta는 (0,0)으로 설정됩니다.
        /// </summary>
        /// <param name="rect">대상 RectTransform</param>
        /// <param name="offset">anchoredPosition에 적용할 오프셋</param>
        public static void SetFullStretch(RectTransform rect, Vector2? offset = null)
        {
            SetRectTransform(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                offset);
        }

        /// <summary>
        /// 화면 정중앙 기준으로 위치시키며, 명시한 크기로 설정합니다.
        /// </summary>
        /// <param name="rect">대상 RectTransform</param>
        /// <param name="size">UI의 크기 (width, height)</param>
        /// <param name="offset">기준 위치에서의 오프셋</param>
        public static void SetCenter(RectTransform rect, Vector2 size, Vector2? offset = null)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            SetRectTransform(rect, center, center, center, Vector2.zero, size, offset);
        }


        /// <summary>
        /// 오른쪽 위 모서리를 기준으로 UI를 배치합니다.
        /// </summary>
        /// <param name="rect">대상 RectTransform</param>
        /// <param name="size">UI의 크기 (width, height)</param>
        /// <param name="offset">오른쪽 위 기준 위치에서의 오프셋</param>
        public static void SetRightTop(RectTransform rect, Vector2 size, Vector2? offset = null)
        {
            Vector2 pos = new Vector2(1f, 1f);
            SetRectTransform(rect, pos, pos, pos, Vector2.zero, size, offset);
        }

        /// <summary>
        /// 오른쪽 아래 모서리를 기준으로 UI를 배치합니다.
        /// </summary>
        /// <param name="rect">대상 RectTransform</param>
        /// <param name="size">UI의 크기 (width, height)</param>
        /// <param name="offset">오른쪽 아래 기준 위치에서의 오프셋</param>
        public static void SetRightBottom(RectTransform rect, Vector2 size, Vector2? offset = null)
        {
            Vector2 pos = new Vector2(1f, 0f);
            SetRectTransform(rect, pos, pos, pos, Vector2.zero, size, offset);
        }


        /// <summary>
        /// 왼쪽 위 모서리를 기준으로 UI를 배치합니다.
        /// </summary>
        /// <param name="rect">대상 RectTransform</param>
        /// <param name="size">UI의 크기 (width, height)</param>
        /// <param name="offset">왼쪽 위 기준 위치에서의 오프셋</param>
        public static void SetLeftTop(RectTransform rect, Vector2 size, Vector2? offset = null)
        {
            Vector2 pos = new Vector2(0f, 1f);
            SetRectTransform(rect, pos, pos, pos, Vector2.zero, size, offset);
        }


        /// <summary>
        /// 왼쪽 아래 모서리를 기준으로 UI를 배치합니다.
        /// </summary>
        /// <param name="rect">대상 RectTransform</param>
        /// <param name="size">UI의 크기 (width, height)</param>
        /// <param name="offset">왼쪽 아래 기준 위치에서의 오프셋</param>
        public static void SetLeftBottom(RectTransform rect, Vector2 size, Vector2? offset = null)
        {
            Vector2 pos = new Vector2(0f, 0f);
            SetRectTransform(rect, pos, pos, pos, Vector2.zero, size, offset);
        }


        public static void SetCenterBottom(RectTransform rect, Vector2 size, Vector2? offset = null)
        {
            Vector2 pos = new Vector2(0.5f, 0f);
            SetRectTransform(rect, pos, pos, pos, Vector2.zero, size, offset);
        }
        
        public static void SetCenterTop(RectTransform rect, Vector2 size, Vector2? offset = null)
        {
            Vector2 pos = new Vector2(0.5f, 1f);
            SetRectTransform(rect, pos, pos, pos, Vector2.zero, size, offset);
        }

        #endregion

        #region Load Scene

        /// <summary>
        /// 비동기 씬전환
        /// </summary>
        public static IEnumerator LoadSceneWithDelay(string sceneName, float delay, bool showProgress = false)
        {
            var loadingUI = Manager.UI.PeekPopupUI<UI_Loading>();
            // Debug.Log($"loding ui? {loadingUI == null}");
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            
            while (op.progress < 0.9f)
            {
                float p = Mathf.Clamp01(op.progress / 0.9f); // 0.0 ~ 1.0 스케일
                if (showProgress) loadingUI?.SetProgress(p);
               
                yield return null;
            }
            
            // 0.9f에서 대기 중일 때 100% 표시
            if (showProgress) loadingUI?.SetProgress(1f);
            
            // 씬 활성화
            op.allowSceneActivation = true;

            // 실제 완료까지 대기
            while (!op.isDone)
                yield return null;
            
            // 모달 보여지는 시간 확보
            yield return new WaitForSeconds(delay);
            
            loadingUI?.RequestClose(); // 혹은 Manager.UI.ClosePopup(loadingUI)
   
        }


        #endregion

        #region Network


        public static void ClearAllPlayerProperty()
        {
            var customProperties = PhotonNetwork.LocalPlayer.CustomProperties;

            var clearProperties = new ExitGames.Client.Photon.Hashtable();

            foreach (var key in customProperties.Keys)
            {
                clearProperties[key] = null;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(clearProperties);
        }
        
        public static UnimoCombo GetCurrentRoomPlayerUnimoCombo(string uid)
        {
            if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)                 
                return new UnimoCombo(Define_LDH.DefaultData.DefaultCharacter, Define_LDH.DefaultData.DefaultEquip);

            Player player = null;
            foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (p.CustomProperties.TryGetValue(
                        Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.Uid),
                        out object value) && value is string playerUid && playerUid.Equals(uid))
                {
                    player = p;
                    break;
                }
            }
            if (player == null)
            {
                Debug.LogWarning("player is null. Return Default Character ID");
                return new UnimoCombo(Define_LDH.DefaultData.DefaultCharacter, Define_LDH.DefaultData.DefaultEquip);
            }
            
            string charId = player
                .CustomProperties[
                    Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.CharacterId)]
                .ToString();
            string equipId =  player
                .CustomProperties[
                    Define_LDH.PlayerProps.GetPlayerInfoKey(Define_LDH.PlayerProps.PlayerInfoKey.EquipId)]
                .ToString();

            return new UnimoCombo(charId, equipId);
        }

        #endregion
    }
}