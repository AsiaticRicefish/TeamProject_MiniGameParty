using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using DesignPattern;
using Photon.Realtime;
using ShootingScene;
using static LDH_Util.Define_LDH;
using Managers;
using Customization;
using UnityEngine.TextCore.Text;
using Cysharp.Threading.Tasks;

public class EggManager : PunSingleton<EggManager>, IGameComponent
{
    [Header("Pool Settings")]
    public GameObject unimoEggPrefab;
    public Transform eggSpawnPoint;
    public int poolSizePerPlayer = 5;   //한사람당 5개만

    private const string unimoEggPrefabPath = "Net/UnimoEggPrefab";

    [Header("Current State")]
    public UnimoEgg currentUnimoEgg;

    // 예시: uid → 프리팹 이름 매핑
    private Dictionary<string, string> playerPrefabMap = new Dictionary<string, string>();

    private Dictionary<string, List<UnimoEgg>> playerEggPools = new();

    //모든 알이있는 딕셔너리 
    public Dictionary<int, UnimoEgg> viewIdToEgg = new();

    private bool isPoolReady = false;

    private HashSet<string> registerdPools = new();

    protected override void OnAwake()
    {
        Debug.Log("[EggManager] - 초기화");
        isPoolReady = false;
        registerdPools.Clear();
    }

    public async void Initialize()
    {
        Debug.Log("EggManager Initialize 시작");
        await LocalInitPool();              //끝까지 돌 때까지 
    }

    // 각자 자신의 풀 생성
    private async UniTask LocalInitPool()
    {
        Debug.Log("각자 EggManager 유니모 오브젝트 생성 시작");       

        string myUid = PMS_Util.Util.GetMyUid();     
        Debug.Log($"{myUid}");// 내 UID를 가져오기
        List<int> viewIDs = new List<int>();                            //UnimoEgg를 viewID 매핑하기 위하여 초기화

        //만약 내이름에 풀이 있으면 안되니깐 먼저 확인하고 새로운 풀리스트 생성
        if (!playerEggPools.ContainsKey(myUid))
        {
            Debug.Log($"{myUid} uid에 해당하는 풀이 없어서 풀을 만듭니다.");
            playerEggPools[myUid] = new List<UnimoEgg>();               //풀 리스트 초기화                
        }
        else
        {
            Debug.Log("[UnimoEgg] - 이미 내 UID에 맞는 pool이 존재함!");
        }

        Debug.Log($"{myUid} 에그 생성 시작");
        //string CharacterIDKey = PlayerProps.GetPlayerInfoKey(PlayerProps.PlayerInfoKey.CharacterId);
        //string EquipIDKey = PlayerProps.GetPlayerInfoKey(PlayerProps.PlayerInfoKey.EquipId);

        string myCharacterID = CustomizationManager.Instance.GetEquippedLocal().characterId;
        string myEquipID = CustomizationManager.Instance.GetEquippedLocal().equipId;

        Debug.Log($"[EggManager] 커스터마이징 데이터 가져오기 - characterID : {myCharacterID}, equipID{myEquipID}");

        for (int i = 0; i < poolSizePerPlayer; i++)
        {
            
            GameObject eggObj = PhotonNetwork.Instantiate(unimoEggPrefabPath, Vector3.zero, Quaternion.identity);
            UnimoEgg egg = eggObj.GetComponent<UnimoEgg>();

            egg.ShooterUid = myUid;
            egg.gameObject.SetActive(false);            //로컬 비활성화

            // TODO - 커스텀 데이터 들고와서 입히기
            await Manager.Custom.ApplyToAvatarAsync(egg.playerAvatar, myCharacterID, myEquipID);

            playerEggPools[myUid].Add(egg);
            viewIdToEgg[egg.photonView.ViewID] = egg;
            viewIDs.Add(egg.photonView.ViewID);

            await UniTask.Yield(); // 프레임 넘기기
        }

        registerdPools.Add(myUid);
        Debug.Log($"[EggManager] - (로컬) {PhotonNetwork.LocalPlayer.NickName}의 풀 생성 완료");

        
        Debug.Log($"[EggManager] - 내 풀을 남한테 전달합니다.");
        // 모든 유저에게 생성된 egg의 내가 생성한 viewIDs 전달
        // 커스터마이징에 대한 정보도 넘겨주는데 대신 비동기 작업이라 보장을 해주고 싶은데 RPC안에서는 await를 하지 못한다.
        photonView.RPC(nameof(RPC_RegisterEgg), RpcTarget.OthersBuffered, myUid, viewIDs.ToArray(), myCharacterID, myEquipID);

        // TODO - 커스텀 관련데이터 넘겨주기(rpc전송)
        CheckRegisterAllPool();
    }

    [PunRPC]
    private void RPC_RegisterEgg(string uid,int[] viewIDs,string characterId,string equipId)
    {
        // 코루틴으로 래핑해서 UniTask 내부 await을 기다리게 함
        StartCoroutine(RegisterEggCoroutine(uid, viewIDs, characterId, equipId));
    }

    private IEnumerator RegisterEggCoroutine(string uid, int[] viewIDs, string characterId, string equipId)
    {
        Debug.Log("register egg - view ids count : " + viewIDs.Length);
        //viewIDs를 전달 받음 배열로 전체 순회
        foreach (var id in viewIDs)
        {
            PhotonView view = PhotonView.Find(id);

            if (view == null)
                continue;

            UnimoEgg egg = view.GetComponent<UnimoEgg>();
            egg.ShooterUid = uid;
            egg.gameObject.SetActive(false);                    //다른 클라이언트도 비활성화시키게 하기

            if (!playerEggPools.ContainsKey(uid))               //나 이외의 유저들은 해당 viewID를 가진 유니모를 해당 UID의 유저의 풀에 등록
                playerEggPools[uid] = new List<UnimoEgg>();
            if (!playerEggPools[uid].Contains(egg))             
                playerEggPools[uid].Add(egg);

            viewIdToEgg[id] = egg;

            // 커스터마이징 적용
            yield return Manager.Custom
            .ApplyToAvatarAsync(egg.playerAvatar, characterId, equipId)
            .ToCoroutine();       

            yield return null;  //한프레임 쉬게 하기 한 프레임 안에 모든 작업 처리하지 않고 프레임마다 분배
        }

        // 모든 에그에 커스터마이징이 적용된 뒤에 풀 등록 처리
        registerdPools.Add(uid);
        Debug.Log($"{uid} 의 풀 전달 받아서 등록 완료");

        CheckRegisterAllPool();
    }


    private void CheckRegisterAllPool()
    {

        Debug.Log(registerdPools.Count + " / " + PhotonNetwork.CurrentRoom.PlayerCount + " 플레이어의 풀 등록 상태 확인");
        if (registerdPools.Count == PhotonNetwork.CurrentRoom.PlayerCount) //모든 플레이어의 풀이 등록이 완료 되었을 때
        {
            isPoolReady = true;
            Debug.Log("모든 플레이어의 풀 등록 완료 - isPoolReady true");
        }
    }


    // 턴 시작 시 개인이 호출
    public UnimoEgg SpawnEgg(string shooterUid)
    {
        if (!isPoolReady) return null;

        //if (currentUnimoEgg != null) return null;

        // 풀에서 비활성 알 찾기
        Debug.Log($"플레이어 egg pools {playerEggPools.Count}개 - {playerEggPools.Values?.ToList()[0].Count} egg 있음");
        Debug.Log($"shooter uid {shooterUid}에 해당하는 egg pools 있나? {playerEggPools.ContainsKey(shooterUid)}");
        Debug.Log($" playerEggPools[shooterUid] == null? {playerEggPools[shooterUid] == null}");
        UnimoEgg egg = playerEggPools[shooterUid].Find(e => !e.gameObject.activeInHierarchy);
        if (egg == null)
        {
            Debug.LogError($"[EggManager] {shooterUid} 사용 가능한 알 없음!");
            return null;
        }

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

        egg.ShooterUid ??= shooterUid;

        egg.SetMaterial();
        egg.gameObject.SetActive(true);

        Rigidbody rb = egg.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        //currentUnimoEgg = egg;     
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

        if (egg.photonView.IsMine)
        {
            Debug.Log("Egg의 주인만 초기화 진행");
            egg.Initialize();                       //UnimoEgg 초기화
            egg.localPlayerInput.Initialize();      //LocalPlayerInput 초기화
        }

        egg.gameObject.SetActive(false);

        //if (currentUnimoEgg == egg)
        //    currentUnimoEgg = null;
    }

    public void DestroyAllMyEggs()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("각자 생성한 유니모 파괴시키기");

        // 현재 풀 전체 순회
        foreach (var unimoEggList in playerEggPools)
        {
            foreach (var egg in unimoEggList.Value)
            {
                if (egg.photonView.IsMine && egg != null && egg.gameObject != null )
                {
                    PhotonNetwork.Destroy(egg.gameObject);
                }
            }
        }

        playerEggPools.Clear();         // 풀 정리
        viewIdToEgg.Clear();            // ViewID Egg정리
        currentUnimoEgg = null;         
        isPoolReady = false;
    }


    //public void ReturnAllEggOwnership()
    //{
    //    if (!PhotonNetwork.IsMasterClient) return;

    //    Debug.Log("마스터 모든 Egg 오브젝트 소유권 리턴");

    //    foreach (var unimoEgg in playerEggPools)
    //    {
    //        foreach (var egg in unimoEgg.Value)
    //        {
    //            if (egg != null && egg.gameObject != null)
    //            {
    //                egg.photonView.RequestOwnership();
    //            }
    //        }
    //    }
    //}

    //나간 유저의 otherPlayer를 가지고 UID를 찾아야한다.
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer.CustomProperties.TryGetValue("uid", out object uidObj) && uidObj is string targetUid)
        {
            Debug.Log($"[EggManager] - 플레이어 퇴장: UID = {targetUid}");

            if (playerEggPools.TryGetValue(targetUid, out List<UnimoEgg> targetPool))
            {
                Debug.Log("[EggManager] - targetUID와 일치하는 eggList key가 존재");
                //foreach (var egg in targetPool)           //에그입니다 - 해당 유저 풀리스트
                //{
                //    if (egg != null)
                //    {
                //        Debug.Log($"[EggManager] - {egg.ShooterUid} 에 해당 되는 egg가 존재");

                //        // viewIdToEgg에서도 제거
                //        if (viewIdToEgg.ContainsKey(egg.photonView.ViewID))
                //        {
                //            Debug.Log($"[EggManager] - viewID가 존재하는 Egg 제거");
                //            viewIdToEgg.Remove(egg.photonView.ViewID);
                //        }
                //        else
                //        {
                //            Debug.Log($"[EggManager] - viewID가 존재하는 Egg가 없음");
                //        }
                //        Debug.Log($"[EggManager] - 해당 오브젝트 파괴");
                //        if (PhotonNetwork.IsMasterClient)
                //        {
                //            PhotonNetwork.Destroy(egg.gameObject);
                //        }
                //    }
                //    else
                //    {
                //        Debug.Log($"[EggManager] - {egg.ShooterUid} 에 해당 되는 egg가 존재않음");
                //    }
                //}
                // 풀 제거
                if (playerEggPools.Remove(targetUid))
                {
                    Debug.Log($"[EggManager] - 유저 : {targetUid} pool를 제거 함");
                }
                else
                {
                    Debug.Log($"[EggManager] - 풀에서 제거 실패");
                }
            }
            else
            {
                Debug.Log($"[EggManager] - 정리 작업 실패");
            }
        }
    }
}
