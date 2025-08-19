using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera mainCam;

    private PlayerInput playerInput; // PlayerInput ������Ʈ ���� ����
    private InputAction touchAction; // TouchPress �׼� ���� ����

    private UnimoEgg selectedUnimoEgg;

    private void Awake()
    {
        // PlayerInput ������Ʈ�� ��������, �׼��� ã���ϴ�.
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            touchAction = playerInput.actions.FindAction("TouchPress");
        }
    }
    private void OnEnable()
    {
        // TouchPress �׼��� �̺�Ʈ�� ����
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
        // ��ũ��Ʈ�� ��Ȱ��ȭ�� �� �̺�Ʈ ���� ����
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
        Debug.DrawRay(transform.position, transform.forward * 10f, Color.red); // 100f�� Ray�� ����, 1f�� ǥ�� �ð�
        //�ƴ� ���̰� �� �Ⱥ����� ����� ����?  
    }

    // TouchPress �̺�Ʈ ����
    public void OnTouchPress(InputAction.CallbackContext ctx)
    {
        Vector2 screenPos;

        //�� ���� �𸣰ڴµ� pc���� �ڲ� �̻��ѵ� ���� ����
        //Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();

        // ��ġ���� ���콺���� Ȯ���ؼ� ��ġ ��������
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
        

        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red); // 100f�� Ray�� ����, 1f�� ǥ�� �ð�

        if (ctx.started)
        {
            Debug.Log("start");
            // ��ġ ���� �� Raycast�� �� ����
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
                Debug.Log("NULLó���Ϸ�");
            }
        }
    }

    public void OnTouchPosition(InputAction.CallbackContext ctx)
    {
        Vector3 touchpos = ctx.ReadValue<Vector3>();
        // Debug.Log("��ġ ��ġ: " + pos);
    }
}
