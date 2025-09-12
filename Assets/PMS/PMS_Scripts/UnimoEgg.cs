using System.Collections;
using UnityEngine;
using Photon.Pun;
using ShootingScene;
using UnityEngine.UI;

[RequireComponent(typeof(LocalPlayerInput))]
[RequireComponent(typeof(Rigidbody))]
public class UnimoEgg : MonoBehaviourPun
{
    [Header("유니모 스크립트")]
    public LocalPlayerInput localPlayerInput;
    public ChargeController chargeController;

    public Rigidbody rb;
    private float stopSpeed = 0.01f; // 속도 기준

    //상태 체크용 bool변수
    [Header("상태 체크용 bool변수")]
    [SerializeField] private bool turnEnded;
    [SerializeField] private bool isLaunched; // 내가 발사한 알인가?
    [SerializeField] private bool isCameraFollowing;
    [SerializeField] private bool hasCrossedStartLine;

    public string ShooterUid; // 누가 던졌는지 저장
    
    private void Awake()
    {
        _renderer = GetComponent<Renderer>();

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (localPlayerInput == null) localPlayerInput = GetComponent<LocalPlayerInput>();
    }

    #region Test용 Material 임시 추가

    private Renderer _renderer;
    public Material[] unimoMats;
    
    /*public void SetMaterial()
    {
        if (ShooterUid == null) return;
        _renderer.material = unimoMats[TurnManager.Instance.currentTurnIndex - 1];

    }*/

    #endregion

    public void Initialize()
    {
        // Rigidbody 초기화
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false;

        // 상태 초기화
        ShooterUid = null;
        turnEnded = false;
        isLaunched = false;
        isCameraFollowing = false;
        hasCrossedStartLine = false;
    }

    // 기존 Shot 호출 대신 RPC로 보내기
    public void Shot(Vector3 dir)
    {
        if (!photonView.IsMine) return;

        ShootingScene.PlayerInputManager.Instance.DisableInput();
        ShootingCameraManager.Instance.StartFollowTarget(gameObject);
        // 자기 화면에서 AddForce 적용
        isLaunched = true;
        //ApplyForce(dir);
        WindHelper.AddForceWithWind(rb, dir);
        // 다른 클라이언트에도 RPC 전송
        photonView.RPC("RPC_Shot", RpcTarget.Others, dir);
        isCameraFollowing = true;
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

        while (rb.velocity.magnitude > stopSpeed)
        {
            //rb.velocity *= 0.99f;
            yield return new WaitForFixedUpdate(); //업데이트 프레임
        }

        yield return new WaitForSeconds(1.0f);
        ShootingCameraManager.Instance.StopFollowTarget(); //돌아가는 부분

        // 내가 던진 알일 때만 마스터에게 턴 종료 요청
        if (photonView.IsMine && !turnEnded)
        {
            turnEnded = true;
            TurnManager.Instance.photonView.RPC(("RequestTurnEnd"), RpcTarget.MasterClient);
            isLaunched = false;
            isCameraFollowing = false;

            //TODO - 내가 선을 넘지 못했을 때 SetActive요청
            if (CheckCrossedStartLine())
            {
                EggManager.Instance.photonView.RPC("RPC_DeactivateEgg", RpcTarget.All, photonView.ViewID);
            }
        }
    }
    
    private bool CheckCrossedStartLine()
    {
        if (hasCrossedStartLine) return true; // 이미 넘었다면 그대로 true 유지

        if (transform.position.z < ShootingGameManager.Instance.startLine.transform.position.z)
        {
            hasCrossedStartLine = true;
            return true;
        }

        return false;
    }

    [PunRPC]
    private void RPC_Shot(Vector3 dir)
    {
        //ApplyForce(dir);
        WindHelper.AddForceWithWind(rb, dir);
    }

    //떨어졌을때
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("FallDownZone"))
        {
            rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        }

        if (!photonView.IsMine) return;
        //if (!photonView.IsMine || turnEnded) return; // 내 알이 아니면 아무것도 안 함

        //모두가 비활성처리를 해줘야한다.
        if (other.CompareTag("PlayGround") && isLaunched)
        {
            isLaunched = false; // 바깥으로 나가며 턴 종료 → 발사 상태 해제
            ShootingCameraManager.Instance.StopFollowTarget();
            TurnManager.Instance.photonView.RPC(("RequestTurnEnd"), RpcTarget.MasterClient);

        }

        if (other.CompareTag("PlayGround"))
        {
            EggManager.Instance.photonView.RPC("RPC_DeactivateEgg", RpcTarget.All, photonView.ViewID);
        }
    }

    private void OnDisable()
    {
        if(isCameraFollowing)       //카메라가 연출중이니깐
        {
            Debug.Log("[UnimoEgg] - 유니모를 잃어버려서 카메라가 원위치로 돌아가는중");
            ShootingCameraManager.Instance?.StopFollowTarget();
        }
    }
}
