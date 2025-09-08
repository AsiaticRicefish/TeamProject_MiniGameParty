using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class CameraSwipeController : MonoBehaviour
{
    [Header("Settings")]
    public float swipeSensitivity = 0.01f;  // Z축 이동 민감도
    public float minZ = -10f;
    public float maxZ = 10f;

    public Camera targetCamera;

    private bool isSwiping = false;
    private Vector2 lastTouchPos;

    private void Start()
    {
        Debug.LogWarning("구독 처리 완료");
        var inputMgr = ShootingScene.PlayerInputManager.Instance;
        inputMgr.onCameraGesture += HandleTouch;        // PrimaryTouch
        inputMgr.onCameraPosition += HandlePosition;    // PrimaryPosition
    }

    //private void OnEnable()
    //{
    //    var inputMgr = ShootingScene.PlayerInputManager.Instance;
    //    inputMgr.onCameraGesture += HandleTouch;        // PrimaryTouch
    //    inputMgr.onCameraPosition += HandlePosition;    // PrimaryPosition
    //}

    //private void OnDisable()
    //{
    //    var inputMgr = ShootingScene.PlayerInputManager.Instance;
    //    inputMgr.onCameraGesture -= HandleTouch;
    //    inputMgr.onCameraPosition -= HandlePosition;
    //}

    private void HandleTouch(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            Debug.LogWarning("Primary Touch 터치함");
            lastTouchPos = Vector2.zero; // 필요 시 초기화
            lastTouchPos = ctx.ReadValue<Vector2>();
            isSwiping = true;
        }
        if (ctx.canceled)
        {
            Debug.LogWarning("Primary Touch 뗌");
            isSwiping = false;
        }
    }

    private void HandlePosition(InputAction.CallbackContext ctx)
    {
        if (!isSwiping) return;

        Debug.LogWarning("Primary Touch 터치중");

        Vector2 delta = ctx.ReadValue<Vector2>() - lastTouchPos;
        lastTouchPos = ctx.ReadValue<Vector2>();

        // Z축 이동 처리
        Vector3 pos = transform.localPosition;
        pos.z = Mathf.Clamp(pos.z - delta.y * swipeSensitivity, minZ, maxZ);
        transform.localPosition = pos;
    }

    #region Legacy
//    private bool isSwiping = false;
//    private Vector2 startPos;
//    private Vector2 currentPos;

//    [Header("Settings")]
//    public float swipeSensitivity = 0.01f;  // 이동 민감도
//    public float minZ = -10f;               // 최소 Z
//    public float maxZ = 10f;                // 최대 Z

//    private void RegisterInput()
//    {
//        ShootingScene.PlayerInputManager.Instance.onCameraGesture += HandleTouch;
//    }

//    private void UnRegisterInput()
//    {
//        ShootingScene.PlayerInputManager.Instance.onCameraGesture -= HandleTouch;
//    }

//    private void Start()
//    {
//        //테스트코드
//        RegisterInput();
//    }

//    private Vector2 GetInputPosition()
//    {
//#if UNITY_EDITOR || UNITY_STANDALONE
//        return Mouse.current.position.ReadValue();
//#else
//    return Touchscreen.current.primaryTouch.position.ReadValue();
//#endif
//    }

//    private void HandleTouch(InputAction.CallbackContext context)
//    {
//        if (context.started)
//        {
//            Debug.LogWarning("터치 시작");
//            startPos = GetInputPosition();//context.ReadValue<Vector2>();
//            isSwiping = true;
//        }

//        if (context.performed && isSwiping)
//        {
//            Vector2 currentPos = GetInputPosition();//context.ReadValue<Vector2>();
//            Vector2 delta = currentPos - startPos;

//            // Z축 이동
//            Vector3 pos = transform.localPosition;
//            pos.z = Mathf.Clamp(pos.z - delta.y * swipeSensitivity, minZ, maxZ);
//            transform.localPosition = pos;

//            startPos = currentPos;
//        }

//        if (context.canceled)
//        {
//            Debug.LogWarning("터치 손땜");
//            isSwiping = false;
//        }
//    }
    #endregion
}
