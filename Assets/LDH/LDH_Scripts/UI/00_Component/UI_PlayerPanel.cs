using System;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace LDH_UI
{
    /// <summary>
    /// 플레이어 패널 UI 관리
    /// - 클릭 이벤트만 외부에 알림 (ProfileClicked, ReadyClicked)
    /// - 외부에서 상태(점유/인터랙션/비주얼)를 세팅
    /// Photon/권한 판정/커스텀 프로퍼티 변경 x
    /// </summary>
    public class UI_PlayerPanel : MonoBehaviour
    {
        [Header("UI Component")] [SerializeField]
        private Button inviteButton; // 프로필 버튼

        [SerializeField] private Image profileImage; // 프로필 이미지지
        [SerializeField] private Button readyButton; // 준비 버튼
        [SerializeField] private TextMeshProUGUI readyText;


        [Header("Styles")] [SerializeField] private UI_ButtonStateStyle readyTheme;
        [SerializeField] private UI_ButtonStateStyle notReadyTheme;

        public int SlotIndex { get; private set; } = -1;
        public bool IsOccupied { get; private set; }

        public event Action<int> InviteButtonClicked;
        public event Action<int> ReadyClicked;

        

        /// <summary>
        /// 초기화 : 슬롯 인덱스 지정, 클릭 이벤트 바인딩
        /// </summary>
        /// <param name="slotIndex"></param>
        /// <param name="canInvite"></param>
        public void Setup(int slotIndex) // 마스만 권한 있음
        {
            SlotIndex = slotIndex;

            // 클릭 이벤트를 외부로 전달하기 위한 구독 처리
            inviteButton.onClick.RemoveAllListeners();
            inviteButton.onClick.AddListener(() => InviteButtonClicked?.Invoke(SlotIndex));

            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(() => ReadyClicked?.Invoke(SlotIndex));
        }

        public void SetEmpty(bool canInvite)
        {
            SetOccupied(false); // 빈 슬롯으로 처리
            
            SetReadyButtonInteractable(false);
            SetReadyVisual(false);
            SetInviteActive(canInvite);

        }

        public void ApplyPlayer(bool isReady, bool isLocalPlayer)
        {
            SetOccupied(true);
            SetProfileImage();
            
            SetReadyButtonInteractable(isLocalPlayer);
            SetReadyVisual(isReady);
            
            SetInviteActive(false);
        }
        

        #region UI Control

        /// <summary>
        /// 슬롯 점유 / 비점유 상태에 따라 UI 반영 (프로필 이미지 표시 여부, 초대 기능 활성화 여부)
        /// </summary>
        /// <param name="occupied"></param>
        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            profileImage.enabled = occupied;
            inviteButton.image.color = occupied ? Color.white : readyTheme.backgroundColor;
        }

        public void SetProfileImage()
        {
            //todo: 프로필 이미지 설정
        }

        /// <summary>
        /// Ready 버튼의 비주얼(텍스트/색)을 상태에 맞게 반영
        /// </summary>
        /// <param name="isReady"></param>
        public void SetReadyVisual(bool isReady)
        {
            readyText.text = isReady ? readyTheme.label : notReadyTheme.label;
            readyText.color = isReady ? readyTheme.labelColor : notReadyTheme.labelColor;
            readyButton.image.color = isReady ? readyTheme.backgroundColor : notReadyTheme.backgroundColor;
        }


        /// <summary>
        /// Ready 버튼 Interaction 제어 (local player만 제어 가능하도록 설정해야 함)
        /// </summary>
        /// <param name="interactable"></param>
        public void SetReadyButtonInteractable(bool interactable)
        {
            readyButton.interactable = interactable;
        }


        /// <summary>
        /// 프로필 버튼 인터랙션 제어(마스터만 true로 세팅하기)
        /// </summary>
        public void SetInviteActive(bool active)
        {
            inviteButton.interactable = active;
        }


        /// <summary>
        /// 게임 시작 직전 전체 잠금 등에 사용(현재 상태를 반영해서 && 로 처리)
        /// </summary>
        public void SetInteractableAll(bool interactable)
        {
            readyButton.interactable = interactable && readyButton.interactable;
            inviteButton.interactable = interactable && inviteButton.interactable;
        }

        #endregion
        
    }
}