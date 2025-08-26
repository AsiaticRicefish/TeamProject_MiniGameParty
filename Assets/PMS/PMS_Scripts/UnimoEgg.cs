using UnityEngine;
using ShootingScene;
[RequireComponent(typeof(Rigidbody))]
public class UnimoEgg : MonoBehaviour
{
    private Rigidbody rb;

    private Vector3 startdic;

    private Vector3 startTouchPos;
    private Vector3 endTouchPos;

    public string ShooterUid; // 누가 던졌는지 저장

    [SerializeField][Range(0.1f,15f)] private float forceMultiplier = 3f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    //터치 시작했을 때
    public void OnTouchStart(Vector2 touchPos)
    {
        Debug.Log("유니모 찾아서 터치 시작함");
        startTouchPos = touchPos;               //시작 위치를 저장
        rb.isKinematic = true; 
    }

    //터치 중일때
    public void OnTouchMove(Vector2 touchPos)
    {
        // 화면 좌표의 Y를 월드 좌표의 Z로 변환
        //Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));
        //transform.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        //생각해 보니깐 이동을 굳이 안해도된다.
    }

    //터치를 땟을 때
    public void OnTouchEnd(Vector2 endtouchPos)
    {
        //TODO - 추후 나중에 놓은 시간만큼 forceMultiplier값을 높게 하여 곱해줘야한다.
        rb.isKinematic = false;

        //항상 카메라는 x,y좌표 보다 Camera.main.transform.position.y 만큼 떨어져있다.
        Vector3 startWorld = Camera.main.ScreenToWorldPoint(new Vector3(startTouchPos.x,startTouchPos.y,Camera.main.transform.position.y));
        Vector3 endWorld = Camera.main.ScreenToWorldPoint(new Vector3(endtouchPos.x,endtouchPos.y,Camera.main.transform.position.y));

        //두벡터사이의 거리를 구함
        //float pullBackPower = Vector3.Distance(startWorld,endWorld);
        //Debug.Log(pullBackPower);

        //두벡터 사이의 방향을 구함 -> 여기서 dir.magnitude의 값은 벡터의 크기
        Vector3 dir = (startWorld - endWorld);
        Debug.Log(dir.magnitude);
        dir.y = 0;

        //forceMultiplier - 보정값
        rb.AddForce(dir * forceMultiplier, ForceMode.Impulse);
        /*Vector3 dir = startTouchPos - endTouchPos; //종료 -> 시작 방향 벡터
         

        //해상도 max min 힘의 크기? 특정힘까지만 줄수있도록 

        dir = new Vector3(dir.x, 0 , dir.z);
        
        //Vector3 force = new Vector3(delta.x, 0, delta.y) * forceMultiplier;

        rb.AddForce(dir.normalized * forceMultiplier, ForceMode.Impulse);*/
    }

    public void Shot(Vector3 dir)
    {
        rb.velocity = Vector3.zero; // 기존 속도 초기화
        rb.AddForce(dir, ForceMode.Impulse);
        //rb.AddForce(dir * forceMultiplier, ForceMode.Impulse);
        Debug.Log($"발사 방향의 힘의 크기 - {dir.magnitude}");
        //Debug.Log($"발사 방향의 힘의 크기 - {dir.magnitude * forceMultiplier}");
        Test_ShotFollowCamera.Instance.StartFollow(gameObject);
    }
}
