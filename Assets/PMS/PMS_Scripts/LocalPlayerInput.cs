using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using ShootingScene.ShootingGame;

public class LocalPlayerInput : MonoBehaviourPun
{
    public Transform player;
    public DirectionUIArrow arrow;
    public GameObject arrowRangeImage;
    public ChargeController charger;
    public Camera mainCam;

    private float stepStartTime = 0;
    private float duration = 0.9f;

    private float stepLimitTime = 5f;
    private bool inputEnabled = false;
    private bool stepCompleted = false;

    private int currentStepIndex = 0;

    // 간단한 타임아웃 처리 - 타이머 중복 방지
    private Coroutine currentTimeoutCoroutine;

    private Vector3 autoMoveStartPos;
    private float autoMoveRangeX = 3.0f;   // 좌우 자동 이동 범위
    private bool autoMoveFlag = false;

    private List<InputStep> steps;

    public void EnableInput()
    {
        // 구독은 내 소유일 때만 하도록 (일관성)
        if (photonView.IsMine)
        {
            Debug.Log("EnableInput 처리 완료 - Input 활성화!");

            if (ShootingScene.PlayerInputManager.Instance != null)
                ShootingScene.PlayerInputManager.Instance.onTouchPress += HandleTouch;

            stepCompleted = false;
            inputEnabled = true;
            StartStep(0); // 첫 번째 단계 (steps[0]) 시작
        }  
    }
    public void DisableInput()
    {
        // 구독 해제도 소유 확인 후
        if (photonView.IsMine)
        {
            if (ShootingScene.PlayerInputManager.Instance != null)
                ShootingScene.PlayerInputManager.Instance.onTouchPress -= HandleTouch;
        }

        inputEnabled = false;
    }

    private void Awake()
    {
        player = gameObject.transform;

        SetupSteps();
    }

    private void StartStep(int index)
    {
        StopCurrentTimeout();
        currentStepIndex = index;
        stepCompleted = false;

        var step = steps[index];
        step.OnStart?.Invoke();

        // Step1일 때 자동이동 기준점 저장
        if (steps[index].StepNumber == 1)
        {
            stepStartTime = Time.time;
            autoMoveStartPos = transform.position;
            autoMoveFlag = true;
        }
        else
        {
            autoMoveFlag = false;
        }

        currentTimeoutCoroutine = StartCoroutine(StepTimeout(stepLimitTime));
    }


    private void OnDisable()
    {
        if (photonView.IsMine)
        {
            Debug.Log("구독 해제");
            if(ShootingScene.PlayerInputManager.Instance != null)
            {
                ShootingScene.PlayerInputManager.Instance.onTouchPress -= HandleTouch;
            }
            else
            {
                Debug.LogWarning("구독해제가 안됬어요");
            }

            //타이머 정지를 모두에게 알리기
            NotifyStopCountdown(true);

            if (currentTimeoutCoroutine != null)
            {
                StopCoroutine(currentTimeoutCoroutine);
                currentTimeoutCoroutine = null;
            }
        }
    }

    //return pool 데이터 리셋 함수
    public void Initialize()
    {
        stepStartTime = 0;
        inputEnabled = false;
        stepCompleted = false;
        currentStepIndex = 0;
        autoMoveFlag = false;
        autoMoveStartPos = Vector3.zero;

        arrow.Initialize();
        charger.Initialize();

        if (arrow != null) arrow.gameObject.SetActive(false);
        if (charger != null) charger.gameObject.SetActive(false);

        if (currentTimeoutCoroutine != null)
        {
            StopCoroutine(currentTimeoutCoroutine);
            currentTimeoutCoroutine = null;
        }
    }

    private void SetupSteps()
    {
        steps = new List<InputStep>
        {
            new InputStep
            {
                StepNumber = 1,
                OnStart = () =>
                {
                    Debug.Log("Step 1 시작: 유니모 이동");
                    inputEnabled = true;
                },
                OnComplete = () =>
                {
                    Debug.Log("Step 1 완료");
                     autoMoveFlag = false;
                    inputEnabled = false;
                }
            },
            new InputStep
            {
                StepNumber = 2,
                OnStart = () =>
                {
                    Debug.Log("Step 2 시작: 화살표 표시");
                    if (arrow != null && arrowRangeImage != null && photonView.IsMine)
                    {
                        arrow.gameObject.SetActive(true);
                        arrowRangeImage.SetActive(true);
                    }
                    inputEnabled = true;
                },
                OnComplete = () =>
                {
                    Debug.Log("Step 2 완료");
                    arrow?.Freeze();  // 방향 고정
                    if (arrowRangeImage != null && photonView.IsMine)
                        arrowRangeImage.SetActive(false);
                    inputEnabled = false;
                }
            },
            new InputStep
            {
                StepNumber = 3,
                OnStart = () =>
                {
                    Debug.Log("Step 3 시작: 차징");
                    if (charger != null && photonView.IsMine)
                    {
                        charger.chargeSlider.gameObject.SetActive(true);
                        charger.StartCharge();
                    }
                    inputEnabled = true;
                },
                OnComplete = () =>
                {
                    Debug.Log("Step 3 완료: 발사");
                    var unimo = GetComponent<UnimoEgg>();
                    unimo?.Shot(arrow.CurrentDir * charger.ChargePower);

                    if(arrow != null && photonView.IsMine)
                    {
                        arrow.gameObject.SetActive(false);
                    }

                    if (charger != null && photonView.IsMine)
                    {
                        charger.chargeSlider.gameObject.SetActive(false);
                        charger.StopCharge();
                    }
                    inputEnabled = false;
                }
            }
        };
    }

    private void StopCurrentTimeout()
    {
        if (currentTimeoutCoroutine != null)
        {
            //타이머 정지를 모두에게 알리기
            NotifyStopCountdown();
            StopCoroutine(currentTimeoutCoroutine);
            currentTimeoutCoroutine = null;
        }
    }

    private IEnumerator StepTimeout(float seconds)
    {
        NotifyStartCountdown(seconds);
        yield return new WaitForSeconds(seconds);

        Debug.Log($"Step {steps[currentStepIndex].StepNumber} 시간 초과 → 자동 완료");
        CompleteStep();
    }

    private void CompleteStep()
    {
        if (stepCompleted) return;

        StopCurrentTimeout();
        stepCompleted = true;
        inputEnabled = false;

        var step = steps[currentStepIndex];
        step.OnComplete?.Invoke();

        // 다음 단계 있으면 진행
        if (currentStepIndex + 1 < steps.Count)
        {
            StartStep(currentStepIndex + 1);
        }
        else
        {
            Debug.Log("모든 단계 완료!");
            NotifyStopCountdown(true);
            DisableInput();
        }
    }

    private void Update()
    {
        // Step 1: 유니모 좌우 자동 이동
        if (currentStepIndex == 0 && inputEnabled)
        {
            float elapsed = Time.time - stepStartTime; // 스텝 시작 시간 기준
            float t = Mathf.PingPong(elapsed/ duration, 1f); // 0~설정값 반복
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
            Debug.Log($"Step {currentStepIndex}: 터치로 즉시 완료");
            CompleteStep();
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

    #region Screen 좌표를 World 좌표로 변환 - 카메라의 각도와 상관없이
    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);              //screenPos(마우스나 터치 위치)를 카메라 기준으로 Ray
        Plane groundPlane = new Plane(Vector3.up, player.position); // y = player.position.y 평면
        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter); // XZ 평면 좌표
        }
        return player.position;
    }
    #endregion

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


    #region CountDown 싱크 로직
    
    public void NotifyStartCountdown(float durationSec)
    {
        //double now = PhotonNetwork.Time;
        //double lead = 0.3f;
        //double startAt = now + lead;
        //double endAt = startAt + durationSec;
        Debug.Log("NotifyStartCountdown 카운트 다운 호출");
        photonView.RPC(nameof(RPC_StartCountDown), RpcTarget.All, durationSec);
    }
    
    public void NotifyStopCountdown(bool close = false)
    {
        Debug.Log("NotifyStopCountdown 카운트 다운 호출");
        photonView.RPC(nameof(RPC_StopCountDown), RpcTarget.All, close);
    }

    [PunRPC]
    private void RPC_StartCountDown(float durationSec)
    {
        Debug.Log("StartCountDown RPC 호출");
        // NetworkTimer를 통해 타이머 시작
        NetworkTimer.Instance.OnStartTimer(durationSec);
        //ShootingUIManager.Instance.StartCountDown(startAt, endAt);
    }
    
    [PunRPC]
    private void RPC_StopCountDown(bool close)
    {
        Debug.Log("StopCountDown RPC 호출");
        // NetworkTimer 정지
        NetworkTimer.Instance?.StopTimer();
        //ShootingUIManager.Instance?.StopCountDown(close);
    }
    #endregion
}