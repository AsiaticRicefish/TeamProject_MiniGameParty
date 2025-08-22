using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DirectionSign : MonoBehaviour
{
    [Header("Direction Settings")]
    public float angleRange = 100f;   // �� ���� (��: 100��)
    public float angleSpeed = 30f;     // �¿� �̵� �ӵ�

    [Header("Charge Settings")]
    public float chargeMin = 0f;
    public float chargeMax = 5f;
    public float chargeSpeed = 10f;

    [Header("References")]
    public Transform firePoint;       // �߻� ��ġ
    public GameObject projectilePrefab;
    public Slider chargeSlider;

    private float currentAngle;
    private Vector3 dir;
    private float chargePower;


    public void OnTouchStart(Vector2 touchPos)
    {
        // ��ũ�� �� ���� ��ȯ
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));

        // ���� ���� ���
        Vector3 start = ShootingGameManager.Instance.currentUnimo.transform.position;
        Vector3 dir = worldPos - start;
        dir.y = 0f; // y�� ���� (XZ ��鸸 ���)

        // ���� �ð�ȭ
        Debug.DrawRay(start, dir.normalized * 5.0f, Color.blue, 10.0f);

        Debug.Log("����ǥ�õ��� Ŭ����");
    }

    public void OnTouch(float Timer)
    {
        Debug.Log("����ǥ�õ��� Ŭ����");
    }

    public void OnTouchEnd()
    {
        Debug.Log("����ǥ�õ�� ���� ��");
    }
}
