using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DirectionSign : MonoBehaviour
{
    [Header("Direction Settings")]
    public float angleRange = 100f;   // 총 각도 (예: 100도)
    public float angleSpeed = 30f;     // 좌우 이동 속도

    [Header("Charge Settings")]
    public float chargeMin = 0f;
    public float chargeMax = 25f;
    //public float chargeSpeed = 10f;

    [Header("References")]
    public Transform firePoint;       // 발사 위치
    public GameObject projectilePrefab;
    public Slider chargeSlider;

    private float currentAngle;
    private Vector3 startPos;
    private Vector3 dir;
    private float chargePower;

    private bool isPress = false;
    private float pressStartTime;   // 누른 시점 기록
    private bool isAngleCheck = false;

    private void Update()
    {
        if(isPress)
        {
            // period = 한 사이클 시간 (0→Max→0)
            float chargePeriod = 2f;
            // 누른 후 경과시간
            float elapsed = Time.time - pressStartTime;

            //chargePower = Mathf.PingPong(elapsed * chargeSpeed, 1f) * chargeMax;
            float t = Mathf.PingPong(elapsed / (chargePeriod / 2f), 1f);
            chargePower = t * chargeMax;

            // Slider 표시
            if (chargeSlider != null)
                chargeSlider.value = chargePower / chargeMax;
        }
    }

    public void OnTouchStart(Vector2 touchPos)
    {
        if (!CheckAngle(touchPos)) return;

        if (chargeSlider != null)
            chargeSlider.value = 0f;

        pressStartTime = Time.time; // 시작 시각 저장
        chargePower = 0f;  
        isPress = true;

        /*// 스크린 → 월드 변환
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));

        // 방향 벡터 계산
        Vector3 start = ShootingGameManager.Instance.currentUnimo.transform.position;
        Vector3 dir = worldPos - start;
        dir.y = 0f; // y는 무시 (XZ 평면만 고려)
        dir.Normalize();*/

        // 방향 시각화
        //Debug.DrawRay(startPos, dir.normalized * 5.0f, Color.blue, 3.0f);

        Debug.Log("방향표시등을 클릭함");
    }

    public void OnTouch(float Timer)
    {
        if (isAngleCheck)
        {
            Debug.Log("방향표시등을 클릭중");
        }
    }

    public void OnTouchEnd()
    {
        if (isAngleCheck)
        {
            Debug.Log($"힘의 크기 {chargePower}");
            Debug.Log("방향표시등에서 손을 땜");
            ShootingGameManager.Instance.currentUnimo.Shot(dir * chargePower);
            //다작업후에
            ResetData();

            if (chargeSlider != null)
                chargeSlider.value = 0f;
        }
    }

    private void ResetData()
    {
        chargePower = 0;
        isPress = false;
        isAngleCheck = false;
    }

    private bool CheckAngle(Vector2 touchPos)
    {
        startPos = ShootingGameManager.Instance.currentUnimo.transform.position;
        Vector3 forward = ShootingGameManager.Instance.currentUnimo.transform.forward;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));
        dir = worldPos - startPos;
        dir.y = 0f;
        dir.Normalize();

        float angle = Vector3.Angle(forward, dir);

        if (angle > angleRange/2) // 허용 각도
        {
            Debug.Log("각도 범위 벗어남");
            return false;
        }

        isAngleCheck = true;
        return true;
    }
}
