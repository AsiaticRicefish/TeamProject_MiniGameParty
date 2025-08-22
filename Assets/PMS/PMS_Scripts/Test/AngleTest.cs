using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngleTest : MonoBehaviour
{
    [SerializeField] private Transform player; // �÷��̾� ���� Transform
    [SerializeField] private float maxAngle = 50f;  // ����/������ �ִ� ����
    [SerializeField] private float speed = 2f;      // �����̴� �ӵ�
    [SerializeField] private float lineLength = 5f; // ����� ���� ����

    private Vector3 currentDir; 
    void Update()
    {
        // forward ����
        Vector3 forward = player.forward;

        // �ð��� ���� -1 ~ 1�� �ݺ��Ǵ� ��
        float t = Mathf.Sin(Time.time * speed);

        // -maxAngle ~ maxAngle ������ ����
        float angle = t * maxAngle;

        // ȸ���� ���� ���ϱ�
        currentDir = Quaternion.AngleAxis(angle, Vector3.up) * forward;

        // ���� 50�� ����
        Vector3 leftDir = Quaternion.AngleAxis(-50f, Vector3.up) * forward;

        // ������ 50�� ����
        Vector3 rightDir = Quaternion.AngleAxis(50f, Vector3.up) * forward;

        // ����� ���� Ȯ��
        //Debug.DrawRay(player.position, forward * 5, Color.white);  // forward
        Debug.DrawRay(player.position, leftDir * 5, Color.red);    // left 50��
        Debug.DrawRay(player.position, rightDir * 5, Color.blue);  // right 50��

        //�����̴� Ray
        Debug.DrawRay(player.position, currentDir * lineLength, Color.yellow);
    }
}
