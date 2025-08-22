using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

public class Test_JengaInitHarness : MonoBehaviour
{
    [Header("�ɼ�")]
    public bool delayManagerAwake = false;   // �Ŵ��� ���� ����(WaitForSingletonReady Ȯ�ο�)
    public bool skipManagerCreation = false; // �Ŵ��� �̻���(Ÿ�Ӿƿ�/���� ���̽� Ȯ�ο�)
    public float managerCreateDelay = 0.5f;  // ���� �ð�
    public string testUid = "test-uid-001";  // ���� �÷��̾� UID
    public string mainSceneName = "MainBoardScene"; // �׽�Ʈ�� ���κ��� �� �̸�

    private void Start()
    {
        // 1) Photon �������� ���
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.NickName = "LocalTester";
        PhotonNetwork.CreateRoom("LOCAL_TEST_ROOM");
        // CustomProperties�� uid ����
        var props = new Hashtable { { "uid", testUid } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log("[Harness] Photon Offline Mode ready, room created, uid set.");

        // 2) PlayerManager�� ���� �÷��̾� ���(�� ������Ʈ�� ���� PlayerManager�� �ִٸ� �װ� ���)

        PlayerManager.Instance.RegisterPlayer(testUid, "LocalTester");

        // 3) JengaSceneController + PhotonView
        var controllerGO = new GameObject("~JengaSceneController");
        var pv = controllerGO.AddComponent<PhotonView>();    // RPC�� ���� �ʿ�
        controllerGO.AddComponent<JengaSceneController>();

        // 4) JengaGameManager ����
        if (!skipManagerCreation)
        {
            if (delayManagerAwake)
                Invoke(nameof(CreateJengaGameManager), managerCreateDelay);
            else
                CreateJengaGameManager();
        }
        else
        {
            Debug.LogWarning("[Harness] skipManagerCreation=true �� JengaGameManager�� ������ �ʽ��ϴ�(Ÿ�Ӿƿ� Ȯ�ο�).");
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
