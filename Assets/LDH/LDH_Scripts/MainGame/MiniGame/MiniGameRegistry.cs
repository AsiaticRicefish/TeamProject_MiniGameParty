using System;
using System.Collections.Generic;
using System.Linq;
using LDH_Util;
using UnityEngine;

namespace LDH_MainGame
{
    public class MiniGameRegistry : MonoBehaviour
    {
        [SerializeField] private List<MiniGameInfo> miniGameInfos;

        private Dictionary<string, MiniGameInfo> _infoMap;
        public int Count => miniGameInfos?.Count ?? 0;
        public List<MiniGameInfo> MiniGameInfos => miniGameInfos;

        private void Awake()
        {
            _infoMap = miniGameInfos.ToDictionary(info => info.id);
        }

        public MiniGameInfo Get(string id) => _infoMap[id];
        public MiniGameInfo PickRandomGame(Func<MiniGameInfo, bool> filter = null)
        {
            var baseList = miniGameInfos;
            var candidates = (filter == null) ? baseList : baseList.Where(filter).ToList();

            // 필터로 0개가 되었는데 레지스트리에 1개뿐이라면 그 1개 반환
            if (candidates.Count == 0 && baseList.Count == 1)
            {
                if (candidates.Count == 0 && baseList.Count == 1)
                    return baseList[0];
            }

            if (candidates.Count == 0)
            {
                Util_LDH.ConsoleLog(this, "선택 가능한 미니게임 후보가 없습니다.");
                return null;
            }
            
            int idx = Util_LDH.GetRandomInt(0, candidates.Count);

            return candidates[idx];
        }


        #region API

        public string GetSceneName(string id) => Get(id).sceneName;
        public string GetGameName(string id) => Get(id).gameName;
        public string GetDescription(string id) => Get(id).description;
        public Sprite GetSprite(string id) => Get(id).sprite;
        
        #endregion
    }
}