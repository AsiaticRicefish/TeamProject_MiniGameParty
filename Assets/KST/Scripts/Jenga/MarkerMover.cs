using UnityEngine;

public class MarkerMover : MonoBehaviour
{
    [SerializeField] Transform marker;
    [SerializeField] float offsetX = 0.25f; // 블록 +X(정면)으로 얼마나 떨어질지
    [SerializeField] float offsetY = 0f;     // 필요하면 살짝 위/아래 보정
    [SerializeField] float offsetZ = 0f;     // 필요하면 좌/우 보정

    /// <summary>
    /// 블록의 로컬 +X 정면으로 (offsetX, offsetY, offsetZ)만큼 이동시켜 마커를 놓고,
    /// 마커의 z축이 블록의 X축을 바라보도록 회전.
    /// </summary>
    public void PlaceAtBlockFront(Transform block)
    {

        Vector3 localOffset = new(offsetX, offsetY, offsetZ);

        marker.SetPositionAndRotation(
            block.TransformPoint(localOffset),  // 위치: 블록 로컬 좌표 지점을 월드 좌표로 변환
            Quaternion.LookRotation(-block.right, block.up)); // 회전: 마커의 z축을 블록의 x축을 바라보도록로 정렬
    }
}
