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
    public float chargeMax = 5f;
    public float chargeSpeed = 10f;

    [Header("References")]
    public Transform firePoint;       // 발사 위치
    public GameObject projectilePrefab;
    public Slider chargeSlider;

    private float currentAngle;
    private Vector3 dir;
    private float chargePower;


    public void OnTouchStart(Vector2 touchPos)
    {
        // 스크린 → 월드 변환
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));

        // 방향 벡터 계산
        Vector3 start = ShootingGameManager.Instance.currentUnimo.transform.position;
        Vector3 dir = worldPos - start;
        dir.y = 0f; // y는 무시 (XZ 평면만 고려)

        // 방향 시각화
        Debug.DrawRay(start, dir.normalized * 5.0f, Color.blue, 10.0f);

        Debug.Log("방향표시등을 클릭함");
    }

    public void OnTouch(float Timer)
    {
        Debug.Log("방향표시등을 클릭중");
    }

    public void OnTouchEnd()
    {
        Debug.Log("방향표시등에서 손을 땜");
    }
}
