using LDH_Game;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// ★★★ 설명 ★★★
// Login(게스트/GPGS) 성공 → UID/Nickname이 Photon에 주입된 상태에서
// "Game Bootstrap" 오브젝트를 만들면 이 컴포넌트가 붙습니다.
// 역할:
//  1) Photon 서버에 연결(ConnectUsingSettings)
//  2) 마스터 연결되면 로비 진입(JoinLobby) - NetworkManager가 있으면 그쪽 TryJoinLobby() 우선
//  3) 로비 진입까지 완료되면 자기 자신은 필요 없으니 파괴(선택)
//
// 네임스페이스를 비운 이유:
//  - 다른 스크립트에서 typeof(GameBootstrap)로 바로 접근하므로,
//    여기서도 전역으로 두면 using/namespace 없이 항상 인식됩니다.

public class GameBootstrap : MonoBehaviourPunCallbacks
{
    [Header("자동 연결 옵션")]
    [Tooltip("Start에서 자동으로 ConnectUsingSettings()를 호출할지")]
    [SerializeField] private bool autoConnectOnStart = true;

    [Tooltip("마스터 연결 즉시 로비로 진입할지 (NetworkManager 없을 때만 직접 JoinLobby)")]
    [SerializeField] private bool autoJoinLobby = true;

    [Header("로그")]
    [SerializeField] private bool verbose = true;

    private void Awake()
    {
        //    1) 로그아웃 중이거나, "다음 씬 1회 억제"가 걸려있으면
        //    → 이번 씬에서 자동접속을 절대 시도하지 않게 자신을 파괴
        //    Consume()는 1회성으로 플래그를 소모하므로 이후엔 정상 동작
        if (AuthLogout.IsLoggingOut || AuthAutoSuppressor.Consume())
        {
            if (verbose) Debug.Log("[GameBootstrap] suppressed by logout/suppressor → Destroy self");
            Destroy(gameObject);
            return;
        }

        // 2) 여기까지 왔으면 유지
        DontDestroyOnLoad(gameObject);
        if (verbose) Debug.Log("[GameBootstrap] Awake()");
    }

    private void Start()
    {
        // Awake에서 이미 파괴됐다면 Start는 호출되지 않습니다.
        if (!autoConnectOnStart)
        {
            if (verbose) Debug.Log("[GameBootstrap] autoConnectOnStart=false → 대기");
            return;
        }

        // 실제 접속 로직(예시)
        if (verbose) Debug.Log("[GameBootstrap] ConnectUsingSettings()");
        // PhotonNetwork.AutomaticallySyncScene = true; // 필요 시
        // PhotonNetwork.ConnectUsingSettings();
        
        // 마스터 연결 후 로비 자동 진입을 원하면 flag를 사용
        // (OnConnectedToMaster에서 체크해서 JoinLobby 호출)
        
        // 게임 부트스트랩을 돌려줘야 합니다. (초기화 및 데이터 정보 가져온 후에 포톤 네트워크에 연결해서 로비로 이동시켜야 합니다. 이 일을 모두 GameBootstrap에서 처리합니다.)
        GameObject gameBootstrap = new GameObject("Game Bootstrap", typeof(GameStartBootstrap));
    }

    public override void OnConnectedToMaster()
    {
        if (verbose) Debug.Log("[GameBootstrap] OnConnectedToMaster");
        if (autoJoinLobby)
        {
            //join lobby 중복 호출되서 오류나므로 주석처리합니다.
            // PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinedLobby()
    {
        if (verbose) Debug.Log("[GameBootstrap] OnJoinedLobby → 로비 진입 완료");

        // (선택) 할 일 끝났으면 자신을 정리
        // 프로젝트에서 이후에도 뭔가 더 시키고 싶다면 Destroy하지 말고 남겨도 됩니다.
        Destroy(gameObject);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[GameBootstrap] OnDisconnected: {cause}");
        // 여기서 재시도 로직을 넣을 수도 있지만, 보통은 상위 매니저가 책임집니다.
    }

    // ===== 내부 유틸 =====
    private void TryEnterLobby()
    {
        // NetworkManager가 있으면 그쪽 루틴 사용
        var nm = FindObjectOfType<Network.NetworkManager>(true);
        if (nm != null)
        {
            if (verbose) Debug.Log("[GameBootstrap] 이미 연결됨 → NetworkManager.TryJoinLobby()");
            nm.TryJoinLobby();
            return;
        }

        // 없으면 직접 로비 진입 시도
        if (autoJoinLobby && !PhotonNetwork.InLobby && PhotonNetwork.NetworkClientState != ClientState.JoiningLobby)
        {
            if (verbose) Debug.Log("[GameBootstrap] 이미 연결됨 → JoinLobby()");
            PhotonNetwork.JoinLobby();
        }
    }
}
