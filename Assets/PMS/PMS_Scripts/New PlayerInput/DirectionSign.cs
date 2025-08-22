using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShootingScene;

public class DirectionSign : MonoBehaviour
{
    [Header("Direction Settings")]
    public float angleRange = 100f;   // �� ���� (��: 100��)
    public float angleSpeed = 30f;     // �¿� �̵� �ӵ�

    [Header("Charge Settings")]
    public float chargeMin = 0f;
    public float chargeMax = 25f;
    //public float chargeSpeed = 10f;

    [Header("References")]
    public Transform firePoint;       // �߻� ��ġ
    public GameObject projectilePrefab;
    public Slider chargeSlider;

    private float currentAngle;
    private Vector3 startPos;
    private Vector3 dir;
    private float chargePower;

    private bool isPress = false;
    private float pressStartTime;   // ���� ���� ���
    private bool isAngleCheck = false;

    private void Update()
    {
        if (isSwing)
        {
            // forward ����
            Vector3 forward = ShootingGameManager.Instance.currentUnimo.transform.forward;

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
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, leftDir * 5, Color.red);    // left 50��
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, rightDir * 5, Color.blue);  // right 50��

            //�����̴� Ray
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, currentDir * lineLength, Color.yellow);
        }

        if (isPress)
        {
            Debug.DrawRay(ShootingGameManager.Instance.currentUnimo.transform.position, currentDir * lineLength, Color.yellow);
            // period = �� ����Ŭ �ð� (0��Max��0)
            float chargePeriod = 2f;
            // ���� �� ����ð�
            float elapsed = Time.time - pressStartTime;

            //chargePower = Mathf.PingPong(elapsed * chargeSpeed, 1f) * chargeMax;
            float t = Mathf.PingPong(elapsed / (chargePeriod / 2f), 1f);
            chargePower = t * chargeMax;

            // Slider ǥ��
            if (chargeSlider != null)
                chargeSlider.value = chargePower / chargeMax;
        }
    }

    public void OnTouchStart(Vector2 touchPos)
    {
        if (!CheckAngle(touchPos)) return;

        if (chargeSlider != null)
            chargeSlider.value = 0f;

        pressStartTime = Time.time; // ���� �ð� ����
        chargePower = 0f;  
        isPress = true;

        /*// ��ũ�� �� ���� ��ȯ
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));

        // ���� ���� ���
        Vector3 start = ShootingGameManager.Instance.currentUnimo.transform.position;
        Vector3 dir = worldPos - start;
        dir.y = 0f; // y�� ���� (XZ ��鸸 ���)
        dir.Normalize();*/

        // ���� �ð�ȭ
        //Debug.DrawRay(startPos, dir.normalized * 5.0f, Color.blue, 3.0f);

        Debug.Log("����ǥ�õ��� Ŭ����");
    }

    public void OnTouch(float Timer)
    {
        if (isAngleCheck)
        {
            Debug.Log("����ǥ�õ��� Ŭ����");
        }
    }

    public void OnTouchEnd()
    {
        if (isAngleCheck)
        {
            Debug.Log($"���� ũ�� {chargePower}");
            Debug.Log("����ǥ�õ�� ���� ��");
            ShootingGameManager.Instance.currentUnimo.Shot(dir * chargePower);
            //���۾��Ŀ�
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

        if (angle > angleRange/2) // ��� ����
        {
            Debug.Log("���� ���� ���");
            return false;
        }

        isAngleCheck = true;
        return true;
    }

    [SerializeField] private Transform player; // �÷��̾� ���� Transform
    [SerializeField] private float maxAngle = 50f;  // ����/������ �ִ� ����
    [SerializeField] private float speed = 2f;      // �����̴� �ӵ�
    [SerializeField] private float lineLength = 5f; // ����� ���� ����

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
