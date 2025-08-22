using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngleTestUI : MonoBehaviour
{
    [SerializeField] private RectTransform center;
    [SerializeField] private float maxAngle = 50f; // 왼쪽/오른쪽 최대 각도
    [SerializeField] private float speed = 2f;     // 움직이는 속도

    void Update()
    {
        // -1 ~ 1 반복
        float t = Mathf.Sin(Time.time * speed);

        // 각도 계산
        float angle = t * maxAngle;

        // Z축 회전 적용 (UI는 z축 회전)
        center.localRotation = Quaternion.Euler(0, 0, angle);
    }
}
