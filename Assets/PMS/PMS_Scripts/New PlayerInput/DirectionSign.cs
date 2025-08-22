using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShootingScene;

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
        if (isSwing)
        {
            // forward 기준
            Vector3 forward = ShootingGameManager.Instance.currentUnimo.transform.forward;

            // 시간에 따라 -1 ~ 1로 반복되는 값
            float t = Mathf.Sin(Time.time * speed);

            // -maxAngle ~ maxAngle 범위의 각도
            float angle = t * maxAngle;

            // 회전된 방향 구하기
            currentDir = Quaternion.AngleAxis(angle, Vector3.up) * forward;

            // 왼쪽 50도 방향
            Vector3 leftDir = Quaternion.AngleAxis(-50f, Vector3.up) * forward;

            // 오른쪽 50도 방향
            Vector3 rightDir = Quaternion.AngleAxis(50f, Vector3.up) * forward;

            // 디버그 라인 확인
            //Debug.DrawRay(player.position, forward * 5, Color.white);  // forward
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, leftDir * 5, Color.red);    // left 50도
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, rightDir * 5, Color.blue);  // right 50도

            //움직이는 Ray
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, currentDir * lineLength, Color.yellow);
        }

        if (isPress)
        {
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, currentDir * lineLength, Color.yellow);
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
        /*startPos = ShootingGameManager.Instance.currentUnimo.transform.position;
        Vector3 forward = ShootingGameManager.Instance.currentUnimo.transform.forward;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));
        dir = worldPos - startPos;
        dir.y = 0f;
        dir.Normalize();*/

        dir = currentDir;

        Vector3 forward = ShootingGameManager.Instance.currentUnimo.transform.forward;

        float angle = Vector3.Angle(forward, dir);

        if (angle > angleRange/2) // 허용 각도
        {
            Debug.Log("각도 범위 벗어남");
            return false;
        }

        isAngleCheck = true;
        return true;
    }

    [SerializeField] private Transform player; // 플레이어 기준 Transform
    [SerializeField] private float maxAngle = 50f;  // 왼쪽/오른쪽 최대 각도
    [SerializeField] private float speed = 2f;      // 움직이는 속도
    [SerializeField] private float lineLength = 5f; // 디버그 라인 길이

    private bool isSwing = true;
    private Vector3 currentDir;

    private void Start()
    {
        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.onTouched += SetDir;
    }

    private void OnDisable()
    {
        PlayerInputManager.Instance.onTouched -= SetDir;
    }

    private void SetDir()
    {
        isSwing = false;
    }
}
