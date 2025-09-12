using DesignPattern;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ShootingScene;

namespace ShootingScene
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputManager : CombinedSingleton<PlayerInputManager>, IGameComponent
    {
        public CameraSwipeController cameraSwipeController; 

        private PlayerInput playerInput; // PlayerInput 컴포넌트 참조 변수

        public InputAction touchAction; // 유니모 터치 액션 참조 변수 (실질적인 게임 플레이 액션)
        public InputAction cameraControlAction; // 카메라 액션 참조 변수 (스와이프, 줌 등 -> 부가적인 카메라 연출을 하기 위한 인풋액션)
        public InputAction cameraPositionAction; // PrimaryPosition

        //유니모 터치 액션
        public event Action<InputAction.CallbackContext> onTouchPress;

        //카메라 터치 액션
        public event Action<InputAction.CallbackContext> onCameraGesture;
        public event Action<InputAction.CallbackContext> onCameraPosition;

        private bool inputEnabled = false;
        private bool cameraControlEnabled = false;
        private bool cameraPositionEnabled = false;

        protected override void OnAwake()
        {
           isPersistent = false;
           Debug.Log("PlayerInputManager OnAwake 호출");
        }

        //테스트코드
        //private void Start()
        //{
        //    Initialize();
        //}

        public void OnTouchPress(InputAction.CallbackContext ctx)
        {
            onTouchPress?.Invoke(ctx); // 구독자에게 전달
        }

        public void OnCameraGesture(InputAction.CallbackContext ctx)
        {
            Debug.Log("[PlayerInputManger] - OnCameraGesture 가 이벤트 Invoke");
            onCameraGesture?.Invoke(ctx); // 구독자에게 전달
        }

        public void OnCameraPosition(InputAction.CallbackContext ctx)
        {
            onCameraPosition?.Invoke(ctx); // 구독자들에게 이벤트 전달
        }

        public void Initialize()
        {
            Debug.Log("PlayerInputManager 초기화 시도");

            // PlayerInput 컴포넌트 초기화
            InitializePlayerInput();

            // Input Actions 초기화
            InitializeInputActions();

            RegisterActions();          // 액션 구독 등록하고

            Debug.Log("[PlayerInputManager] - cameraSwipeController 의존성 주입");
            //의존성 주입 CameraSwipeController
            //cameraSwipeController.Initialize(this);

            DisableAllInput2();
            //DisableAllInput();          // 액션을 비활성화
        }

        private void InitializePlayerInput()
        {
            playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                Debug.LogError("PlayerInput 컴포넌트를 찾을 수 없습니다!");
            }
        }

        private void InitializeInputActions()
        {
            if (playerInput == null) return;

            // TouchPress
            var touchMap = playerInput.actions.FindActionMap("Player");
            touchAction = touchMap.FindAction("TouchPress");
            if (touchAction == null) Debug.LogError("TouchPress 액션을 찾을 수 없습니다!");

            // PrimaryTouch
            var cameraMap = playerInput.actions.FindActionMap("Camera");
            cameraControlAction = cameraMap.FindAction("PrimaryTouch");
            if (cameraControlAction == null) Debug.LogError("PrimaryTouch 액션을 찾을 수 없습니다!");

            // PrimaryPosition
            cameraPositionAction = cameraMap.FindAction("PrimaryPosition");
            if (cameraPositionAction == null) Debug.LogError("PrimaryPosition 액션을 찾을 수 없습니다!");
        }

        #region 유니모 터치 클릭 관련 활성/비활성화 함수
        public void EnableInput()
        {
            Debug.Log("유니모 클릭 활성화");

            if (inputEnabled) return; // 이미 활성화되었으면 그냥 리턴

            if (touchAction != null)
            {
                touchAction.Enable();
            }
            inputEnabled = true;
        }

        public void DisableInput()
        {
            Debug.Log("유니모 클릭 비활성화");
            if (!inputEnabled) return;

            if (touchAction != null)
            {
                touchAction.Disable();
            }
            inputEnabled = false;
        }
        #endregion

        #region 카메라 관련 InputAction 구독,구독해제 함수 (스와이프,줌인줌아웃 관련 활성/비활성화 함수)
        public void EnableCameraControl()
        {
            Debug.Log("카메라 컨트롤 액션 활성화");

            if (cameraControlEnabled) return;

            if (cameraControlAction != null)
            {
                cameraControlAction.Enable();
            }
            cameraControlEnabled = true;
        }

        public void DisableCameraControl()
        {
            Debug.Log("카메라 컨트롤 액션 비활성화");
            if (!cameraControlEnabled) return;

            if (cameraControlAction != null)
            {
                cameraControlAction.Disable();
            }
            cameraControlEnabled = false;
        }

        public void EnableCameraPosition()
        {
            Debug.Log("카메라 포지션 액션 활성화");
            if (cameraPositionEnabled) return;

            if (cameraPositionAction != null)
            {
                cameraPositionAction.Enable();
            }
            cameraPositionEnabled = true;
        }

        public void DisableCameraPosition()
        {
            Debug.Log("카메라 포지션 액션 비활성화");
            if (!cameraPositionEnabled) return;

            if (cameraPositionAction != null)
            {
                cameraPositionAction.Disable();
            }
            cameraPositionEnabled = false;
        }
        #endregion

        //구독만 하고 Enable처리는 각자 따로 하기
        public void RegisterActions()
        {
            if (touchAction != null && !inputEnabled)
            {
                touchAction.started += OnTouchPress;
            }

            if (cameraControlAction != null && !cameraControlEnabled)
            {
                cameraControlAction.started += OnCameraGesture;
                cameraControlAction.canceled += OnCameraGesture;
            }

            if (cameraPositionAction != null && !cameraPositionEnabled)
            {
                cameraPositionAction.performed += OnCameraPosition;
            }
        }

        //비활성화 및 구독해제
        private void UnRegisterActions()
        {
            // 플래그 상관없이 강제로 비활성화
            if (touchAction != null && inputEnabled == true)
            {
                touchAction.started -= OnTouchPress;
                touchAction.Disable();
            }

            if (cameraControlAction != null && cameraControlEnabled == true)
            {
                cameraControlAction.started -= OnCameraGesture;
                cameraControlAction.canceled -= OnCameraGesture;
                cameraControlAction.Disable();
            }

            if (cameraPositionAction != null && cameraPositionEnabled == true)
            {
                cameraPositionAction.performed -= OnCameraPosition;
                cameraPositionAction.Disable();
            }

            // 플래그 리셋
            inputEnabled = false;
            cameraControlEnabled = false;
            cameraPositionEnabled = false;
        }

        public void EnableAllInput()
        {
            EnableInput();
            EnableCameraControl();
            EnableCameraPosition();
        }

        public void DisableAllInput2()
        {
            Debug.Log("모든 액션 비활성화");
            touchAction.Disable();
            cameraControlAction.Disable();
            cameraPositionAction.Disable();
        }

        public void DisableAllInput()
        {
            DisableInput();
            DisableCameraControl();
            DisableCameraPosition();
        }

        //게임 종료시 구독 해제 처리
        public void Cleanup()
        {
            UnRegisterActions();

            // 이벤트 구독자들 정리
            onTouchPress = null;
            onCameraGesture = null;
            onCameraPosition = null;
        }
    }
}