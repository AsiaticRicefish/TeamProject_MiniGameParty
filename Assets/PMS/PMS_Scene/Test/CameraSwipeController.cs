using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class CameraSwipeController : MonoBehaviour
{
    [Header("Settings")]
    public float swipeSensitivity = 0.01f;  // Z축 이동 민감도
    [SerializeField] private float minZ;
    [SerializeField] private float maxZ;

    [SerializeField] private GameObject finishLine; 

    private bool isSwiping = false;
    private Vector2 lastPosition;

    private void Start()
    {
        EnableInput();
        //z축 범위 지정
        minZ = transform.position.z;
        maxZ = finishLine.transform.position.z;
    }

    public void EnableInput()
    {
        Debug.Log("구독 처리 완료");
        ShootingScene.PlayerInputManager.Instance.onCameraGesture += HandleTouch;        // PrimaryTouch
        ShootingScene.PlayerInputManager.Instance.onCameraPosition += HandlePosition;    // PrimaryPosition
    }

    public void DisableInput()
    {
        Debug.Log("구독 해제 처리 완료");
        ShootingScene.PlayerInputManager.Instance.onCameraGesture -= HandleTouch;
        ShootingScene.PlayerInputManager.Instance.onCameraPosition -= HandlePosition;
    }

    private void HandleTouch(InputAction.CallbackContext ctx)
    {
        switch (ctx.phase)
        {
            case InputActionPhase.Started:
                lastPosition = Vector2.zero;
                isSwiping = true;
                break;
            case InputActionPhase.Canceled:
                isSwiping = false;
                break;
        }
    }

    private void HandlePosition(InputAction.CallbackContext ctx)
    {
        if (!isSwiping) return;

        //Debug.Log("터치중");

        Vector2 current = ctx.ReadValue<Vector2>();
        Vector2 delta = lastPosition == Vector2.zero ? Vector2.zero : current - lastPosition;
        lastPosition = current;

        // Z축 이동 처리
        Vector3 pos = transform.localPosition;
        pos.z = Mathf.Clamp(pos.z - delta.y * swipeSensitivity, minZ, maxZ);
        transform.localPosition = pos;
    }
}
