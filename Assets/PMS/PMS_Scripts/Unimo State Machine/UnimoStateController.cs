using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using ShootingScene;

public class UnimoStateController : MonoBehaviourPun
{
    [Header("유니모 스크립트")]
    public UnimoEgg unimoEgg;
    public LocalPlayerInput localPlayerInput;
    public ChargeController chargeController;
    public PlayerInputUIController playerUiController;

    private IUnimoEggState currentState;

    public Rigidbody rb;
    public float stopSpeed = 0.01f;
    public bool isLaunched;
    public bool turnEnded;

    public Vector3 LastShotDirection { get; private set; }

    private Dictionary<UnimoEggStateType, IUnimoEggState> stateMap;

    private void Awake()
    {
        stateMap = new Dictionary<UnimoEggStateType, IUnimoEggState>
    {
        { UnimoEggStateType.Idle, new EggIdleState() },
        { UnimoEggStateType.Launched, new EggLaunchedState() },
        { UnimoEggStateType.Moving, new EggMovingState() },
        { UnimoEggStateType.Stopped, new EggStoppedState() },
        { UnimoEggStateType.Disabled, new EggDisabledState() }
    };
    }

    private void OnEnable()
    {
        ChangeState(new EggIdleState());
    }

    private void Update()
    {
        currentState?.Tick(this);
    }

    private void FixedUpdate()
    {
        currentState?.FixedTick(this);
    }


    public void ChangeState(IUnimoEggState newState)
    {
        Debug.Log($"상태 전환: {currentState?.StateType} → {newState.StateType}");
        currentState?.Exit(this);
        currentState = newState;
        currentState.Enter(this);
    }

    public void Shot(Vector3 dir)
    {
        if (!photonView.IsMine) return;

        LastShotDirection = dir;
        //ChangeState(new EggLaunchedState());
    }

    public bool CheckOutOfBounds()
    {
        return transform.position.y < -5f; // 예시
    }

    public bool CheckCrossedStartLine()
    {
        return transform.position.z < ShootingGameManager.Instance.startLine.transform.position.z;
    }

    #region 시간없으면 그냥 처리하도록하고
    [PunRPC]
    public void ChangeStateByType(UnimoEggStateType stateType)
    {
        if (stateMap.TryGetValue(stateType, out IUnimoEggState newState))
        {
            ChangeState(newState);
        }
        else
        {
            Debug.LogWarning($"정의되지 않은 상태: {stateType}");
        }
    }
    #endregion

    public void RequestStateChange(UnimoEggStateType stateType)
    {
        if (!photonView.IsMine) return;

        photonView.RPC("RPC_RequestStateChange", RpcTarget.MasterClient, (int)stateType, photonView.ViewID);
    }

    [PunRPC]
    public void RPC_RequestStateChange(int stateTypeInt, int viewID)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        UnimoEggStateType stateType = (UnimoEggStateType)stateTypeInt;

        // 상태 전환 대상 알 찾기
        PhotonView targetView = PhotonView.Find(viewID);

        switch (stateType)
        {
            case UnimoEggStateType.Idle:
            case UnimoEggStateType.Launched:
            case UnimoEggStateType.Moving:
            case UnimoEggStateType.Stopped:
            case UnimoEggStateType.Disabled:
                // 모든 상태에 대해 동일하게 승인 처리
                targetView.RPC("RPC_ApplyStateChange", RpcTarget.All, stateTypeInt);
                break;

            default:
                Debug.LogWarning($"[Master] 알 수 없는 상태 요청: {stateType}");
                break;
        }
    }

    [PunRPC]
    public void RPC_ApplyStateChange(int stateTypeInt)
    {
        UnimoEggStateType stateType = (UnimoEggStateType)stateTypeInt;
        ChangeStateByType(stateType);       
    }
}
