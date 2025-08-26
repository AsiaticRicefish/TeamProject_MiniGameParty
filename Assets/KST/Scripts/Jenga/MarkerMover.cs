using UnityEngine;

public class MarkerMover : MonoBehaviour
{
    [SerializeField] Transform marker;
    [SerializeField] float offsetX = 0.25f; // ��� +X(����)���� �󸶳� ��������
    [SerializeField] float offsetY = 0f;     // �ʿ��ϸ� ��¦ ��/�Ʒ� ����
    [SerializeField] float offsetZ = 0f;     // �ʿ��ϸ� ��/�� ����

    /// <summary>
    /// ����� ���� +X �������� (offsetX, offsetY, offsetZ)��ŭ �̵����� ��Ŀ�� ����,
    /// ��Ŀ�� z���� ����� X���� �ٶ󺸵��� ȸ��.
    /// </summary>
    public void PlaceAtBlockFront(Transform block)
    {

        Vector3 localOffset = new(offsetX, offsetY, offsetZ);

        marker.SetPositionAndRotation(
            block.TransformPoint(localOffset),  // ��ġ: ��� ���� ��ǥ ������ ���� ��ǥ�� ��ȯ
            Quaternion.LookRotation(-block.right, block.up)); // ȸ��: ��Ŀ�� z���� ����� x���� �ٶ󺸵��Ϸ� ����
    }
}
