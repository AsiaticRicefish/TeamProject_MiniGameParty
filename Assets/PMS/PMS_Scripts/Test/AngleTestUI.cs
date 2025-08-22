using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngleTestUI : MonoBehaviour
{
    [SerializeField] private RectTransform center;
    [SerializeField] private float maxAngle = 50f; // ����/������ �ִ� ����
    [SerializeField] private float speed = 2f;     // �����̴� �ӵ�

    void Update()
    {
        // -1 ~ 1 �ݺ�
        float t = Mathf.Sin(Time.time * speed);

        // ���� ���
        float angle = t * maxAngle;

        // Z�� ȸ�� ���� (UI�� z�� ȸ��)
        center.localRotation = Quaternion.Euler(0, 0, angle);
    }
}
