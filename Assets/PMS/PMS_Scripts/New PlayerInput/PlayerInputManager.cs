using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShootingScene
{
    public class PlayerInputManager : MonoBehaviour
    {
        public static PlayerInputManager Instance { get; private set; }

        [SerializeField] private Camera mainCam;

        private PlayerInput playerInput; // PlayerInput 컴포넌트 참조 변수
        private InputAction touchAction; // TouchPress 액션 참조 변수

        private DirectionSign selectedDirectionSign;
        private UnimoEgg currentPlayerUnimo;

        //private float limitTime = 5.0f;    //플레이 제한시간
        private float chargingtime;        //현재 차징 시간

        private bool isPressing = false;

        //터치 하면 UI가 끊어지게
        public event Action onTouched;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject); // 혹은 그냥 return
            }

            // PlayerInput 컴포넌트를 가져오고, 액션을 찾습니다.
            playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                touchAction = playerInput.actions.FindAction("TouchPress");
            }
        }

        private void Update()
        {
            // 누르고 있는 동안만 timer 증가
            if (isPressing)
            {
                chargingtime += Time.deltaTime;
            }
        }

        #region 싱글 환경 테스트 코드 - 추후 자기턴일때 자기만 Input이 가능하도록 설계 예정 
        private void OnEnable()
        {
            EnableInput();
        }

        private void OnDisable()
        {
            // 스크립트가 비활성화될 때 이벤트 구독 해제
            DisableInput();
        }
        #endregion

        public void EnableInput()
        {
            // TouchPress 액션의 이벤트를 구독
            if (touchAction != null)
            {
                touchAction.started += OnTouchPress;
                touchAction.performed += OnTouchPress;
                touchAction.canceled += OnTouchPress;
                touchAction.Enable();
            }
        }

        //touchAction 비활성화 - 클릭못하게 막음
        public void DisableInput()
        {
            if (touchAction != null)
            {
                touchAction.started -= OnTouchPress;
                touchAction.performed -= OnTouchPress;
                touchAction.canceled -= OnTouchPress;
                touchAction.Disable();
            }
        }

        // TouchPress 이벤트 연결
        public void OnTouchPress(InputAction.CallbackContext ctx)
        {
            /*if(limitTime > 5.0f)
            {
                Debug.Log("제한시간이 지나서 강제 실행");

                // TODO - 강제 실행 추가 작업처리                 
                // 아예 손을 안댔거나
                // 터치중인데 제한시간이 지났거나
                 
            }*/

            Vector2 screenPos;

            //왜 인지 모르겠는데 pc에서 Simulrator로 Player하면 자꾸 이상한데 값을 들고옴

            // 터치인지 마우스인지 확인해서 위치 가져오기
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();   //Vector2 -> x,y값
            }
            else if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue(); //Vector2 -> x,y값
            }
            else
            {
                return;
            }

            Ray ray = mainCam.ScreenPointToRay(screenPos);

            if (ctx.started)
            {
                Debug.Log("start");

                // 타이머 초기화
                ResetChargingTime();

                // 터치 시작 → Raycast로 알 선택
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("DirectionSign")))
                {
                    Debug.Log("Raycast hit DirectionSign!");
                    onTouched?.Invoke();
                    selectedDirectionSign = hit.collider.GetComponent<DirectionSign>();
                    selectedDirectionSign?.OnTouchStart(screenPos);
                }
            }
            else if (ctx.performed)
            {
                isPressing = true;
                Debug.Log($"click중 - 누른 시간: {chargingtime}초");
                selectedDirectionSign?.OnTouch(chargingtime);

            }
            else if (ctx.canceled)
            {
                Debug.Log($"end - 최종 누른 시간: {chargingtime:F2}초");
                isPressing = false; // 타이머 증가 중단

                selectedDirectionSign?.OnTouchEnd();
                selectedDirectionSign = null;

                //DisableInput();                         //한번 쏘고 나면 다시 못쏘도록

                if (selectedDirectionSign == null)
                {
                    Debug.Log("NULL처리완료");
                }

            }
        }

        public void ResetChargingTime()
        {
            // 타이머 초기화 및 차징중인지 아닌지 bool 변수 초기화
            chargingtime = 0f;
            isPressing = false;
        }

        public void OnTouchPosition(InputAction.CallbackContext ctx)
        {
            Vector3 touchpos = ctx.ReadValue<Vector3>();
            // Debug.Log("터치 위치: " + pos);
        }
    }
}
