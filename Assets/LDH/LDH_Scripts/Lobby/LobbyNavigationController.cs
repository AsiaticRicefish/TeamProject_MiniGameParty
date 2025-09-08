using System;
using System.Collections.Generic;
using System.Threading;
using Cinemachine;
using Cysharp.Threading.Tasks;
using LDH_Camera;
using LDH_UI;
using LDH_Util;
using Managers;
using UnityEngine;

namespace LDH_Lobby
{
    public class LobbyNavigationController : MonoBehaviour
    {
        [Serializable]
        public struct MenuEntry
        {
            public string id; 
            public UI_Popup popup;
        }

        
        [Header("Input Lock")] [SerializeField]
        private InputLockController inputLock; // EventSystem / UI 모듈 끄는 컴포넌트

        [Header("Cameras")] [SerializeField] private CinemachineBrain brain;
        [SerializeField] private VirtualCamera_Lobby[] lobbyCams;
        
        [Header("UI")]
        [SerializeField] private MenuEntry[] menuEntries;

        private static LobbyNavigationController _instance;
        public static LobbyNavigationController Instance => _instance;

        private Dictionary<string, VirtualCamera_Lobby> _camDict = new();
        private Dictionary<string, UI_Popup> _uiDict = new();
        
        private bool _isSwitching;
        private UI_Popup _currentPopupInstance;

        private void Awake()
        {
            _instance = this;
            RegisterCams();
            RegisterMenus();

            CloseAllUI().Forget();
        }
        
        #region Register

        private void RegisterCams()
        {
            if (lobbyCams.Length == 0) return;
            foreach (VirtualCamera_Lobby lobbyCam in lobbyCams)
            {
                _camDict.Add(lobbyCam.cameraID, lobbyCam);
            }
            
            Debug.Log($"[LobbyCameraController] 등록된 카메라 개수 : {_camDict.Count} ");
        }
        
        private void RegisterMenus()
        {
            foreach (var e in menuEntries)
            {
                if (string.IsNullOrEmpty(e.id) || e.popup == null) continue;
                if (_uiDict.ContainsKey(e.id))
                    Debug.LogWarning($"[LobbyNav] duplicate menu id: {e.id}");
                else
                    _uiDict.Add(e.id, e.popup);
            }
        }

        #endregion
     
        
        public void RequestFocus(string id) => SwitchToAsync(id).Forget();

        private async UniTaskVoid SwitchToAsync(string id)
        {
            if(_isSwitching) return;
            _isSwitching = true;
            
            inputLock?.Lock();   // 전환하는 동안 입력 막기
            
            await CloseCurrentUI();  // 활성화된 현재 팝업이 있다면 닫기
            
            SetVCamPriority(id);               // 카메라 전환 시작
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);    // 브레인이 블렌드를 시작할 프레임을 한번 넘겨줌
            
            await WaitBlendCompleteAsync(_camDict[id].VCam, default);  // 블렌드 끝날 때까지 대기

            await ShowUI(id);   // 해당하는 UI 활성화
            
            inputLock?.Unlock();   // input 입력 차단 해제
            _isSwitching = false;
        }


        private void SetVCamPriority(string id)
        {
            foreach (var(camId, cam) in _camDict)
            {
                if (camId == id)
                    cam.VCam.Priority = cam.FocusPriority;
                else
                    cam.VCam.Priority = cam.OffPriority;
            }
        }

        private async UniTask WaitBlendCompleteAsync(CinemachineVirtualCamera target, CancellationToken ct)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            while (!ct.IsCancellationRequested)
            {
                var active = brain.ActiveVirtualCamera;
                bool isTarget = active != null &&  active.VirtualCameraGameObject == target.gameObject;

                if (!brain.IsBlending && isTarget)
                    break;

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);

            }

        }

        private async UniTask CloseCurrentUI()
        {
            if (_currentPopupInstance != null)
            {
                await Manager.UI.ClosePopupUI(_currentPopupInstance, false);
                _currentPopupInstance = null;
            }

        }
        
        private async UniTask ShowUI(string id)
        {
           
            if (_uiDict.TryGetValue(id, out UI_Popup popup))
            {
                _currentPopupInstance = popup;
                await Manager.UI.ShowPopupUI(popup);
            }
        }

        private async UniTask CloseAllUI()
        {
            foreach (UI_Popup popup in _uiDict.Values)
            {
                await popup.CloseAsync();
            }
        }
    }
}