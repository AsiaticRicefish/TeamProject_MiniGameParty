using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera mainCam;

    private UnimoEgg selectedMarble;

    // TouchPress �̺�Ʈ ����
    public void OnTouchPress(InputAction.CallbackContext ctx)
    {
        Debug.Log("��ġ�ǰ� �ִ���?");
        if (Touchscreen.current == null) return;

        Vector3 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(touchPos);

        var phase = Touchscreen.current.primaryTouch.phase.ReadValue();

        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            // ��ġ ���� �� Raycast�� �� ����
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                selectedMarble = hit.collider.GetComponent<UnimoEgg>();
                selectedMarble?.OnTouchStart(touchPos);
            }
        }
        else if (phase == UnityEngine.InputSystem.TouchPhase.Moved)
        {
            selectedMarble?.OnTouchMove(touchPos);
        }
        else if (phase == UnityEngine.InputSystem.TouchPhase.Ended)
        {
            selectedMarble?.OnTouchEnd(touchPos);
            selectedMarble = null;
        }
    }

    // TouchPosition �̺�Ʈ�� �ʿ� �� �巡�� UI������ Ȱ��
    public void OnTouchPosition(InputAction.CallbackContext ctx)
    {
        Vector3 pos = ctx.ReadValue<Vector3>();
        // Debug.Log("��ġ ��ġ: " + pos);
    }
}
