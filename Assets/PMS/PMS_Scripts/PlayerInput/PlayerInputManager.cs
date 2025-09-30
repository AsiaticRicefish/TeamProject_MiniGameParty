using DesignPattern;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ShootingScene;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PMS_Util;
using Cysharp.Threading.Tasks;


namespace ShootingScene
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputManager : CombinedSingleton<PlayerInputManager>, IGameComponent
    {
        [Header("References")]
        private PlayerInput playerInput; // PlayerInput 컴포넌트 참조 변수

        public event Action<InputAction.CallbackContext> onTouchPress;
        public event Action<InputAction.CallbackContext> onCameraGesture;
        public event Action<InputAction.CallbackContext> onCameraPosition;

        // 실제 사용할 InputAction 레퍼런스
        private InputAction _touchAction;                   // 유니모 터치 액션 참조 변수 (실질적인 게임 플레이 액션)
        private InputAction _cameraGestureAction;           // 카메라 액션 참조 변수 (스와이프, 줌 등 -> 부가적인 카메라 연출을 하기 위한 인풋액션)
        private InputAction _cameraPositionAction;          //카메라 터치 Pos값 - PrimaryPosition

        private readonly Dictionary<InputMode, List<InputAction>> _modeActions = new();

        // 현재 활성 모드 저장
        private InputMode _currentMode = InputMode.None;
        private InputMode _pendingMode;

        protected override void OnAwake()
        {
            isPersistent = false;
            Debug.Log("PlayerInputManager OnAwake 호출");
        }

        public void Initialize()
        {
            InitializeActions();

            // 모드별로 사용할 액션 등록
            RegisterMode(InputMode.Gameplay, _touchAction);
            RegisterMode(InputMode.Camera, _cameraGestureAction, _cameraPositionAction);
            // 만약 InputSystemUIInputModule을 쓰면 UI 모드 액션도 여기에 등록 가능
            // RegisterMode(InputMode.UI, playerInput.actions["Navigate"], playerInput.actions["Submit"]);

            // 기본은 전부 꺼두기
            SetInputMode(InputMode.None);

            // 콜백 구독
            RegisterCallbacks();
        }

        private void InitializePlayerInput()
        {
            playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                Debug.LogError("PlayerInput 컴포넌트를 찾을 수 없습니다!");
            }
        }

        protected override void OnDestroy()
        {
            UnregisterCallbacks();
        }

        // 1) PlayerInput에서 액션 뽑아오기
        private void InitializeActions()
        {
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            var playerMap = playerInput.actions.FindActionMap("Player");
            _touchAction = playerMap?.FindAction("TouchPress");

            var cameraMap = playerInput.actions.FindActionMap("Camera");
            _cameraGestureAction = cameraMap?.FindAction("PrimaryTouch");
            _cameraPositionAction = cameraMap?.FindAction("PrimaryPosition");
        }

        // 2) 모드별로 켤/끄를 액션을 등록
        public void RegisterMode(InputMode mode, params InputAction[] actions)
        {
            if (!_modeActions.ContainsKey(mode))
                _modeActions[mode] = new List<InputAction>();

            foreach (var act in actions)
            {
                if (act != null && !_modeActions[mode].Contains(act))
                    _modeActions[mode].Add(act);
            }
        }

        // 3) 호출 한 줄로 각 모드를 Enable/Disable
        public void SetInputMode(InputMode mode)
        {
            _currentMode = mode;

            foreach (var kv in _modeActions)
            {
                bool shouldBeOn = mode.HasFlag(kv.Key);
                foreach (var action in kv.Value)
                {
                    if (shouldBeOn) action.Enable();
                    else action.Disable();
                }
            }
        }

        /*/// <summary>
        /// 콜백 내에서 바로 호출해도 안전하도록,
        /// 다음 프레임 LateUpdate 시점에 모드 전환을 수행합니다.
        /// </summary>
        public void RequestInputModeAsync(InputMode mode)
        {
            _pendingMode = mode;
            UniTask.Void(async () =>
            {
                // 모든 InputSystem 콜백이 끝나고 LateUpdate 이후에 적용
                await UniTask.Yield(PlayerLoopTiming.LastUpdate);
                SetInputMode(_pendingMode);
            });
        }*/

        // 4) InputAction 콜백 구독
        private void RegisterCallbacks()
        {
            if (_touchAction != null)
                _touchAction.started += OnTouchPress;

            if (_cameraGestureAction != null)
            {
                _cameraGestureAction.started += OnCameraGesture;
                _cameraGestureAction.canceled += OnCameraGesture;
            }

            if (_cameraPositionAction != null)
                _cameraPositionAction.performed += OnCameraPosition;
        }

        // 5) 콜백 해제
        public void UnregisterCallbacks()
        {
            if (_touchAction != null)
                _touchAction.started -= OnTouchPress;

            if (_cameraGestureAction != null)
            {
                _cameraGestureAction.started -= OnCameraGesture;
                _cameraGestureAction.canceled -= OnCameraGesture;
            }

            if (_cameraPositionAction != null)
                _cameraPositionAction.performed -= OnCameraPosition;
        }

        // 6) 터치 콜백 – 특정 UI면 무시, 아니면 이벤트 발생
        private void OnTouchPress(InputAction.CallbackContext ctx)
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            onTouchPress?.Invoke(ctx);
        }

        // 7) 카메라 제스처 콜백
        private void OnCameraGesture(InputAction.CallbackContext ctx)
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            onCameraGesture?.Invoke(ctx);
        }

        // 8) 카메라 포지션 콜백
        private void OnCameraPosition(InputAction.CallbackContext ctx)
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            onCameraPosition?.Invoke(ctx);

        }

        private readonly Stack<InputMode> _modeStack = new();

        public void PushMode(InputMode mode)
        {
            _modeStack.Push(_currentMode);
            StartCoroutine(DelayedSetMode(mode));
        }

        public void PopMode()
        {
            if (_modeStack.Count > 0)
                StartCoroutine(DelayedSetMode(_modeStack.Pop()));
            else
                StartCoroutine(DelayedSetMode(InputMode.None));
        }

        private IEnumerator DelayedSetMode(InputMode mode)
        {
            yield return null; // 다음 프레임으로 연기
            SetInputMode(mode);
        }
    }
}