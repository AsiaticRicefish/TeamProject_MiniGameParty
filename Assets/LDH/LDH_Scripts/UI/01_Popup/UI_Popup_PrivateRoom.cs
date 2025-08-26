using System.Collections.Generic;
using LDH_Util;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    /// <summary>
    /// 비공개 룸 패널 UI
    /// - 플레이어 패널 4개 컨트롤
    /// - 룸 코드 표시, 나가기 버튼 -> RequestClose() (외부는 OnClosedRequest 구독)
    /// - 패널의 슬롯 인덱스 setup만 처리 (플레이어 정보 바인딩, 이벤트 바인딩, 상태 갱신은 비공개 매칭 컨트롤러가 관리)
    /// </summary>
    public class UI_Popup_PrivateRoom : UI_Popup
    {
        [Header("UI Component")]
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private Button exitButton;
        
        [Header("Player Panel UI List")]
        [Tooltip("UI 배치 순서(좌 -> 우)대로 배열에 추가")]
        [SerializeField] private UI_PlayerPanel[] playerPanels;

        public IReadOnlyList<UI_PlayerPanel> PlayerPanels => playerPanels;   // 외부에서 플레이어 패널 순회 / 조회용
        
        
        // slot indexer
        public UI_PlayerPanel this[int slot] => 
            (playerPanels != null && Util_LDH.IsValidIndex(slot, playerPanels))
            ? playerPanels[slot]
            : null;
        
        
        protected override void Init()
        {
            base.Init();
            
            //외부는 UI_Base의 OnClosedRequested를 구독해서 처리
            exitButton?.onClick.RemoveAllListeners();
            exitButton?.onClick.AddListener(RequestClose);
        }

        #region UI Control API

        /// <summary>
        /// 룸 코드 표시 (컨트롤러에서 호출)
        /// </summary>
        /// <param name="roomCode"></param>
        public void SetRoomCode(string roomCode)
        {
            if(roomCodeText == null) return;
            roomCodeText.text = roomCode;
        }

        
        /// <summary>
        /// 패널 초기화
        /// </summary>
        public void ResetAllSlots(bool canInvite)
        {
            if(playerPanels== null) return;

            foreach (var playerPanel in playerPanels)
            {
                
                for (int i = 0; i < playerPanels.Length; i++)
                {
                    playerPanels[i].Setup(i);
                    playerPanels[i].SetEmpty(canInvite);
                    
                }
            }
        }

        public void SetPlayerPanel(int slotIdx, bool isReady, bool isLocal, bool isMaster)
        {
            Debug.Log(slotIdx);
            Debug.Log(this[slotIdx]==null);
            this[slotIdx].ApplyPlayer(isReady, isLocal, isMaster);
        }
        
        #endregion
        
    
    }
}