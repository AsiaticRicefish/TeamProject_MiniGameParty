using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using UnityEngine.SceneManagement;

/// <summary>
/// 세션 킥 오버레이 표시 관리자
/// - 로그인/타이틀 씬에서는 절대 표시하지 않음(즉시 리셋)
/// - 로그인 완료 상태 + 킥 플래그일 때만 표시
/// - overlayPrefab을 필요 시 런타임에 Instantiate (씬에 직접 두지 않아도 됨)
/// </summary>
public class SessionKickGuard : MonoBehaviourPunCallbacks
{
    [SerializeField] private SessionKickOverlay overlayPrefab;

    private SessionKickOverlay _overlay;
    private bool _shown;
    private static SessionKickGuard _instance;
    // 킥 감지 후 SignOut되어도 표시되도록 완화
    private const int KickWindowMs = 10000;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // (중요) 앱 시작 시점이 로그인/타이틀 씬일 수 있으므로 즉시 한 번 초기화
        var cur = SceneManager.GetActiveScene();
        if (IsLoginLikeScene(cur.name))
        {
            _shown = false;
            SessionEnforcer.ClearKickFlag();
            if (_overlay) _overlay.Hide();
        }

        // 씬 변경 시 로그인/타이틀 씬으로 들어오면 항상 리셋
        SceneManager.activeSceneChanged += (_, scene) =>
        {
            if (IsLoginLikeScene(scene.name))
            {
                _shown = false;
                SessionEnforcer.ClearKickFlag();
                if (_overlay) _overlay.Hide();
            }
        };
    }

    void Update()
    {
        if (_shown) return;
        if (ShouldShow())
        {
            _shown = true;
            EnsureOverlay();
            _overlay?.Show();
        }
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        if (_shown) return;
        if (ShouldShow())
        {
            _shown = true;
            EnsureOverlay();
            _overlay?.Show();
        }
    }

    private void EnsureOverlay()
    {
        if (_overlay != null) return;
        if (overlayPrefab != null)
        {
            _overlay = Instantiate(overlayPrefab);
            DontDestroyOnLoad(_overlay.gameObject);
        }
        else
        {
            Debug.LogError("[SessionKickGuard] overlayPrefab이 지정되지 않았습니다.");
        }
    }

    private bool ShouldShow()
    {
        // 로그인/타이틀류 씬에서는 아예 금지 (원래 의도 유지)
        var sceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(sceneName))
        {
            var s = sceneName.ToLowerInvariant();
            if (s.Contains("title") || s.Contains("login") || s.Contains("auth"))
                return false;
        }

        // 로그인 여부와 무관하게, 킥 플래그가 섰으면 표시
        if (SessionEnforcer.KickedByRemote) return true;

        // 또는 "최근"에 킥이 섰으면 표시(씬 전환/SignOut 타이밍 보정)
        return SessionEnforcer.WasKickedRecently(KickWindowMs);
    }

    // 프로젝트 명명 규칙에 맞춰 키워드는 필요시 수정
    private bool IsLoginLikeScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        sceneName = sceneName.ToLowerInvariant();
        return sceneName.Contains("title") || sceneName.Contains("login") || sceneName.Contains("auth");
    }
    
    
}
