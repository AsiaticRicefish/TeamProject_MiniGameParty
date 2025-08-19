using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera mainCam;

    private UnimoEgg selectedMarble;

    // TouchPress 이벤트 연결
    public void OnTouchPress(InputAction.CallbackContext ctx)
    {
        Debug.Log("터치되고 있는지?");
        if (Touchscreen.current == null) return;

        Vector3 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(touchPos);

        var phase = Touchscreen.current.primaryTouch.phase.ReadValue();

        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            // 터치 시작 → Raycast로 알 선택
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

    // TouchPosition 이벤트는 필요 시 드래그 UI용으로 활용
    public void OnTouchPosition(InputAction.CallbackContext ctx)
    {
        Vector3 pos = ctx.ReadValue<Vector3>();
        // Debug.Log("터치 위치: " + pos);
    }
}
