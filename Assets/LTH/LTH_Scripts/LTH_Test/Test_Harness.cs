using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;

public class Test_Harness : MonoBehaviour
{
    [Header("�ʼ�")]
    [SerializeField] private JengaTowerManager towerMgr;          // �ν����� ������ �� �ڵ� Find
    [SerializeField] private JengaNetworkManager netMgr;          // �ν����� ������ �� �ڵ� ����

    [Header("�ɼ�")]
    public string offlineRoomName = "LOCAL_OFFLINE";
    public float testAccuracy = 0.85f;
    public int clientSuggestedScore = 15;

    void Awake()
    {
        // 1) EventSystem ����
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem").AddComponent<EventSystem>();
            es.gameObject.AddComponent<StandaloneInputModule>();
        }

        // 2) �������� �� �غ�
        PhotonNetwork.OfflineMode = true;           // ���� ���� �� ��
        PhotonNetwork.NickName = "LocalTester";

        // uid Ŀ���� ������Ƽ ���� (JengaTowerManager.TryGetUid���� ����)
        var h = new ExitGames.Client.Photon.Hashtable
        {
            ["uid"] = "UID-LOCAL"
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(h);

        // 3) �Ŵ��� ���� ����
        if (!towerMgr) towerMgr = JengaTowerManager.Instance ?? FindObjectOfType<JengaTowerManager>();
        if (!netMgr) netMgr = JengaNetworkManager.Instance ?? FindObjectOfType<JengaNetworkManager>();
        if (!netMgr)   // ���� ������ ���� + PhotonView ����
        {
            netMgr = new GameObject("JengaNetworkManager").AddComponent<JengaNetworkManager>();
            if (!netMgr.TryGetComponent<PhotonView>(out _)) netMgr.gameObject.AddComponent<PhotonView>();
        }
    }

    IEnumerator Start()
    {
        // 4) �� ����/���� ���
        if (!PhotonNetwork.InRoom) PhotonNetwork.CreateRoom(offlineRoomName);
        yield return new WaitUntil(() => PhotonNetwork.InRoom);

        // 5) �ʱ�ȭ
        if (!towerMgr)
        {
            var go = new GameObject("JengaTowerManager");
            towerMgr = go.AddComponent<JengaTowerManager>();
        }
        towerMgr.Initialize();
        netMgr.Initialize();

        // 6) �� Ÿ���� ���� ������ ������ ���
        var actor = PhotonNetwork.LocalPlayer.ActorNumber;
        JengaTower tower = null;
        for (int i = 0; i < 60 && tower == null; i++) // �ִ� 1�� ���� ���
        {
            tower = towerMgr.GetPlayerTower(actor);
            if (tower == null) yield return null;
        }
        if (tower == null) { Debug.LogError("Ÿ�� ���� ����: blockPrefab/��� ���� Ȯ��"); yield break; }

        // 7) ���� ���� ��� Ȯ��
        var removable = tower.GetRemovableBlocks();
        if (removable == null || removable.Count == 0)
        {
            Debug.LogError("���� ���� ����� �����ϴ�. towerHeight/allowTopRemoval/ĳ�� ���� Ȯ��");
            yield break;
        }
        var block = removable[0];
        block.SetInteractable(true);

        // 8) �Է� ��� ����ũ �׽�Ʈ
        var ped = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(block.gameObject, ped, ExecuteEvents.pointerClickHandler); // 1�� Ŭ��
        ExecuteEvents.Execute(block.gameObject, ped, ExecuteEvents.pointerClickHandler); // 2�� Ŭ��

        // 9) ��Ʈ��ũ ��η� ���� ��û (Offline/�������� �� ���÷� �ٷ� ó��)
        netMgr.RequestBlockRemoval_MasterAuth(
            actorNumber: actor,
            blockId: block.BlockId,
            clientSuggestedScore: clientSuggestedScore,
            clientAccuracy: testAccuracy
        );

        // 10) ���� ��� & ����
        yield return new WaitForSeconds(0.1f);
        Debug.Log($"[Harness] Removed? {block.IsRemoved}");
    }
}