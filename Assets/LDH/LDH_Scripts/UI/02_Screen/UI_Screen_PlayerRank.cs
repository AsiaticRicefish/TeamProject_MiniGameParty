using System.Collections.Generic;
using Managers;
using ShootingScene;
using TMPro;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Screen_PlayerRank : UI_Screen
    {
        [SerializeField] private List<TMP_Text> playerNameTextList;

        protected override void Init()
        {
            ClearRank();
        }


        /// <summary>
        /// 전체 랭킹 업데이트
        /// </summary>
        /// <param name="nicknames"></param>
        public void UpdateRanks(string[] nicknames)
        {
            for (int i = 0; i < nicknames.Length; i++)
            {
                SetRankName(i, nicknames[i]);
            }
        }
        
        
        /// <summary>
        /// rank : 0부터 시작
        /// </summary>
        /// <param name="rank"></param>
        /// <param name="playerNickname"></param>
        public void SetRankName(int rank, string playerNickname)
        {
            if (!LDH_Util.Util_LDH.IsValidIndex<TMP_Text>(rank, playerNameTextList))
            {
                Debug.LogError("인덱스 out of range!");
            }
            playerNameTextList[rank].text = playerNickname;
        }


        private void ClearRank()
        {
            foreach (TMP_Text tmpText in playerNameTextList)
            {
                tmpText.text = "";
            }
        }
        
    }
}