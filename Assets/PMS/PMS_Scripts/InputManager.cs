using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera mainCam;

    private PlayerInput playerInput; // PlayerInput 컴포넌트 참조 변수
    private InputAction touchAction; // TouchPress 액션 참조 변수

    private UnimoEgg selectedUnimoEgg;

    private void Awake()
    {
        // PlayerInput 컴포넌트를 가져오고, 액션을 찾습니다.
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            touchAction = playerInput.actions.FindAction("TouchPress");
        }
    }
    private void OnEnable()
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

    private void OnDisable()
    {
        // 스크립트가 비활성화될 때 이벤트 구독 해제
        if (touchAction != null)
        {
            touchAction.started -= OnTouchPress;
            touchAction.performed -= OnTouchPress;
            touchAction.canceled -= OnTouchPress;
            touchAction.Disable();
        }
    }

    private void Update()
    {
        Debug.DrawRay(transform.position, transform.forward * 10f, Color.red); // 100f는 Ray의 길이, 1f는 표시 시간
        //아니 레이가 왜 안보이지 기즈모를 껏나?  
    }

    // TouchPress 이벤트 연결
    public void OnTouchPress(InputAction.CallbackContext ctx)
    {
        Vector2 screenPos;

        //왜 인지 모르겠는데 pc에서 자꾸 이상한데 값을 들고옴
        //Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();

        // 터치인지 마우스인지 확인해서 위치 가져오기
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
        }
        else
        {
            return;
        }
        Ray ray = mainCam.ScreenPointToRay(screenPos);
        

        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red); // 100f는 Ray의 길이, 1f는 표시 시간

        if (ctx.started)
        {
            Debug.Log("start");
            // 터치 시작 → Raycast로 알 선택
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                selectedUnimoEgg = hit.collider.GetComponent<UnimoEgg>();
                Debug.Log(hit.collider.gameObject.name);
                selectedUnimoEgg?.OnTouchStart(screenPos);          
            }
        }
        else if (ctx.performed)
        {
            Debug.Log("move");
            selectedUnimoEgg?.OnTouchMove(screenPos);
        }
        else if(ctx.canceled)
        {
            Debug.Log("end");
            selectedUnimoEgg?.OnTouchEnd(screenPos);
            selectedUnimoEgg = null;

            if(selectedUnimoEgg == null)
            {
                Debug.Log("NULL처리완료");
            }
        }
    }

    public void OnTouchPosition(InputAction.CallbackContext ctx)
    {
        Vector3 touchpos = ctx.ReadValue<Vector3>();
        // Debug.Log("터치 위치: " + pos);
    }
}
