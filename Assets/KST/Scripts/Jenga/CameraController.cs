using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [SerializeField] CinemachineVirtualCamera baseCam; //기본 카메라 위치
    [SerializeField] CinemachineVirtualCamera focusCam; //줌인 시 카메라 위치

    int basePriority, focusPriority; //카메라 우선순위

    void Awake()
    {
        basePriority = 10;
        focusPriority = 0;
    }

    // 마커 위치로 줌인
    public void Focus(Transform marker)
    {
        focusCam.transform.SetPositionAndRotation(marker.position, marker.rotation);

        //카메라 우선순위 변경 (focusCam> baseCam)
        baseCam.Priority = basePriority;
        focusCam.Priority = basePriority + 1;
    }

    // 원래 카메라로 복귀하기
    public void BackToOrigin()
    {
        //카메라 우선순위 변경 (baseCam > focusCam)
        baseCam.Priority = basePriority + 1;
        focusCam.Priority = focusPriority;
    }
}
