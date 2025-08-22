using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

public class Test_Harness : MonoBehaviour
{
    [Header("필수")]
    [SerializeField] private JengaTowerManager towerMgr;          // 인스펙터 미지정 시 자동 Find
    [SerializeField] private JengaNetworkManager netMgr;          // 인스펙터 미지정 시 자동 생성

    [Header("옵션")]
    public string offlineRoomName = "LOCAL_OFFLINE";
    public float testAccuracy = 0.85f;
    public int clientSuggestedScore = 15;

    void Awake()
    {
        // 1) EventSystem 보장
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem").AddComponent<EventSystem>();
            es.gameObject.AddComponent<StandaloneInputModule>();
        }

        // 2) 오프라인 룸 준비
        PhotonNetwork.OfflineMode = true;           // 서버 연결 안 함
        PhotonNetwork.NickName = "LocalTester";

        // uid 커스텀 프로퍼티 세팅 (JengaTowerManager.TryGetUid에서 읽음)
        var h = new ExitGames.Client.Photon.Hashtable
        {
            ["uid"] = "UID-LOCAL"
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(h);

        // 3) 매니저 참조 보장
        if (!towerMgr) towerMgr = JengaTowerManager.Instance ?? FindObjectOfType<JengaTowerManager>();
        if (!netMgr) netMgr = JengaNetworkManager.Instance ?? FindObjectOfType<JengaNetworkManager>();
        if (!netMgr)   // 씬에 없으면 생성 + PhotonView 부착
        {
            netMgr = new GameObject("JengaNetworkManager").AddComponent<JengaNetworkManager>();
            if (!netMgr.TryGetComponent<PhotonView>(out _)) netMgr.gameObject.AddComponent<PhotonView>();
        }
    }

    IEnumerator Start()
    {
        // 4) 룸 생성/입장 대기
        if (!PhotonNetwork.InRoom) PhotonNetwork.CreateRoom(offlineRoomName);
        yield return new WaitUntil(() => PhotonNetwork.InRoom);

        // 5) 초기화
        if (!towerMgr)
        {
            var go = new GameObject("JengaTowerManager");
            towerMgr = go.AddComponent<JengaTowerManager>();
        }
        towerMgr.Initialize();
        netMgr.Initialize();

        // 6) 내 타워가 실제 생성될 때까지 대기
        var actor = PhotonNetwork.LocalPlayer.ActorNumber;
        JengaTower tower = null;
        for (int i = 0; i < 60 && tower == null; i++) // 최대 1초 정도 대기
        {
            tower = towerMgr.GetPlayerTower(actor);
            if (tower == null) yield return null;
        }
        if (tower == null) { Debug.LogError("타워 생성 실패: blockPrefab/모드 설정 확인"); yield break; }

        // 7) 제거 가능 블록 확보
        var removable = tower.GetRemovableBlocks();
        if (removable == null || removable.Count == 0)
        {
            Debug.LogError("제거 가능 블록이 없습니다. towerHeight/allowTopRemoval/캐시 로직 확인");
            yield break;
        }
        var block = removable[0];
        block.SetInteractable(true);

        // 8) 입력 경로 스모크 테스트
        var ped = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(block.gameObject, ped, ExecuteEvents.pointerClickHandler); // 1차 클릭
        ExecuteEvents.Execute(block.gameObject, ped, ExecuteEvents.pointerClickHandler); // 2차 클릭

        // 9) 네트워크 경로로 제거 요청 (Offline/마스터일 땐 로컬로 바로 처리)
        netMgr.RequestBlockRemoval_MasterAuth(
            actorNumber: actor,
            blockId: block.BlockId,
            clientSuggestedScore: clientSuggestedScore,
            clientAccuracy: testAccuracy
        );

        // 10) 적용 대기 & 검증
        yield return new WaitForSeconds(0.1f);
        Debug.Log($"[Harness] Removed? {block.IsRemoved}");
    }
}