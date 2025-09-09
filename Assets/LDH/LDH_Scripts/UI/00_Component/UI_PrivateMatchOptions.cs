using System;
using Cysharp.Threading.Tasks;
using LDH_Util;
using Managers;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vector3 = System.Numerics.Vector3;

namespace LDH_UI
{
    public class UI_PrivateMatchOptions : MonoBehaviour
    {
        [Header("Component UI")] 
        [SerializeField]
        private Toggle privateMatchToggle;

        [SerializeField] private Transform optionSpawnRectTransform;
        
        
        
        [SerializeField] private GameObject optionObj;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private TMP_InputField roomCodeInputField;

        private RectTransform _optionRect;
        public Toggle PrivateMatchToggle => privateMatchToggle;
        

        private void Awake() => Init();
        private void OnDestroy() => Unsubscribe();

        private void Init()
        {
            _optionRect = optionObj.GetComponent<RectTransform>();
            
            //이벤트 구독 처리
            Subscribe();

            //초기 설정
            privateMatchToggle.isOn = false;
            optionObj?.SetActive(false);
        }


        #region Event Subscribe / Unsubscribe

        private void Subscribe()
        {
            // 토글
            privateMatchToggle?.onValueChanged.AddListener(ActivePrivateMatchOption);

            if (MatchController.Instance != null)
            {
                //방 코드 입력
                roomCodeInputField.onEndEdit.AddListener(MatchController.Instance.PrivateMatch.RequestJoinPrivateRoom);

                //방 생성 버튼
#if TEST_PLAYER_COUNT
                createRoomButton.onClick.AddListener(()=>
                {
                    MatchController.Instance.ShowPlayerCount(Define_LDH.MatchType.Private);
                });
#else
                createRoomButton.onClick.AddListener(MatchController.Instance.PrivateMatch.RequestCreatePrivateRoom);
#endif
            }
        }

        private void Unsubscribe()
        {
            privateMatchToggle?.onValueChanged.RemoveListener(ActivePrivateMatchOption);
            createRoomButton.onClick.RemoveAllListeners();

            roomCodeInputField.onEndEdit.RemoveAllListeners();
        }

        #endregion


        private void ActivePrivateMatchOption(bool isOn)
        {
            if (!isOn) ClearUI();

            _optionRect.position = optionSpawnRectTransform.position;
            
            optionObj.SetActive(isOn);
        }

        private void ClearUI()
        {
            roomCodeInputField.text = "";
        }
    }
}