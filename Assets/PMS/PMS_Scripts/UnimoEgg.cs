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

    //터치 시작했을 때
    public void OnTouchStart(Vector3 touchPos)
    {
        startTouchPos = touchPos;
        rb.isKinematic = true; 
    }

    //터치 중일때
    public void OnTouchMove(Vector3 touchPos)
    {
        // 드래그 중 시각적으로 이동하는 모습
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, Camera.main.transform.position.y, touchPos.z));
        transform.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);
    }

    //터치를 땟을 때
    public void OnTouchEnd(Vector3 touchPos)
    {
        //TODO - 추후 나중에 놓은 시간만큼 forceMultiplier값을 높게 하여 곱해줘야한다.
        rb.isKinematic = false;
        endTouchPos = touchPos; // 직접 전달받은 좌표 사용(놓은 순간 해당 좌표)
        Vector3 delta = startTouchPos - endTouchPos; //종료 -> 시작 방향 벡터
        
        //Vector3 force = new Vector3(delta.x, 0, delta.y) * forceMultiplier;

        rb.AddForce(delta.normalized * forceMultiplier, ForceMode.Impulse);
    }
}
