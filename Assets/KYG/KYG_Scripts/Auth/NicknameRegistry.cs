using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Firebase;
using Firebase.Database;
using UnityEngine;

public static class NicknameRegistry
{
    // ★ 우리가 직접 만든 DB 인스턴스를 보관
    private static FirebaseDatabase _db;

    /// <summary>
    /// FirebaseApp과 URL로 Realtime DB를 명시적으로 바인딩.
    /// 게임 시작 시 1회 호출 필수.
    /// </summary>
    public static void ConfigureDatabase(string databaseUrl)
    {
        var app = FirebaseApp.DefaultInstance;
        if (app == null) throw new InvalidOperationException("FirebaseApp not initialized.");

        if (string.IsNullOrWhiteSpace(databaseUrl) || !databaseUrl.StartsWith("https://"))
            throw new ArgumentException("Invalid databaseUrl. Copy the URL from Firebase console.");

        _db = FirebaseDatabase.GetInstance(app, databaseUrl); // ← 핵심
        Debug.Log($"[NicknameRegistry] DB bound to: {_db.App.Options.DatabaseUrl}");
    }

    private static DatabaseReference Root
    {
        get
        {
            if (_db == null)
                throw new InvalidOperationException("[NicknameRegistry] ConfigureDatabase() must be called first.");
            return _db.RootReference;
        }
    }

    private static DatabaseReference NickRoot => Root.Child("nicknames");

    // ===== (그대로 사용) 예약 + 재시도 + 타임아웃 + 상세 로그 =====
    public static async Task<bool> TryReserveAsync(
        string uid, string rawName, int maxRetry = 3, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(uid)) return false;

        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return false;

        var node = NickRoot.Child(key);

        try { _db.GoOnline(); } catch { /* ignore */ }

        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            Debug.Log($"[NicknameRegistry] Reserve attempt {attempt}/{maxRetry}, key={key}, uid={uid}");

            try
            {
                await WithTimeout(
                    node.RunTransaction(mutable =>
                    {
                        if (mutable.Value == null)
                        {
                            Debug.Log($"[NicknameRegistry] TX SUCCESS path: key={key} by {uid}");
                            mutable.Value = new Dictionary<string, object>
                            {
                                { "uid", uid },
                                { "ts", NowMs() }
                            };
                            return TransactionResult.Success(mutable);
                        }
                        Debug.Log($"[NicknameRegistry] TX ABORT (taken) key={key}");
                        return TransactionResult.Abort();
                    }),
                    timeoutMs
                );
            }
            catch (TimeoutException) { Debug.LogWarning($"[NicknameRegistry] TX TIMEOUT key={key}"); }
            catch (Exception e)      { Debug.LogWarning($"[NicknameRegistry] TX ERROR key={key}: {e.Message}"); }

            try
            {
                var snap = await WithTimeout(node.GetValueAsync(), timeoutMs);
                var winner = snap?.Child("uid")?.Value as string;
                Debug.Log($"[NicknameRegistry] Winner for key={key} => {winner ?? "null"}");

                if (string.Equals(winner, uid, StringComparison.Ordinal)) return true;
                else if (!string.IsNullOrEmpty(winner)) return false;
            }
            catch (TimeoutException) { Debug.LogWarning($"[NicknameRegistry] SNAPSHOT TIMEOUT key={key}"); }
            catch (Exception e)      { Debug.LogWarning($"[NicknameRegistry] SNAPSHOT ERROR key={key}: {e.Message}"); }

            await Task.Delay(UnityEngine.Random.Range(50, 150));
        }
        return false;
    }

    public static async Task<bool> VerifyAsync(string uid, string rawName, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(uid)) return false;
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return false;

        var node = NickRoot.Child(key);
        var snap = await WithTimeout(node.GetValueAsync(), timeoutMs);
        var winner = snap?.Child("uid")?.Value as string;
        Debug.Log($"[NicknameRegistry] VERIFY winner key={key} => {winner ?? "null"}");
        return string.Equals(winner, uid, StringComparison.Ordinal);
    }
    
    public static async Task<bool> IsAvailableAsync(string rawName, int timeoutMs = 3000)
    {
        string key = Normalize(rawName);
        if (string.IsNullOrWhiteSpace(key)) return false; // 형식 오류는 가용(false)로 보지 않음

        var node = NickRoot.Child(key);
        var snap = await WithTimeout(node.GetValueAsync(), timeoutMs);
        // 값이 없으면 사용 가능(true)
        return snap == null || snap.Value == null;
    }

    public static async Task<bool> ReserveStrictAsync(string uid, string rawName, int maxRetry = 3, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(uid)) return false;
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return false;

        var node = NickRoot.Child(key);
        try { _db.GoOnline(); } catch { /* ignore */ }

        // 1) 기존 재시도 로직으로 '적어도 한 번' 트랜잭션을 시도
        bool touched = await TryReserveAsync(uid, rawName, maxRetry, timeoutMs);

        // 2) 최종 승자 안정화 판독(짧은 구간에서 값이 안 바뀌는지)
        var winner = await WaitStableWinnerAsync(
            node,
            settleMs: 600,
            readIntervalMs: 60,
            timeoutMs: Math.Min(timeoutMs, 1500)
        );

        // 3) 승자 확정: winner가 있으면 그 사람만 true. 없으면 내가 만졌으면(내가 먼저 쓴 흔적만 있고 아직 미정) true로 간주.
        if (!string.IsNullOrEmpty(winner))
            return string.Equals(winner, uid, StringComparison.Ordinal);

        return touched; // winner가 비어있지만 내가 실제로 썼던 흔적이 있다면 나를 승자로 간주(경계 케이스 보호)
    }

    public static Task BindOnDisconnectCleanupAsync(string rawName)
    {
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return Task.CompletedTask;
        return NickRoot.Child(key).OnDisconnect().RemoveValue();
    }
    
    public static async Task<bool> ReleaseIfOwnerAsync(string uid, string rawName, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(uid)) return false;
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return false;

        var node = NickRoot.Child(key);

        try { _db.GoOnline(); } catch { /* ignore */ }

        try
        {
            // 소유자(uid) 일치시에만 삭제하도록 시도
            await WithTimeout(
                node.RunTransaction(mutable =>
                {
                    if (mutable.Value is Dictionary<string, object> cur)
                    {
                        if (cur.TryGetValue("uid", out var v) && v is string owner && owner == uid)
                        {
                            // 내가 주인이면 삭제
                            mutable.Value = null;
                            return TransactionResult.Success(mutable);
                        }
                    }
                    // 남의 키거나 비어있으면 변경하지 않음
                    return TransactionResult.Abort();
                }),
                timeoutMs
            );
        }
        catch { /* 트랜잭션 타임아웃/예외는 아래 검증 단계에서 다시 판별 */ }

        // 최종 검증: 노드가 실제로 사라졌는지 확인
        try
        {
            var snap = await WithTimeout(node.GetValueAsync(), timeoutMs);
            // 값이 null이면 삭제 완료(내가 주인이었고 커밋됨)
            return snap == null || snap.Value == null;
        }
        catch
        {
            // 읽기 실패 시 삭제 성공 여부를 확정 못 하므로 보수적으로 false
            return false;
        }
    }

    public static Task ReleaseAsync(string rawName)
    {
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return Task.CompletedTask;
        return NickRoot.Child(key).RemoveValueAsync();
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, int ms)
    {
        var winner = await Task.WhenAny(task, Task.Delay(ms));
        if (winner == task) return await task;
        throw new TimeoutException();
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        string nfkc = s.Normalize(NormalizationForm.FormKC);
        return nfkc.Trim().ToLowerInvariant();
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
    private static async Task<string> WaitStableWinnerAsync(DatabaseReference node,
        int settleMs = 600, int readIntervalMs = 60, int timeoutMs = 1500)
    {
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string lastUid = null; long lastTs = 0; int stableCount = 0;

        while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start < timeoutMs)
        {
            var snap = await WithTimeout(node.GetValueAsync(), timeoutMs);
            var uid = snap?.Child("uid")?.Value as string;

            long ts = 0;
            var tsObj = snap?.Child("ts")?.Value;
            if (tsObj is long l) ts = l;
            else if (tsObj is IConvertible c) try { ts = c.ToInt64(null); } catch { ts = 0; }

            if (!string.IsNullOrEmpty(uid))
            {
                if (uid == lastUid && ts == lastTs)
                {
                    stableCount++;
                    if (stableCount >= 2)  // 연속 2회 동일하면 안정화로 간주
                        return uid;
                }
                else
                {
                    lastUid = uid; lastTs = ts; stableCount = 1;
                }
            }

            await Task.Delay(readIntervalMs);
        }

        // 시간 초과 시 마지막 관측값(있다면)을 반환
        return lastUid;
    }
}
