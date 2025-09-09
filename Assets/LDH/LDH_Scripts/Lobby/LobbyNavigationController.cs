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
using WebSocketSharp;

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
        private string _currentFocusId;

        private void Awake()
        {
            _instance = this;
            RegisterCams();
            RegisterMenus();
            
            CloseAllUI().Forget();
        }

        private void Start()
        {
            RequestFocus("Home");
        }

        private void OnDestroy()
        {
            _instance = null;
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
            if(!_currentFocusId.IsNullOrEmpty() && _currentFocusId.Equals(id)) return; //이미 focus인 걸 또 focus 하는 경우 return
            
            _isSwitching = true;
            
            inputLock?.Lock();   // 전환하는 동안 입력 막기
            
            try
            {
                // 1) ID 검증
                if (!_camDict.TryGetValue(id, out var cam))
                {
                    Debug.LogWarning($"[LobbyNav] unknown id: {id}");
                    return;
                }
                
                // 현재 focus 업데이트
                _currentFocusId = id;

                // 2) 현재 UI 먼저 닫기(예외 안전)
                await CloseCurrentUI();

                // 3) 카메라 전환
                SetVCamPriority(id);
                
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

                // 4) 블렌드 완료 대기 + 타임아웃(예: 2초)
                await WaitBlendCompleteAsync(cam.VCam, this.GetCancellationTokenOnDestroy(), 2f);

                // 5) 대상 UI가 있으면 열기(없으면 스킵)
                await ShowUI(id);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyNav] Switch error: {e}");
            }
            finally
            {
                inputLock?.Unlock(); 
                _isSwitching = false;
            }
        }


        private void SetVCamPriority(string id)
        {
            Debug.Log($"id : {id}");
            
            foreach (var(camId, cam) in _camDict)
            {
                if (camId == id)
                {
                    cam.VCam.Priority = cam.FocusPriority;
                }
  
                else
                    cam.VCam.Priority = cam.OffPriority;
            }
            
            // 타겟 가상 카메라를 서브큐 최상단으로 올려 동점/기존 라이브 깨기
            var target = _camDict[id].VCam;
            target.MoveToTopOfPrioritySubqueue();
        }

        private async UniTask WaitBlendCompleteAsync(
            CinemachineVirtualCamera target,
            CancellationToken ct,
            float timeoutSec = 2f)
        {
            float start = Time.realtimeSinceStartup;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            
            while (!ct.IsCancellationRequested)
            {
                if (brain == null || target == null) break;
                
                var active = brain.ActiveVirtualCamera;
               
                bool isTarget = active != null &&  active.VirtualCameraGameObject == target.gameObject;

                if (!brain.IsBlending && isTarget)
                    break;
                if (Time.realtimeSinceStartup - start > timeoutSec)
                {
                    Debug.LogWarning("[LobbyNav] Blend wait timed out.");
                    break;
                }
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);

            }

        }

        private async UniTask CloseCurrentUI()
        {
            if (_currentPopupInstance == null) return;
            try { await Manager.UI.ClosePopupUI(_currentPopupInstance, false); }
            catch (Exception e) { Debug.LogWarning($"[LobbyNav] Close UI error: {e}"); }
            finally { _currentPopupInstance = null; }
            
        }
        
        private async UniTask ShowUI(string id)
        {
           
            if (_uiDict.TryGetValue(id, out var popup) && popup != null)
            {
                try
                {
                    _currentPopupInstance = popup;
                    await Manager.UI.ShowPopupUI(popup);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LobbyNav] Show UI error: {e}");
                    _currentPopupInstance = null;
                }
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