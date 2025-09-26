using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LDH_MainGame;
using Managers;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_QuitGame : UI_Popup
    {
        [Header("UI")] [SerializeField] private Button okButton;
        [SerializeField] private TextMeshPro infoMessage;

        [Header("Animation")] [SerializeField] private RectTransform targetRect;
        [SerializeField] private float offsetFromCenter;
        [SerializeField] private Ease easyType;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Canvas Order")] private int forceTopOrder = 1000;

        [Header("Loading UI")] [SerializeField]
        private UI_LoadingTheme uiLoadingTheme;
        
        
        private object _tweenId; // 중복 방지를 위한 캐싱
        private Vector2 _centerPos; // 최종 목적지(중앙) 위치

        private void Awake()
        {
            _tweenId = this;
            if (!targetRect) targetRect = (RectTransform)transform;
            _centerPos = targetRect.anchoredPosition;

            // 구독
            okButton?.onClick.AddListener(QuitGame);
        }


        public void SetData(string infoMsg)
        {
            infoMessage.text = infoMsg;
        }

        protected override async UniTask OnShowAsync(CancellationToken ct)
        {
            GetComponent<Canvas>().sortingOrder += forceTopOrder;

            // 진행 중인 dotween이 있으면 종료
            DOTween.Kill(_tweenId, complete: false);

            // 시작점 위치
            Vector2 startPos = _centerPos + Vector2.left * offsetFromCenter;
            targetRect.anchoredPosition = startPos;

            var tween = targetRect
                .DOAnchorPos(_centerPos, fadeTime)
                .SetEase(easyType)
                .SetUpdate(useUnscaledTime)
                .SetId(_tweenId);

            try
            {
                await tween.AsyncWaitForCompletion();
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                // cg.alpha = 1f;
                // cg.blocksRaycasts = true;
                // cg.interactable = true;
            }
        }

        private async void QuitGame()
        {
            Debug.Log("Quit Game");
            okButton.interactable = false;
            // StartCoroutine(MainGameManager.Instance.Co_EndGame(true));

            if (MainGameManager.Instance)
                MainGameManager.Instance.EndGameAsync().Forget();
            else
            {
                await Manager.UI.CloseAllPopupUI();
                
                if (PhotonNetwork.InRoom && !PhotonNetwork.InLobby &&
                    PhotonNetwork.NetworkClientState != ClientState.Leaving &&
                    PhotonNetwork.NetworkClientState != ClientState.Joining)
                {

                    //bgm이 있다면 멈추기
                    SoundManager.Instance.StopBGM();

                    //로비로 가는 로딩창 띄우기
                    var loadingUI = await ShowLoadingAsync();
                    Manager.UI.ShowPopupUI(loadingUI).Forget();
                    
                    PhotonNetwork.LeaveRoom();
                }
            }
            
        }
        private async UniTask<UI_Loading> ShowLoadingAsync()
        {
            var loading = Manager.UI.CreatePopupUI<UI_Loading>();
            loading.ApplyTheme(uiLoadingTheme);
            loading.SetProgress(0f);
            return loading;
        }
    }
}