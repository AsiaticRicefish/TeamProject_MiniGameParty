using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

public class Test_JengaInitHarness : MonoBehaviour
{
    [Header("옵션")]
    public bool delayManagerAwake = false;   // 매니저 생성 지연(WaitForSingletonReady 확인용)
    public bool skipManagerCreation = false; // 매니저 미생성(타임아웃/실패 케이스 확인용)
    public float managerCreateDelay = 0.5f;  // 지연 시간
    public string testUid = "test-uid-001";  // 로컬 플레이어 UID
    public string mainSceneName = "MainBoardScene"; // 테스트용 메인복귀 씬 이름

    private void Start()
    {
        // 1) Photon 오프라인 모드
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.NickName = "LocalTester";
        PhotonNetwork.CreateRoom("LOCAL_TEST_ROOM");
        // CustomProperties에 uid 주입
        var props = new Hashtable { { "uid", testUid } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log("[Harness] Photon Offline Mode ready, room created, uid set.");

        // 2) PlayerManager에 로컬 플레이어 등록(네 프로젝트의 실제 PlayerManager가 있다면 그걸 사용)

        PlayerManager.Instance.RegisterPlayer(testUid, "LocalTester");

        // 3) JengaSceneController + PhotonView
        var controllerGO = new GameObject("~JengaSceneController");
        var pv = controllerGO.AddComponent<PhotonView>();    // RPC를 위해 필요
        controllerGO.AddComponent<JengaSceneController>();

        // 4) JengaGameManager 생성
        if (!skipManagerCreation)
        {
            if (delayManagerAwake)
                Invoke(nameof(CreateJengaGameManager), managerCreateDelay);
            else
                CreateJengaGameManager();
        }
        else
        {
            Debug.LogWarning("[Harness] skipManagerCreation=true → JengaGameManager를 만들지 않습니다(타임아웃 확인용).");
        }
    }

    private void CreateJengaGameManager()
    {
        var go = new GameObject("~JengaGameManager");
        var mgr = go.AddComponent<JengaGameManager>();
        var so = new SerializedObject(mgr);
        so.FindProperty("mainMapSceneName").stringValue = mainSceneName;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[Harness] JengaGameManager created.");
    }
}
