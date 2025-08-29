using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
//using Photon.Realtime;
using ShootingScene;
using Photon.Realtime;

public class LocalPlayerInput : MonoBehaviourPun
{
    public Transform player;
    public DirectionUIArrow arrow;
    public ChargeController charger;
    public Camera mainCam;

    //public float coneAngle = 100f;
    //public float coneDistance = 5f;
    //public Color coneColor = new Color(0f, 1f, 0f, 0.3f);

    private bool isInputActive = false;
    private bool inputEnabled = false;

    private int currentStep = 1;
    private bool stepCompleted = false;

    //private UnimoEgg grabbedEgg = null;  // 잡은 Egg
    //private float grabLimit = 1f;        // 좌우 이동 제한 (X기준)
    //private Vector3 grabStartPos;        // 잡은 위치 기준

    private bool autoMoveFlag = false;
    private Vector3 autoMoveStartPos;
    private float autoMoveRangeX = 1.0f;   // 좌우 자동 이동 범위
    private float autoMoveSpeed = 2.0f;    // 좌우 이동 속도

    private void RegisterInput()
    {
        ShootingScene.PlayerInputManager.Instance.onTouchPress += HandleTouch;
    }

    private void UnRegisterInput()
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
        ShootingGameManager.Instance.OnGameStarted += RegisterInput;
        ShootingGameManager.Instance.OnGameEnded -= UnRegisterInput;
        player = gameObject.transform;
        charger = gameObject.GetComponent<ChargeController>();
    }

    private void Start()
    {
        if (mainCam == null)
            mainCam = Camera.main;
    }

    private void Update()
    {
        // Step 1: 유니모 좌우 자동 이동
        if (currentStep == 1 && autoMoveFlag)
        {
            float t = Mathf.PingPong(Time.time * autoMoveSpeed, 1f); // 0~1 반복
            Vector3 newPos = transform.position;
            // grabStartPos.x를 중심으로 좌우 grabLimit 범위 내 이동
            newPos.x = Mathf.Lerp(autoMoveStartPos.x - autoMoveRangeX, autoMoveStartPos.x + autoMoveRangeX, t);
            transform.position = newPos;
        }

        #region 평면 교차점 계산하여 Grap-Drag-Drop 할 수 있도록
        //if (grabbedEgg != null)
        //{
        //    Vector2 currentInputPos;

        //    // 현재 입력 위치 가져오기
        //    if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        //    {
        //        currentInputPos = Touchscreen.current.primaryTouch.position.ReadValue();
        //    }
        //    else
        //    {
        //        currentInputPos = Mouse.current.position.ReadValue();
        //    }

        //    // 바닥(XZ 평면, y=0) 정의
        //    Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        //    // 마우스(혹은 터치) 위치로부터 Ray 쏘기
        //    Ray ray = mainCam.ScreenPointToRay(currentInputPos);
        //    // 평면과 Ray가 교차하는 지점 구하기
        //    if (groundPlane.Raycast(ray, out float enter))
        //    {
        //        Vector3 worldPos = ray.GetPoint(enter); // 평면 위 좌표
        //        Vector3 newPos = grabbedEgg.transform.position;
        //        // X 좌표만 제한적으로 이동
        //        newPos.x = Mathf.Clamp(worldPos.x, grabStartPos.x - grabLimit, grabStartPos.x + grabLimit);
        //        // 필요하면 Z 좌표도 제한 가능
        //        // newPos.z = Mathf.Clamp(worldPos.z, grabStartPos.z - grabLimit, grabStartPos.z + grabLimit);
        //        grabbedEgg.transform.position = newPos;
        //    }
        //}
        #endregion

        #region 완전 수직일 때 만 가능한 ScreenToWorldPoint - 카메라 각도에 따른 부정확성
        //if (grabbedEgg != null)
        //{
        //    // 현재 터치/마우스 위치를 XZ 평면으로 변환
        //    Vector3 touchWorldPos = mainCam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, mainCam.transform.position.y - grabStartPos.y));
        //    Vector3 newPos = grabbedEgg.transform.position;

        //    // XZ 좌표만 사용
        //    newPos.x = Mathf.Clamp(touchWorldPos.x, grabStartPos.x - grabLimit, grabStartPos.x + grabLimit);

        //    grabbedEgg.transform.position = newPos;
        //}
        #endregion
    }

    private void HandleTouch(InputAction.CallbackContext ctx)
    {
        if (!inputEnabled || stepCompleted) return;

        Vector2 screenPos;
        if (!TryGetScreenPosition(out screenPos)) return;

        switch (currentStep)
        {
            case 1: // 유니모 터치 단계
                if (ctx.started)
                {
                    autoMoveFlag = false;
                    stepCompleted = true;
                    Debug.Log("Step 1: 터치 종료, 다음 단계");
                }
                break;

            case 2: // 화살표 단계
                if (ctx.started)
                {
                    arrow.Freeze(); // 랜덤 방향 고정
                    stepCompleted = true;
                    Debug.Log("Step 2: 화살표 선택 완료");
                }
                break;

            case 3: // 차징 단계
                if (ctx.started)
                {
                    charger.StopCharge(); // 클릭 시 힘 적용
                    stepCompleted = true;
                    Debug.Log("Step 3: 차징 완료, 힘 적용");

                    var unimo = gameObject.GetComponent<UnimoEgg>();
                    if (unimo != null)
                    {
                        unimo.Shot(arrow.CurrentDir * charger.ChargePower);
                        TurnOffUIVisibility();
                    }
                }
                break;
        }

        //if (!inputEnabled) return;

        //Vector2 screenPos;
        //////Vector3 worldPos = ScreenToWorld(ctx.ReadValue<Vector2>());

        ////// 터치인지 마우스인지 확인해서 위치 가져오기
        ////if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        ////{
        ////    screenPos = Touchscreen.current.primaryTouch.position.ReadValue();   
        ////}
        ////else if (Mouse.current != null)
        ////{
        ////    screenPos = Mouse.current.position.ReadValue(); 
        ////}
        ////else
        ////{
        ////    return;
        ////}

        //if(TryGetScreenPosition(out Vector2 vaildscreenPos))
        //{
        //    screenPos = vaildscreenPos;
        //}
        //else
        //{
        //    return;
        //}

        //#region Unimo와 충돌했을 때
        //Ray ray = mainCam.ScreenPointToRay(screenPos); // screenPos는 Vector2 (스크린 좌표)
        //RaycastHit hit;

        //if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("UnimoEgg"))) // 무한 거리까지 충돌 검사
        //{
        //    Debug.Log("Raycast hit UnumoEgg!");

        //    grabbedEgg = hit.collider.GetComponent<UnimoEgg>();
        //    if (grabbedEgg != null)
        //    {
        //        grabStartPos = grabbedEgg.transform.position; // 시작 위치 저장
        //        Debug.Log("Raycast hit UnimoEgg! Grabbed!");
        //    }

        //    if(ctx.canceled)
        //    {
        //        grabbedEgg = null;
        //        grabStartPos = Vector3.zero;
        //    }
        //}
        //#endregion
        //else
        //{
        //    if (ctx.started) //&& IsWithinCone(screenPos))
        //    {
        //        arrow.Freeze();
        //        charger.StartCharge();
        //        isInputActive = true;
        //    }
        //    else if (ctx.canceled && isInputActive)
        //    {
        //        //var currentEgg = EggManager.Instance.currentUnimoEgg;
        //        var unimo = gameObject.GetComponent<UnimoEgg>();
        //        if (unimo != null)
        //        {
        //            unimo.Shot(arrow.CurrentDir * charger.ChargePower);
        //            TurnOffUIVisibility();
        //        }

        //        charger.StopCharge();
        //        arrow.Resume();
        //        isInputActive = false;
        //        DisableInput();
        //    }
        //}


        ///*if (ctx.started) //&& IsWithinCone(screenPos))
        //{
        //    arrow.Freeze();
        //    charger.StartCharge();
        //    isInputActive = true;
        //}
        //else if (ctx.canceled && isInputActive)
        //{
        //    //var currentEgg = EggManager.Instance.currentUnimoEgg;
        //    var unimo = gameObject.GetComponent<UnimoEgg>();
        //    if (unimo != null)
        //        unimo.Shot(arrow.CurrentDir * charger.ChargePower);

        //    charger.StopCharge();
        //    arrow.Resume();
        //    isInputActive = false;
        //    DisableInput();
        //}*/
    }

    //Screen 좌표를 World 좌표로 변환 - 카메라의 각도와 상관없이
    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        Ray ray = mainCam.ScreenPointToRay(screenPos);              //screenPos(마우스나 터치 위치)를 카메라 기준으로 Ray
        Plane groundPlane = new Plane(Vector3.up, player.position); // y = player.position.y 평면
        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter); // XZ 평면 좌표
        }
        return player.position;
    }

    #region Legarcy 
    //private bool IsWithinCone(Vector2 screenPos)
    //{
    //    // Screen → World 변환
    //    Vector3 worldPos = ScreenToWorld(screenPos);

    //    // XZ 평면 벡터 계산
    //    Vector3 toInput = worldPos - player.position;
    //    toInput.y = 0f;

    //    if (toInput.magnitude > coneDistance)
    //    {
    //        Debug.Log("터치 범위를 벗어났습니다");
    //        return false;
    //    }

    //    if(Vector3.Angle(player.forward, toInput) <= coneAngle * 0.5f)
    //    {
    //        Debug.Log("각도 안에서 터치 입력했습니다");
    //        return true;
    //    }
    //    else
    //    {
    //        Debug.Log("각도 안에서 터치 입력하지 않아서 입력이 취소 됩니다");
    //        return false;
    //    }

    //    //return Vector3.Angle(player.forward, toInput) <= coneAngle * 0.5f;
    //}

    //private void OnDrawGizmos()
    //{
    //    if (player == null) return;

    //    Vector3 pos = player.position;
    //    Vector3 forward = player.forward;

    //    Vector3 leftDir = Quaternion.Euler(0, -coneAngle * 0.5f, 0) * forward;
    //    Vector3 rightDir = Quaternion.Euler(0, coneAngle * 0.5f, 0) * forward;

    //    Gizmos.color = coneColor;

    //    // 원뿔 경계선
    //    Gizmos.DrawLine(pos, pos + leftDir.normalized * coneDistance);
    //    Gizmos.DrawLine(pos, pos + rightDir.normalized * coneDistance);

    //    // 내부 라인
    //    int steps = 10;
    //    for (int i = 0; i <= steps; i++)
    //    {
    //        float t = i / (float)steps;
    //        Vector3 dir = Quaternion.Euler(0, -coneAngle * 0.5f + t * coneAngle, 0) * forward;
    //        Gizmos.DrawLine(pos, pos + dir.normalized * coneDistance);
    //    }
    //}
    #endregion


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
        if (charger != null)
            charger.chargeSlider.gameObject.SetActive(photonView.IsMine);

        if (arrow != null)
            arrow.gameObject.SetActive(photonView.IsMine);
    }

    public void TurnOffUIVisibility()
    {
        if (charger != null)
            charger.chargeSlider.gameObject.SetActive(false);

        if (arrow != null)
            arrow.gameObject.SetActive(false);
    }

    [PunRPC]
    private void RPC_UpdateUIVisibility()
    {
        if (charger != null)
            charger.chargeSlider.gameObject.SetActive(photonView.IsMine);

        if (arrow != null)
            arrow.gameObject.SetActive(photonView.IsMine);
    }

    private bool TryGetScreenPosition(out Vector2 screenPos)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }

        screenPos = default;        //0,0 기존값을 리턴하기는 한테 입력이 없을리가 없으니깐
        return false; // 입력 없음
    }

    public IEnumerator StepRoutine()
    {
        while (currentStep <= 3)
        {
            stepCompleted = false;
            float endTime = Time.time + 5f; // 각 단계 5초 제한


            // Step 시작 시 UI 활성화  -- 한번만 활성화 하면 되니깐 클릭을 하면 이벤트 처리가 오도록?      //문제의 while switch 부분
            switch (currentStep)
            {
                case 1: // 유니모 좌우이동
                    autoMoveStartPos = transform.position;
                    autoMoveFlag = true;
                    break;

                case 2: // 화살표 단계
                    if (arrow != null && !arrow.gameObject.activeSelf)
                        arrow.gameObject.SetActive(true); // 활성화하면 내부 로직으로 랜덤 회전 시작
                    break;

                case 3: // 차징 단계
                    if (charger != null && !charger.gameObject.activeSelf)
                        charger.gameObject.SetActive(true); // 활성화하면 힘이 자동으로 왔다갔다
                    break;
            }

            while (Time.time < endTime && !stepCompleted)
            {
                yield return null;
            }

            // 5초 초과 시 자동 처리 - 주어진 Step시간이 지날 시 자동 처리
            if (!stepCompleted)
            {
                switch (currentStep)
                {
                    case 1:
                        autoMoveFlag = false;
                        Debug.Log("Step 1: 시간 초과, 자동 진행");
                        break;
                    case 2:
                        arrow.Freeze();
                        Debug.Log("Step 2: 시간 초과, 랜덤 방향 고정");
                        break;
                    case 3:
                        charger.StopCharge();
                        Debug.Log("Step 3: 시간 초과, 기본 힘 적용");
                        break;
                }
            }

            Debug.Log($"Step {currentStep} 종료");
            currentStep++;
        }

    }
}