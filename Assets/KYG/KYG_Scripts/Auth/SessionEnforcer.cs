using System;
using System.Collections;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// UID 단일 세션 강제기(멀티 디바이스 동시 접속 불가)
/// - /sessions/{uid} 에 deviceId/ts/nonce 기록 + onDisconnect 자동 해제
/// - 동일 UID가 다른 기기에서 로그인하면 교체를 감지하여 즉시 로그아웃/Disconnect
/// - 로그인 성공 직후에만 시작되고, 로그아웃/비활성화 되면 모든 리스너/코루틴 즉시 종료
/// </summary>
public class SessionEnforcer : MonoBehaviour
{
    public static SessionEnforcer Instance { get; private set; }

    [Header("Realtime DB URL (콘솔에서 복사한 URL)")]
    [SerializeField] private string databaseUrl =
        "https://<your-project-id>.asia-southeast1.firebasedatabase.app";

    [Header("Keepalive(ms) / 초기 지연(ms)")]
    [SerializeField] private int heartbeatMs = 15_000;
    [SerializeField] private int firstBeatDelayMs = 500;

    private FirebaseDatabase _db;
    private DatabaseReference _node;   // /sessions/{uid}
    private string _uid;
    private string _deviceId;
    private bool _watching;
    private bool _claimed;

    private Coroutine _heartbeatCo;    // 하트비트 코루틴 핸들

    /// <summary>다른 기기에서 로그인 감지되어 강제 종료되었는지</summary>
    public static bool KickedByRemote { get; private set; }
    /// <summary>마지막 킥 발생 시각(UTC ms). 필요 시 최근 킥 여부 판단에 사용</summary>
    public static long LastKickAtMs { get; private set; }

    // ====== 부트/정적 초기화 ======
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetKickFlagsOnBoot() => ClearKickFlag();

    public static void ClearKickFlag()
    {
        KickedByRemote = false;
        LastKickAtMs = 0;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // ★ 인증 상태가 바뀌면(특히 로그아웃) 즉시 리스너/하트비트 정리
        try { FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged; } catch { }
    }

    private void OnDisable()
    {
        try { FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged; } catch { }
        StopAll();  // 씬 비활성/파괴 시에도 안전하게 정리
    }

    private void OnApplicationQuit()
    {
        StopAll();
        try { _db?.GoOffline(); } catch { }
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        // 로그아웃 또는 다른 UID로 전환되면 감시/하트비트 중지
        var user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null || (!string.IsNullOrEmpty(_uid) && user.UserId != _uid))
            StopAll();
    }
    
    // FirebaseDatabase 인스턴스를 안전하게 준비
    private bool EnsureDatabase()
    {
        var app = FirebaseApp.DefaultInstance;
        if (app == null)
        {
            Debug.LogError("[SessionEnforcer] FirebaseApp is null. Initialize Firebase first.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(databaseUrl) || !databaseUrl.StartsWith("https://"))
        {
            Debug.LogError("[SessionEnforcer] Invalid databaseUrl. Set Realtime DB URL in inspector.");
            return false;
        }

        _db = FirebaseDatabase.GetInstance(app, databaseUrl);
        if (_db == null)
        {
            Debug.LogError("[SessionEnforcer] FirebaseDatabase.GetInstance returned null.");
            return false;
        }
        try { _db.GoOnline(); } catch { }
        return true;
    }

    /// <summary>
    /// [로그인 성공 직후 한번] UID 단일 세션 시작
    /// </summary>
    public async Task<bool> StartForUidAsync(string uid)
    {
        if (!await FirebaseInitGate.EnsureReadyAsync())
        {
            Debug.LogError("Firebase not ready");
        }
        
        ClearKickFlag();
        _uid = uid;

        // (1) 인증 상태 확인
        var auth = FirebaseAuth.DefaultInstance;
        var cur  = auth?.CurrentUser;
        if (cur == null || cur.UserId != _uid)
        {
            Debug.LogWarning("[SessionEnforcer] Not authenticated or uid mismatch. Abort.");
            return false;
        }

        // (2) 과거 감시 정리
        StopAll();

        // (3) DB 핸들 준비 (★ 여기서 실패하면 절대 진행 안 함)
        if (!EnsureDatabase()) return false;

        // (4) 세션 노드 구성
        _node = _db.RootReference.Child("sessions").Child(_uid);
        if (_node == null)
        {
            Debug.LogError("[SessionEnforcer] sessions node is null.");
            return false;
        }

        // (5) onDisconnect 예약
        try { await _node.OnDisconnect().RemoveValue(); } catch { }

        // (6) 소유권 획득
        bool ok = await ClaimAsync();             // ClaimAsync 내부에서도 _node null 가드
        if (!ok)
        {
            Debug.LogWarning("[SessionEnforcer] Failed to claim session.");
            StopAll();
            return false;
        }

        // (7) 감시 시작 + 하트비트
        BeginWatch();
        _heartbeatCo = StartCoroutine(CoHeartbeat());
        return true;
    }

    private async Task<bool> ClaimAsync()
    {
        _claimed = false;
        if (_node == null) return false;     // ★ 방어

        string nonce = Guid.NewGuid().ToString("N");

        try
        {
            var payload = new System.Collections.Generic.Dictionary<string, object>
            {
                { "deviceId", _deviceId },
                { "ts", NowMs() },
                { "nonce", nonce }
            };
            await _node.UpdateChildrenAsync(payload);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SessionEnforcer] Claim write 예외: {e.Message}");
            return false;
        }

        try
        {
            var snap = await _node.GetValueAsync();
            var curDev   = snap?.Child("deviceId")?.Value as string;
            var curNonce = snap?.Child("nonce")?.Value as string;
            _claimed = (curDev == _deviceId) && (curNonce == nonce);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SessionEnforcer] Claim read 예외: {e.Message}");
            _claimed = false;
        }

        if (_claimed) Debug.Log($"[SessionEnforcer] 세션 소유권 획득: uid={_uid}");
        return _claimed;
    }

    private void BeginWatch()
    {
        if (_watching || _node == null) return;
        _watching = true;
        _node.ValueChanged += OnSessionChanged;
        Debug.Log("[SessionEnforcer] 세션 변경 감시 시작");
    }

    private async void OnSessionChanged(object sender, ValueChangedEventArgs e)
    {
        if (!_watching) return;

        var dev = e?.Snapshot?.Child("deviceId")?.Value as string;

        // 내 기기 소유가 아닌 상태가 되면 즉시 강제 로그아웃
        if (!string.IsNullOrEmpty(dev) && dev != _deviceId)
        {
            Debug.LogWarning("[SessionEnforcer] 세션이 다른 기기에 의해 교체됨 → 강제 로그아웃/Disconnect");

            KickedByRemote = true;
            LastKickAtMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            try { FirebaseAuth.DefaultInstance?.SignOut(); } catch { /* 무시 */ }
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();

            StopAll();
        }
    }

    private IEnumerator CoHeartbeat()
    {
        if (firstBeatDelayMs > 0) yield return new WaitForSeconds(firstBeatDelayMs / 1000f);
        while (_claimed && _node != null)
        {
            // 간단 heartbeat (권한 에러 시 자동으로 예외를 띄우지 않음)
            _node.Child("ts").SetValueAsync(NowMs());
            yield return new WaitForSeconds(heartbeatMs / 1000f);
        }
    }

    /// <summary>최근 N ms 내 킥이 있었는지</summary>
    public static bool WasKickedRecently(int windowMs = 10000)
    {
        if (!KickedByRemote && LastKickAtMs == 0) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return (now - LastKickAtMs) <= windowMs;
    }

    /// <summary>
    /// 리스너/하트비트/OnDisconnect 예약을 모두 종료.
    /// 로그아웃/씬 비활성화/권한 문제 등 모든 종료 경로에서 반드시 호출.
    /// </summary>
    public void StopAll()
    {
        // 하트비트 중지
        if (_heartbeatCo != null)
        {
            try { StopCoroutine(_heartbeatCo); } catch { }
            _heartbeatCo = null;
        }

        // 이벤트/OnDisconnect 정리
        try
        {
            if (_node != null)
            {
                try { _node.ValueChanged -= OnSessionChanged; } catch { }
                try { _ = _node.OnDisconnect().Cancel(); } catch { } 
            }
        }
        catch { }
        finally
        {
            _node = null;
            _watching = false;
            _claimed = false;
        }
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
