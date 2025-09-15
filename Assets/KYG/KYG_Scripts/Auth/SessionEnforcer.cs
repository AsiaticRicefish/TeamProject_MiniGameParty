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
/// - /sessions/{uid}에 deviceId/ts를 기록하고 onDisconnect로 자동 해제
/// - 다른 기기에서 동일 UID로 로그인하면 소유권을 가져가며, 기존 기기는 DB 변경 수신 즉시 종료
/// </summary>
public class SessionEnforcer : MonoBehaviour
{
    public static SessionEnforcer Instance { get; private set; }

    [Header("Realtime DB URL (콘솔에서 복사한 URL)")]
    [SerializeField] private string databaseUrl = "https://<your-project-id>.firebaseio.com";

    [Header("Keepalive(ms) / 초기 지연(ms)")]
    [SerializeField] private int heartbeatMs = 15_000;
    [SerializeField] private int firstBeatDelayMs = 500;

    private FirebaseDatabase _db;
    private DatabaseReference _node; // /sessions/{uid}
    private string _uid;
    private string _deviceId;
    private bool _watching;
    private bool _claimed;
    
    public static bool KickedByRemote { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>로그인 성공 직후 한번 호출: UID 단일 세션 시작</summary>
    public async Task<bool> StartForUidAsync(string uid)
    {
        KickedByRemote = false;   // 새 세션 시도마다 초기화
        _uid = uid;
        if (string.IsNullOrEmpty(_uid))
        {
            Debug.LogError("[SessionEnforcer] uid가 비어있습니다.");
            return false;
        }

        var app = FirebaseApp.DefaultInstance;
        if (app == null)
        {
            Debug.LogError("[SessionEnforcer] FirebaseApp 초기화 필요.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(databaseUrl) || !databaseUrl.StartsWith("https://"))
        {
            Debug.LogError("[SessionEnforcer] databaseUrl 설정이 올바르지 않음.");
            return false;
        }

        _db = FirebaseDatabase.GetInstance(app, databaseUrl);
        _node = _db.RootReference.Child("sessions").Child(_uid);

        // 안정적인 기기 식별자
        _deviceId = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrEmpty(_deviceId))
            _deviceId = System.Guid.NewGuid().ToString("N");

        // 1) onDisconnect: 내 세션 자동 정리
        try { await _node.OnDisconnect().RemoveValue(); } catch { /* ignore */ }

        // 2) 소유권 강제 획득(= 새 로그인 우선권, 기존 기기는 감지 후 종료)
        bool ok = await ClaimAsync();
        if (!ok)
        {
            Debug.LogError("[SessionEnforcer] 세션 소유권 획득 실패");
            return false;
        }

        // 3) 변경 감시(다른 기기가 내 uid 세션을 뺏으면 즉시 로그아웃)
        BeginWatch();

        // 4) Heartbeat(주기적으로 ts 갱신)
        StartCoroutine(CoHeartbeat());

        return true;
    }

    private async Task<bool> ClaimAsync()
    {
        _claimed = false;
        try { _db.GoOnline(); } catch { /* ignore */ }

        // 경합 판별용 nonce(임의 토큰) 추가: 새 로그인 우선권이므로 그냥 덮어쓰기
        string nonce = Guid.NewGuid().ToString("N");

        try
        {
            // 한 번에 쓰기 (deviceId / ts / nonce)
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

        // 최종 검증: 내가 방금 쓴 값이 그대로 있는지 확인
        try
        {
            var snap = await _node.GetValueAsync();
            var curDev = snap?.Child("deviceId")?.Value as string;
            var curNonce = snap?.Child("nonce")?.Value as string;

            _claimed = string.Equals(curDev, _deviceId, StringComparison.Ordinal)
                       && string.Equals(curNonce, nonce, StringComparison.Ordinal);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SessionEnforcer] Claim read 예외: {e.Message}");
            _claimed = false;
        }

        if (_claimed) Debug.Log($"[SessionEnforcer] 세션 소유권 획득: uid={_uid}, deviceId={_deviceId}");
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
            
            KickedByRemote = true;  // 강제 종료 플래그 세움
            try { FirebaseAuth.DefaultInstance.SignOut(); } catch { /* ignore */ }
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();

            // UI가 있다면 팝업 표시 로직 연결 (예: Toast/Popup)
            // Toast.Show("다른 기기에서 로그인하여 연결이 종료되었습니다.");

            // 내 세션 감시는 더 이상 필요 없음
            StopAll();
        }
    }

    private IEnumerator CoHeartbeat()
    {
        if (firstBeatDelayMs > 0) yield return new WaitForSeconds(firstBeatDelayMs / 1000f);
        while (_claimed && _node != null)
        {
            _node.Child("ts").SetValueAsync(NowMs()); // 간단 heartbeat
            yield return new WaitForSeconds(heartbeatMs / 1000f);
        }
    }

    public void StopAll()
    {
        if (_node != null)
            _node.ValueChanged -= OnSessionChanged;

        _watching = false;
        _claimed = false;
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
