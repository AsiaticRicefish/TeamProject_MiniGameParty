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

    private bool isSwiping = false;
    private Vector2 lastPosition;

    private void Start()
    {
        Debug.LogWarning("구독 처리 완료");
        var inputMgr = ShootingScene.PlayerInputManager.Instance;
        inputMgr.onCameraGesture += HandleTouch;        // PrimaryTouch     // Press 액션
        inputMgr.onCameraPosition += HandlePosition;    // PrimaryPosition  // Position 액션

        //z축 범위 지정
        minZ = transform.position.z;
        maxZ = transform.position.z + 25.0f;
    }

    private void EnableInput()
    {
        ShootingScene.PlayerInputManager.Instance.onCameraGesture += HandleTouch;        // PrimaryTouch
        ShootingScene.PlayerInputManager.Instance.onCameraPosition += HandlePosition;    // PrimaryPosition
    }

    private void DisableInput()
    {
        ShootingScene.PlayerInputManager.Instance.onCameraGesture -= HandleTouch;
        ShootingScene.PlayerInputManager.Instance.onCameraPosition -= HandlePosition;
    }

    private void HandleTouch(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            Debug.LogWarning("터치함");
            lastPosition = Vector2.zero; //이동 좌표 초기화
            isSwiping = true;
        }
        if (ctx.canceled)
        {
            Debug.LogWarning("뗌");
            isSwiping = false;
        }
    }

    private void HandlePosition(InputAction.CallbackContext ctx)
    {
        if (!isSwiping) return;

        Debug.LogWarning("터치중");

        Vector2 current = ctx.ReadValue<Vector2>();
        Vector2 delta = lastPosition == Vector2.zero ? Vector2.zero : current - lastPosition;
        lastPosition = current;

        // Z축 이동 처리
        Vector3 pos = transform.localPosition;
        pos.z = Mathf.Clamp(pos.z - delta.y * swipeSensitivity, minZ, maxZ);
        transform.localPosition = pos;
    }
}
