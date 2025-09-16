using System.Collections.Generic;
using Managers;
using ShootingScene;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

namespace LDH_UI
{
    public class UI_Screen_PlayerRank : UI_Screen
    {
        [SerializeField] private List<TMP_Text> playerNameTextList;
        [SerializeField] private List<Image> playerProfileImageList;
        [SerializeField] public JengaRankingUIAnimated rankingUI;

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

        /// <summary>
        /// 나간유저가 Photon의 OnPlayerLeftRoom에 콜백받아서 호출 되었을 때
        /// </summary>

        // 1. 일단 나가유저의 leftUserName의 인덱스 정보를 알아온다.
        // 2. 나간유저가
        public void LeftUserSetRank(string leftUserName)
        {
            int totalSlots = playerNameTextList.Count;
            int currentPlayerCount = PhotonNetwork.CurrentRoom.PlayerCount;

            // 1. 나간 유저 인덱스 찾기
            int leftIndex = -1;
            for (int i = 0; i < totalSlots; i++)
            {
                if (playerNameTextList[i].text == leftUserName)
                {
                    leftIndex = i;
                    break;
                }
            }

            Debug.Log($"나간 유저의 슬롯 번호는 {leftIndex} 입니다");

            //1차 필터링 - 랭킹에 나간 플레이어가 존재 하지 않을 때
            if (leftIndex == -1)
            {
                Debug.Log("나간유저가 랭킹에 존재하지 않습니다");
                return;
            }

            //2차 필터링 - 나간 플레이어가 마지막 랭킹이었을 때
            if (leftIndex == currentPlayerCount)
            {
                Debug.Log($"나간 유저가 이미 마지막 순서 입니다");

                //ui를 어둡게 하기
                Color color = Color.black;
                color.a = 0.5f;
                playerProfileImageList[leftIndex].color = color;

                return;
            }

            // 3. 랭킹 중간에 있음. 나간 유저 인덱스 이후 유저들을 한 칸씩 앞으로 당김
            for (int i = leftIndex; i < currentPlayerCount; i++)
            {
                if (i + 1 < totalSlots)
                {
                    Debug.Log($"{playerNameTextList[i + 1].text} 유저의 랭킹변화");
                    playerNameTextList[i].text = playerNameTextList[i + 1].text;
                }
            }

            if (currentPlayerCount < totalSlots)
            {
                //나가 유저를 마지막 슬롯에 넣기
                Debug.Log("나간 유저를 마지막 슬롯에 넣기");
                playerNameTextList[currentPlayerCount].text = leftUserName;

                //UI처리
                Color color = Color.black;
                color.a = 0.5f;
                playerProfileImageList[currentPlayerCount].color = color;
            }
        }

        public void OnClick(TMP_Text text)
        {
            Debug.Log(text.text);
        }

       
        private void ClearRank()
        {
            foreach (TMP_Text tmpText in playerNameTextList)
            {
                tmpText.text = "";
            }
        }

        //시작할 때 해당 UI를 키게 해야하는데
        private void ShowRanking(Dictionary<string, int> rankings)
        {
            
        }

        public void UpdateLiveRanks(Dictionary<string, int> ranks)
        {
            rankingUI.UpdateLiveRanks(ranks);
        }
    }
}