using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using ShootingScene;

[RequireComponent(typeof(Rigidbody))]
public class UnimoEgg : MonoBehaviourPun
{
    [SerializeField]private bool turnEnded = false;
    public Rigidbody rb;
    public float stopSpeed = 0.01f; // 속도 기준
    //private Vector3 startdic;

    //private Vector3 startTouchPos;
    //private Vector3 endTouchPos;
    public bool isLaunched; // 내가 발사한 알인가?

    public string ShooterUid; // 누가 던졌는지 저장
    //[SerializeField][Range(0.1f,15f)] private float forceMultiplier = 3f;

    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _renderer = GetComponent<Renderer>();

    }

    #region Test용 Material 임시 추가

    private Renderer _renderer;
    public Material[] unimoMats;
    
    public void SetMaterial()
    {
        if (ShooterUid == null) return;
        _renderer.material = unimoMats[TurnManager.Instance.currentTurnIndex - 1];

    }

    #endregion
    
    #region Legacy 조작법
    ////터치 시작했을 때
    //public void OnTouchStart(Vector2 touchPos)
    //{
    //    Debug.Log("유니모 찾아서 터치 시작함");
    //    startTouchPos = touchPos;               //시작 위치를 저장
    //    rb.isKinematic = true; 
    //}

    ////터치 중일때
    //public void OnTouchMove(Vector2 touchPos)
    //{
    //    // 화면 좌표의 Y를 월드 좌표의 Z로 변환
    //    //Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, Camera.main.transform.position.y));
    //    //transform.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);
    //    //생각해 보니깐 이동을 굳이 안해도된다.
    //}

    ////터치를 땟을 때
    //public void OnTouchEnd(Vector2 endtouchPos)
    //{
    //    //TODO - 추후 나중에 놓은 시간만큼 forceMultiplier값을 높게 하여 곱해줘야한다.
    //    rb.isKinematic = false;

    //    //항상 카메라는 x,y좌표 보다 Camera.main.transform.position.y 만큼 떨어져있다.
    //    Vector3 startWorld = Camera.main.ScreenToWorldPoint(new Vector3(startTouchPos.x,startTouchPos.y,Camera.main.transform.position.y));
    //    Vector3 endWorld = Camera.main.ScreenToWorldPoint(new Vector3(endtouchPos.x,endtouchPos.y,Camera.main.transform.position.y));

    //    //두벡터사이의 거리를 구함
    //    //float pullBackPower = Vector3.Distance(startWorld,endWorld);
    //    //Debug.Log(pullBackPower);

    //    //두벡터 사이의 방향을 구함 -> 여기서 dir.magnitude의 값은 벡터의 크기
    //    Vector3 dir = (startWorld - endWorld);
    //    Debug.Log(dir.magnitude);
    //    dir.y = 0;

    //    //forceMultiplier - 보정값
    //    rb.AddForce(dir * forceMultiplier, ForceMode.Impulse);
    //    /*Vector3 dir = startTouchPos - endTouchPos; //종료 -> 시작 방향 벡터


    //    //해상도 max min 힘의 크기? 특정힘까지만 줄수있도록 

    //    dir = new Vector3(dir.x, 0 , dir.z);

    //    //Vector3 force = new Vector3(delta.x, 0, delta.y) * forceMultiplier;

    //    rb.AddForce(dir.normalized * forceMultiplier, ForceMode.Impulse);*/
    //}
    #endregion

    /*
    public void Shot(Vector3 dir)
    {
        rb.velocity = Vector3.zero; // 기존 속도 초기화
        rb.AddForce(dir, ForceMode.Impulse);
        //rb.AddForce(dir * forceMultiplier, ForceMode.Impulse);
        Debug.Log($"발사 방향의 힘의 크기 - {dir.magnitude}");
        //Debug.Log($"발사 방향의 힘의 크기 - {dir.magnitude * forceMultiplier}");
        Test_ShotFollowCamera.Instance.StartFollow(gameObject);
    }*/

    public void Initialize()
    {
        // Rigidbody 초기화
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false;

        // 상태 초기화
        ShooterUid = null;
        turnEnded = false;
        // 카메라 팔로우 초기화
        //Test_ShotFollowCamera.Instance.StopFollow(gameObject);
    }

    // 기존 Shot 호출 대신 RPC로 보내기
    public void Shot(Vector3 dir)
    {
        if (!photonView.IsMine) return;

        // 자기 화면에서 AddForce 적용
        isLaunched = true;
        ApplyForce(dir);
        // 다른 클라이언트에도 RPC 전송
        photonView.RPC("RPC_Shot", RpcTarget.Others, dir);
        Test_ShotFollowCamera.Instance.StartFollow(gameObject);
        // 발사 후 멈출 때까지 감시 시작
        //StartCoroutine(WaitForStop());
        // 발사 후 한 프레임 대기 후 감시 시작
        StartCoroutine(WaitForStop());
    }
    // 발사 후 한 프레임 대기 후 감시 시작

    private IEnumerator WaitForStop()
    {
        yield return new WaitForFixedUpdate();   //AddForce 보장                                     
        yield return new WaitForFixedUpdate();

        while (rb.velocity.magnitude > stopSpeed && gameObject.activeSelf)
            yield return null;

        // 내가 던진 알일 때만 마스터에게 턴 종료 요청
        if (photonView.IsMine && !turnEnded)
        {
            turnEnded = true;
            TurnManager.Instance.photonView.RPC(("RequestTurnEnd"), RpcTarget.MasterClient);
            isLaunched = false;
        }
    }

    // 실제 힘 적용
    private void ApplyForce(Vector3 dir)
    {
        rb.velocity = Vector3.zero;
        rb.AddForce(dir, ForceMode.Impulse);
        Debug.Log($"발사 방향의 힘의 크기 - {dir.magnitude}");
    }

    [PunRPC]
    private void RPC_Shot(Vector3 dir)
    {
        ApplyForce(dir);
    }

    //떨어졌을때
    private void OnTriggerExit(Collider other)
    {
        //if (!photonView.IsMine || turnEnded) return; // 내 알이 아니면 아무것도 안 함

        //모두가 비활성처리를 해줘야한다.
        EggManager.Instance.photonView.RPC("RPC_DeactivateEgg", RpcTarget.All, photonView.ViewID);

        if (other.CompareTag("PlayGround") && isLaunched)
        {
            isLaunched = false; // 바깥으로 나가며 턴 종료 → 발사 상태 해제
            TurnManager.Instance.photonView.RPC(("RequestTurnEnd"), RpcTarget.MasterClient);
        }
    }
}
