using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ShootingScene;

public class AngleTest : MonoBehaviour
{
    [SerializeField] private Transform player; // 플레이어 기준 Transform
    [SerializeField] private float maxAngle = 50f;  // 왼쪽/오른쪽 최대 각도
    [SerializeField] private float speed = 2f;      // 움직이는 속도
    [SerializeField] private float lineLength = 5f; // 디버그 라인 길이

    private bool isSwing = true;
    private Vector3 currentDir;

    private void Start()
    {
        if (PlayerInputManager.Instance != null)
        {

        }
            //PlayerInputManager.Instance.onTouched += SetDir;
    }

    private void OnDisable()
    {
        //PlayerInputManager.Instance.onTouched -= SetDir;
    }

    void Update()
    {
        if (isSwing)
        {
            // forward 기준
            Vector3 forward = player.forward;

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
            Debug.DrawRay(player.position, leftDir * 5, Color.red);    // left 50도
            Debug.DrawRay(player.position, rightDir * 5, Color.blue);  // right 50도

            //움직이는 Ray
            Debug.DrawRay(player.position, currentDir * lineLength, Color.yellow);
        }
        else
        {
            Debug.DrawRay(player.position, currentDir * lineLength, Color.yellow);
        }
    }

    private void SetDir()
    {
        isSwing = false;
    }
}
