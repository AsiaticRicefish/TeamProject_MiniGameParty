using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class UnimoEgg : MonoBehaviour
{
    private Rigidbody rb;

    private Vector3 startdic;

    private Vector3 startTouchPos;
    private Vector3 endTouchPos;

    [SerializeField][Range(0.1f,15f)] private float forceMultiplier = 10f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    //��ġ �������� ��
    public void OnTouchStart(Vector2 touchPos)
    {
        Debug.Log("���ϸ� ã�Ƽ� ��ġ ������");
        startTouchPos = touchPos;               //���� ��ġ�� ����
        rb.isKinematic = true; 
    }

    //��ġ ���϶�
    public void OnTouchMove(Vector2 touchPos)
    {
        // ȭ�� ��ǥ�� Y�� ���� ��ǥ�� Z�� ��ȯ
        //Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));
        //transform.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        //������ ���ϱ� �̵��� ���� ���ص��ȴ�.
    }

    //��ġ�� ���� ��
    public void OnTouchEnd(Vector2 endtouchPos)
    {
        //TODO - ���� ���߿� ���� �ð���ŭ forceMultiplier���� ���� �Ͽ� ��������Ѵ�.
        rb.isKinematic = false;

        //�׻� ī�޶�� x,y��ǥ ���� Camera.main.transform.position.y ��ŭ �������ִ�.
        Vector3 startWorld = Camera.main.ScreenToWorldPoint(new Vector3(startTouchPos.x,startTouchPos.y,0/*Camera.main.transform.position.y*/));
        Vector3 endWorld = Camera.main.ScreenToWorldPoint(new Vector3(endtouchPos.x,endtouchPos.y,0/*Camera.main.transform.position.y*/));

        //�κ��ͻ����� �Ÿ��� ����
        //float pullBackPower = Vector3.Distance(startWorld,endWorld);
        //Debug.Log(pullBackPower);

        //�κ��� ������ ������ ���� -> ���⼭ dir.magnitude�� ���� ������ ũ��
        Vector3 dir = (startWorld - endWorld);
        Debug.Log(dir.magnitude);
        dir.y = 0;

        //forceMultiplier - ������
        rb.AddForce(dir * forceMultiplier, ForceMode.Impulse);
        /*Vector3 dir = startTouchPos - endTouchPos; //���� -> ���� ���� ����
         

        //�ػ� max min ���� ũ��? Ư���������� �ټ��ֵ��� 

        dir = new Vector3(dir.x, 0 , dir.z);
        
        //Vector3 force = new Vector3(delta.x, 0, delta.y) * forceMultiplier;

        rb.AddForce(dir.normalized * forceMultiplier, ForceMode.Impulse);*/
    }
}
