using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class UnimoEgg : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 startTouchPos;
    private Vector3 endTouchPos;

    [SerializeField] private float forceMultiplier = 5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    //��ġ �������� ��
    public void OnTouchStart(Vector3 touchPos)
    {
        startTouchPos = touchPos;
        rb.isKinematic = true; 
    }

    //��ġ ���϶�
    public void OnTouchMove(Vector3 touchPos)
    {
        // �巡�� �� �ð������� �̵��ϴ� ���
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, Camera.main.transform.position.y, touchPos.z));
        transform.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);
    }

    //��ġ�� ���� ��
    public void OnTouchEnd(Vector3 touchPos)
    {
        //TODO - ���� ���߿� ���� �ð���ŭ forceMultiplier���� ���� �Ͽ� ��������Ѵ�.
        rb.isKinematic = false;
        endTouchPos = touchPos; // ���� ���޹��� ��ǥ ���(���� ���� �ش� ��ǥ)
        Vector3 delta = startTouchPos - endTouchPos; //���� -> ���� ���� ����
        
        //Vector3 force = new Vector3(delta.x, 0, delta.y) * forceMultiplier;

        rb.AddForce(delta.normalized * forceMultiplier, ForceMode.Impulse);
    }
}
