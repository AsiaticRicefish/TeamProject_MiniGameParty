using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using Cinemachine;

public class CameraSwipeController : MonoBehaviour,IGameComponent
{
    [Header("Settings")]
    public float swipeSensitivity = 0.01f;  // Z축 이동 민감도
    [SerializeField] private float minZ;
    [SerializeField] private float maxZ;

    [SerializeField] private GameObject finishLine; 

    private bool isSwiping = false;
    private Vector2 lastPosition;

    [Header("DI")]
    private ShootingScene.PlayerInputManager _inputManager;
    private bool _initialized = false;
    
    private void Start()
    {
        //z축 범위 지정
        minZ = transform.position.z;
        maxZ = finishLine.transform.position.z - 10.0f;
    }
    
    #region Public API
    /// <summary>
    /// PlayerInputManager를 주입하고 초기화 구독 ? Start() -> 
    /// </summary>
    //public void Initialize(ShootingScene.PlayerInputManager inputManager)
    //{
    //    if (_initialized) return;

    //    _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
    //    _initialized = true;

    //    EnableInput();
    //}

    //public void EnableInput()
    //{
    //    if (_inputManager == null)
    //    {
    //        Debug.LogWarning("EnableInput 호출 시 PlayerInputManager가 없음. Initialize 필요");
    //        return;
    //    }

 
    //    _inputManager.onCameraGesture += HandleTouch;
    //    _inputManager.onCameraPosition += HandlePosition;
    //    Debug.Log("CameraSwipeController 구독 완료");
    //}

    //public void DisableInput()
    //{
    //    if (_inputManager == null) return;

    //    _inputManager.onCameraGesture -= HandleTouch;
    //    _inputManager.onCameraPosition -= HandlePosition;
    //    Debug.Log("CameraSwipeController 구독 해제");
    //}
    #endregion

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

    public void Initialize()
    {
        Debug.Log("[CameraSwipeController] - 초기화되나요");
        ShootingScene.PlayerInputManager.Instance.onCameraGesture += HandleTouch; //PrimaryTouch 
        ShootingScene.PlayerInputManager.Instance.onCameraPosition += HandlePosition; // PrimaryPosition
    }
}
