//using DesignPattern;
//using UnityEngine;
//using UnityEngine.InputSystem;

//namespace PMS_Legacy
//{
//    public class InputManager : CombinedSingleton<InputManager>, IGameComponent
//    {
//        [SerializeField] private Camera mainCam;

//        private PlayerInput playerInput; // PlayerInput 컴포넌트 참조 변수
//        private InputAction touchAction; // TouchPress 액션 참조 변수

//        private UnimoEgg selectedUnimoEgg;

//        protected override void OnAwake()
//        {
//            isPersistent = false;
//        }

//        public void Initialize()
//        {
//            // PlayerInput 컴포넌트를 가져오고, 액션을 찾습니다.
//            playerInput = GetComponent<PlayerInput>();
//            if (playerInput != null)
//            {
//                touchAction = playerInput.actions.FindAction("TouchPress");
//            }
//        }

//        #region 싱글 환경 테스트 코드 - 추후 자기턴일때 자기만 Input이 가능하도록 설계 예정 
//        private void OnEnable()
//        {
//            EnableInput();
//        }

//        private void OnDisable()
//        {
//            // 스크립트가 비활성화될 때 이벤트 구독 해제
//            DisableInput();
//        }
//        #endregion

//        public void EnableInput()
//        {
//            // TouchPress 액션의 이벤트를 구독
//            if (touchAction != null)
//            {
//                touchAction.started += OnTouchPress;
//                touchAction.performed += OnTouchPress;
//                touchAction.canceled += OnTouchPress;
//                touchAction.Enable();
//            }
//        }

//        //touchAction 비활성화 - 클릭못하게 막음
//        public void DisableInput()
//        {
//            if (touchAction != null)
//            {
//                touchAction.started -= OnTouchPress;
//                touchAction.performed -= OnTouchPress;
//                touchAction.canceled -= OnTouchPress;
//                touchAction.Disable();
//            }
//        }

//        // TouchPress 이벤트 연결
//        public void OnTouchPress(InputAction.CallbackContext ctx)
//        {
//            Vector2 screenPos;

//            //왜 인지 모르겠는데 pc에서 Simulrator로 Player하면 자꾸 이상한데 값을 들고옴

//            // 터치인지 마우스인지 확인해서 위치 가져오기
//            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
//            {
//                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();   //Vector2 -> x,y값
//            }
//            else if (Mouse.current != null)
//            {
//                screenPos = Mouse.current.position.ReadValue(); //Vector2 -> x,y값
//            }
//            else
//            {
//                return;
//            }

//            Ray ray = mainCam.ScreenPointToRay(screenPos);

//            if (ctx.started)
//            {
//                Debug.Log("start");
//                // 터치 시작 → Raycast로 알 선택
//                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("UnimoEgg")))
//                {
//                    selectedUnimoEgg = hit.collider.GetComponent<UnimoEgg>();
//                    selectedUnimoEgg?.OnTouchStart(screenPos);
//                }
//            }
//            else if (ctx.performed)
//            {
//                Debug.Log("move");
//                selectedUnimoEgg?.OnTouchMove(screenPos);
//            }
//            else if (ctx.canceled)
//            {
//                Debug.Log("end");
//                selectedUnimoEgg?.OnTouchEnd(screenPos);
//                selectedUnimoEgg = null;

//                //DisableInput();                         //한번 쏘고 나면 다시 못쏘도록

//                if (selectedUnimoEgg == null)
//                {
//                    Debug.Log("NULL처리완료");
//                }

//            }
//        }

//        public void OnTouchPosition(InputAction.CallbackContext ctx)
//        {
//            Vector3 touchpos = ctx.ReadValue<Vector3>();
//            // Debug.Log("터치 위치: " + pos);
//        }
//    }
//}
