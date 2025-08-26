using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [SerializeField] CinemachineVirtualCamera baseCam; //�⺻ ī�޶� ��ġ
    [SerializeField] CinemachineVirtualCamera focusCam; //���� �� ī�޶� ��ġ

    int basePriority, focusPriority; //ī�޶� �켱����

    void Awake()
    {
        basePriority = 10;
        focusPriority = 0;
    }

    // ��Ŀ ��ġ�� ����
    public void Focus(Transform marker)
    {
        focusCam.transform.SetPositionAndRotation(marker.position, marker.rotation);

        //ī�޶� �켱���� ���� (focusCam> baseCam)
        baseCam.Priority = basePriority;
        focusCam.Priority = basePriority + 1;
    }

    // ���� ī�޶�� �����ϱ�
    public void BackToOrigin()
    {
        //ī�޶� �켱���� ���� (baseCam > focusCam)
        baseCam.Priority = basePriority + 1;
        focusCam.Priority = focusPriority;
    }
}
