using System;
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

    // 간단한 이벤트들
    public event Action OnStep1Started;    // 유니모 이동 시작
    public event Action OnStep1Completed;  // 유니모 이동 완료
    public event Action OnStep2Started;    // 화살표 시작  
    public event Action OnStep2Completed;  // 화살표 완료
    public event Action OnStep3Started;    // 차징 시작
    public event Action OnStep3Completed;  // 차징 완료
    public event Action OnAllCompleted;    // 모든 단계 완료

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
        Debug.Log("EnableInpute처리 완료 - Input 활성화!");
        inputEnabled = true;
        //UI 마이턴 시작 뛰우기
        StartStep1(); // 첫 번째 단계 시작!
    }
    public void DisableInput() => inputEnabled = false;

    private void Awake()
    {
        ShootingGameManager.Instance.OnGameStarted += RegisterInput;
        ShootingGameManager.Instance.OnGameEnded -= UnRegisterInput;
        player = gameObject.transform;
        charger = gameObject.GetComponent<ChargeController>();

        SetupEvents();
    }

    private void Start()
    {
        if (mainCam == null)
            mainCam = Camera.main;
    }

    private void SetupEvents()
    {
        // Step1 유니모 좌우 자동이동
        OnStep1Started += () => {
            Debug.Log("Step 1: 유니모 이동 시작");
            autoMoveStartPos = transform.position;
            autoMoveFlag = true;
            inputEnabled = true; 
        };

        OnStep1Completed += () => {
            Debug.Log("Step 1: 유니모 이동 완료");
            autoMoveFlag = false;
            inputEnabled = false;
            StartStep2();
        };

        // Step2 방향 시스템
        OnStep2Started += () => {
            Debug.Log("Step 2: 화살표 선택 시작");
            if (arrow != null && photonView.IsMine)
                arrow.gameObject.SetActive(true);
            inputEnabled = true;
        };

        OnStep2Completed += () => {
            Debug.Log("Step 2: 화살표 선택 완료");
            inputEnabled = false;
            StartStep3();

        };

        // Step3 차징 시스템
        OnStep3Started += () => {
            Debug.Log("Step 3: 차징 시작");
            if (charger != null && photonView.IsMine)
                charger.gameObject.SetActive(true);
            inputEnabled = true;
        };

        OnStep3Completed += () => {
            Debug.Log("Step 3: 차징 완료");
            inputEnabled = false;
            var unimo = gameObject.GetComponent<UnimoEgg>();

            unimo.Shot(arrow.CurrentDir * charger.ChargePower);
            FinishAllSteps();
        };

        // 최종 완료
        OnAllCompleted += () => {
            Debug.Log("모든 단계 완료!");
            //StartCoroutine(HandleShotAndWait());
        };
    }

    /*private IEnumerator HandleShotAndWait()
    {
        var unimo = gameObject.GetComponent<UnimoEgg>();

        unimo.Shot(arrow.CurrentDir * charger.ChargePower);

        // 멈출 때까지 기다림
        yield return StartCoroutine(unimo.WaitForStop());

        Debug.Log("UnimoEgg 멈춘 후 처리!");

        TurnManager.Instance.RequestMyTurnEnd();
    }*/

    // 단계 시작 함수들 - 기존 타이머 정지 후 새 타이머 시작
    private void StartStep1()
    {
        StopCurrentTimeout(); // 기존 타이머 정지
        currentStep = 1;
        stepCompleted = false;
        OnStep1Started?.Invoke();
        currentTimeoutCoroutine = StartCoroutine(StepTimeout(5f));
    }

    private void StartStep2()
    {
        StopCurrentTimeout(); // 기존 타이머 정지
        currentStep = 2;
        stepCompleted = false;
        OnStep2Started?.Invoke();
        currentTimeoutCoroutine = StartCoroutine(StepTimeout(5f));
    }

    private void StartStep3()
    {
        StopCurrentTimeout(); // 기존 타이머 정지
        currentStep = 3;
        stepCompleted = false;
        OnStep3Started?.Invoke();
        currentTimeoutCoroutine = StartCoroutine(StepTimeout(5f));
    }

    private void StopCurrentTimeout()
    {
        if (currentTimeoutCoroutine != null)
        {
            StopCoroutine(currentTimeoutCoroutine);
            currentTimeoutCoroutine = null;
        }
    }

    private void FinishAllSteps()
    {
        OnAllCompleted?.Invoke();
        DisableInput();
    }

    // 간단한 타임아웃 처리 - 타이머 중복 방지
    private Coroutine currentTimeoutCoroutine;

    private IEnumerator StepTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Debug.Log($"Step {currentStep}: 시간 초과, 자동 진행");
        CompleteCurrentStep();
    }

    // 현재 단계 완료 처리 - 타이머도 정지
    public void CompleteCurrentStep()
    {
        if (stepCompleted) return;

        StopCurrentTimeout(); // 타이머 정지
        stepCompleted = true;
        inputEnabled = false;  // 완료 직후 잠깐 입력 막음

        switch (currentStep)
        {
            case 1: OnStep1Completed?.Invoke(); break;
            case 2:
                arrow?.Freeze(); // 화살표 고정
                OnStep2Completed?.Invoke();
                break;
            case 3:
                //charger?.StopCharge(); // 차징 정지
                OnStep3Completed?.Invoke();
                break;
        }
    }

    private void Update()
    {
        // Step 1: 유니모 좌우 자동 이동
        if (currentStep == 1 && autoMoveFlag && inputEnabled)
        {
            float t = Mathf.PingPong(Time.time, 1f); // 0~1 반복
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

        if (ctx.started) // 어느 단계에서든 터치하면 즉시 완료
        {
            Debug.Log($"Step {currentStep}: 터치로 즉시 완료");
            CompleteCurrentStep();
        }


        #region Legacy_Code 이전 Input 터치
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
        #endregion
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

    #region Legarcy 플레이어 앞의 각도 체크
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

    public void TurnOffUIVisibility()
    {
        if (charger != null)
            charger.chargeSlider.gameObject.SetActive(false);

        if (arrow != null)
            arrow.gameObject.SetActive(false);
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




}