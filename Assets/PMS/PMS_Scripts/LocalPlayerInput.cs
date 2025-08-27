using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
//using Photon.Realtime;
using ShootingScene;
using Photon.Realtime;

public class LocalPlayerInput : MonoBehaviourPun//, IPunOwnershipCallbacks
{
    public Transform player;
    public DirectionUIArrow arrow;
    public ChargeController charger;
    public Camera mainCam;

    public float coneAngle = 100f;
    public float coneDistance = 5f;
    public Color coneColor = new Color(0f, 1f, 0f, 0.3f);

    private bool isInputActive = false;
    private bool inputEnabled = false;

    private void OnEnable()
    {
        Debug.Log("InputManager 구독");
        ShootingScene.PlayerInputManager.Instance.onTouchPress += HandleTouch;
    }
    private void OnDisable()
    { 
        ShootingScene.PlayerInputManager.Instance.onTouchPress -= HandleTouch;
    }

    public void EnableInput()
    {
        Debug.Log("EnableInpute처리 완료");
        inputEnabled = true;
    }
    public void DisableInput() => inputEnabled = false;

    private void Awake()
    {
        player = gameObject.transform;
        charger = gameObject.GetComponent<ChargeController>();
        mainCam = Camera.main;
    }
    private void HandleTouch(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled) return;

        //Vector3 worldPos = ScreenToWorld(ctx.ReadValue<Vector2>());

        Vector2 screenPos;

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

        if (ctx.started) //&& IsWithinCone(screenPos))
        {
            arrow.Freeze();
            charger.StartCharge();
            isInputActive = true;
        }
        else if (ctx.canceled && isInputActive)
        {
            //var currentEgg = EggManager.Instance.currentUnimoEgg;
            var unimo = gameObject.GetComponent<UnimoEgg>();
            if (unimo != null)
                unimo.Shot(arrow.CurrentDir * charger.ChargePower);

            charger.StopCharge();
            arrow.Resume();
            isInputActive = false;
            DisableInput();
        }
    }

    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        /*Ray ray = mainCam.ScreenPointToRay(screenPos);
        Plane groundPlane = new Plane(Vector3.up, player.position); // y=player.position.y 평면
        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter); // XZ 평면 좌표
        }
        return player.position;*/
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main Camera 없음!");
            return Vector3.zero;
        }

        // Orthographic 카메라: 카메라에서 얼마나 떨어진 평면을 변환할지 지정
        float zDistance = 0f; // 예: z = 0 월드 평면 기준
        Vector3 screenPoint = new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane + zDistance);

        return cam.ScreenToWorldPoint(screenPoint);
    }

    private bool IsWithinCone(Vector2 screenPos)
    {
        // Screen → World 변환
        Vector3 worldPos = ScreenToWorld(screenPos);

        // XZ 평면 벡터 계산
        Vector3 toInput = worldPos - player.position;
        toInput.y = 0f;

        if (toInput.magnitude > coneDistance)
        {
            Debug.Log("터치 범위를 벗어났습니다");
            return false;
        }

        if(Vector3.Angle(player.forward, toInput) <= coneAngle * 0.5f)
        {
            Debug.Log("각도 안에서 터치 입력했습니다");
            return true;
        }
        else
        {
            Debug.Log("각도 안에서 터치 입력하지 않아서 입력이 취소 됩니다");
            return false;
        }

        //return Vector3.Angle(player.forward, toInput) <= coneAngle * 0.5f;
    }

    private void OnDrawGizmos()
    {
        if (player == null) return;

        Vector3 pos = player.position;
        Vector3 forward = player.forward;

        Vector3 leftDir = Quaternion.Euler(0, -coneAngle * 0.5f, 0) * forward;
        Vector3 rightDir = Quaternion.Euler(0, coneAngle * 0.5f, 0) * forward;

        Gizmos.color = coneColor;

        // 원뿔 경계선
        Gizmos.DrawLine(pos, pos + leftDir.normalized * coneDistance);
        Gizmos.DrawLine(pos, pos + rightDir.normalized * coneDistance);

        // 내부 라인
        /*int steps = 10;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 dir = Quaternion.Euler(0, -coneAngle * 0.5f + t * coneAngle, 0) * forward;
            Gizmos.DrawLine(pos, pos + dir.normalized * coneDistance);
        }*/
    }


    #region 소유권 변경 콜백 함수
    /*public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {

    }

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView == photonView)
        {
            UpdateUIVisibility();
        }
    }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        
    }*/
    #endregion

    /// <summary>
    /// 내 소유일 때만 UI 보이기
    /// </summary>
    public void UpdateUIVisibility()
    {
        photonView.RPC(nameof(RPC_UpdateUIVisibility), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_UpdateUIVisibility()
    {
        if (charger != null)
            charger.chargeSlider.gameObject.SetActive(photonView.IsMine);

        if (arrow != null)
            arrow.gameObject.SetActive(photonView.IsMine);
    }
}
