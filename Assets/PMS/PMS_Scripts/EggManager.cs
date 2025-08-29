using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DesignPattern;

public class EggManager : PunSingleton<EggManager>, IGameComponent
{
    [Header("Pool Settings")]
    public GameObject unimoEggPrefab;
    public Transform eggSpawnPoint;
    public int poolSizePerPlayer = 5;   //한사람당 5개만

    [Header("Current State")]
    public UnimoEgg currentUnimoEgg;

    private Dictionary<string, List<UnimoEgg>> playerEggPools = new();
    private Dictionary<int, UnimoEgg> viewIdToEgg = new();

    private bool isPoolReady = false;

    protected override void OnAwake()
    {
        Debug.Log("[EggManager] - 초기화");
    }

    public void Initialize()
    {
        Debug.Log("EggManager Initialize 시작");
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(MasterInitPools());
    }

    //마스터가 Pool을 생성한다. 
    private IEnumerator MasterInitPools()
    {
        Debug.Log("EggManager 유니모 오브젝트 생성 시작");
        // 모든 플레이어 UID 가져오기
        List<string> uids = new List<string>(ShootingGameManager.Instance.players.Keys);

        foreach (var uid in uids)
        {
            if (!playerEggPools.ContainsKey(uid))
                playerEggPools[uid] = new List<UnimoEgg>();

            List<int> viewIDs = new List<int>();

            for (int i = 0; i < poolSizePerPlayer; i++)
            {
                GameObject eggObj = PhotonNetwork.Instantiate(unimoEggPrefab.name, Vector3.zero, Quaternion.identity);
                UnimoEgg egg = eggObj.GetComponent<UnimoEgg>();
                egg.ShooterUid = uid;
                egg.gameObject.SetActive(false);

                playerEggPools[uid].Add(egg);
                viewIdToEgg[egg.photonView.ViewID] = egg;
                viewIDs.Add(egg.photonView.ViewID);

                yield return null;
            }

            // 다른 클라이언트에도 알 풀 세팅
            photonView.RPC(nameof(RPC_SetupPlayerPool), RpcTarget.OthersBuffered, uid, viewIDs.ToArray());
        }

        isPoolReady = true;
        Debug.Log("[EggManager] 모든 풀 초기화 완료");
    }

    [PunRPC]
    private void RPC_SetupPlayerPool(string uid, int[] viewIDs)
    {
        if (!playerEggPools.ContainsKey(uid))
            playerEggPools[uid] = new List<UnimoEgg>();

        foreach (var id in viewIDs)
        {
            PhotonView view = PhotonView.Find(id);
            if (view != null)
            {
                UnimoEgg egg = view.GetComponent<UnimoEgg>();
                egg.ShooterUid = uid;
                egg.gameObject.SetActive(false);

                playerEggPools[uid].Add(egg);
                viewIdToEgg[id] = egg;
            }
        }
        isPoolReady = true;
    }

    // 턴 시작 시 호출
    public UnimoEgg SpawnEgg(string shooterUid)
    {
        Debug.Log("[SpawnEgg] - 호출?");
        if (!isPoolReady) return null;
        Debug.Log("[SpawnEgg] - 호출2?");
        if (currentUnimoEgg != null) return null;
        Debug.Log("[SpawnEgg] - 호출3?");
        // 풀에서 비활성 알 찾기
        UnimoEgg egg = playerEggPools[shooterUid].Find(e => !e.gameObject.activeInHierarchy);
        if (egg == null)
        {
            Debug.LogError($"[EggManager] {shooterUid} 사용 가능한 알 없음!");
            return null;
        }
        Debug.Log("[SpawnEgg] - 호출4?");
        //모든 클라이언트 한테 SetActive 및 position 이동
        photonView.RPC(nameof(RPC_ActivateEgg), RpcTarget.AllBuffered,
            egg.photonView.ViewID,
            shooterUid,
            eggSpawnPoint.position.x,
            eggSpawnPoint.position.y,
            eggSpawnPoint.position.z);

        return egg;
    }

    [PunRPC]
    private void RPC_ActivateEgg(int viewID, string shooterUid, float x, float y, float z)
    {
        if (!viewIdToEgg.TryGetValue(viewID, out var egg))
        {
            Debug.LogError("[EggManager] ViewID에 해당하는 알 없음: " + viewID);
            return;
        }
        Debug.Log("[SpawnEgg] - 호출5?");
        egg.transform.position = new Vector3(x, y, z);
        egg.transform.rotation = Quaternion.identity;
        egg.ShooterUid = shooterUid;
        egg.gameObject.SetActive(true);

        Rigidbody rb = egg.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentUnimoEgg = egg;

        //  여기서 발사자 클라이언트라면 소유권 요청
        if (!PhotonNetwork.IsMasterClient && PMS_Util.PMS_Util.GetMyUid() == shooterUid)//PhotonNetwork.LocalPlayer.UserId == shooterUid)
        {
            Debug.Log("[SpawnEgg] - 소유권 요청!");
            PhotonView eggView = egg.photonView;

            // 소유권 요청
            eggView.RequestOwnership();

            // 소유권 획득 후 Shot 호출 (AddForce 적용)
            // 만약 바로 호출하면 IsMine이 아직 false일 수 있음 → 코루틴으로 약간 지연 가능
            StartCoroutine(CallShotAfterOwnership(eggView));
        }
    }

    private IEnumerator CallShotAfterOwnership(PhotonView eggView)
    {
        // 한 프레임 대기
        //yield return null;
        yield return new WaitUntil(() => eggView.IsMine);
        Debug.Log("소유권 승인!");
    }

    // 턴 종료 시 호출
    [PunRPC]
    public void ClearCurrentEgg()
    {
        currentUnimoEgg = null;
    }

    [PunRPC]
    private void RPC_DeactivateEgg(int viewID)
    {
        if (!viewIdToEgg.TryGetValue(viewID, out var egg)) return;
        egg.gameObject.SetActive(false);

        if (currentUnimoEgg == egg)
            currentUnimoEgg = null;
    }
}
