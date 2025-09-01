using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

public static class NicknameRegistry
{
    private static DatabaseReference Root => FirebaseDatabase.DefaultInstance.RootReference;

    public static async Task<bool> TryReserveAsync(string uid, string rawName) // 트랜잭션으로 빈 슬롯일 때만 닉네임 기록
    {
        if (string.IsNullOrWhiteSpace(uid)) return false;

        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return false;

        var node = Root.Child("nicknames").Child(key);

        // 트랜잭션: 비어있을 때만 내 uid/ts를 기록
        try
        {
            // Unity Firebase SDK: RunTransaction(...) -> Task<DataSnapshot>
            // 'Committed' 같은 플래그는 없으므로, 사후 스냅샷으로 판정
            await node.RunTransaction(mutable =>
            {
                if (mutable.Value == null)
                {
                    mutable.Value = new Dictionary<string, object>
                    {
                        { "uid", uid },
                        { "ts", NowMs() }
                    };
                    return TransactionResult.Success(mutable);
                }
                return TransactionResult.Abort();
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NicknameRegistry] RunTransaction error: {e.Message}");
            // 트랜잭션 실패 → 그냥 선점 실패 취급
        }

        // 스냅샷을 읽어 최종 승자 확인
        try
        {
            var snap = await node.GetValueAsync();
            var winner = snap?.Child("uid")?.Value as string;
            return string.Equals(winner, uid, StringComparison.Ordinal);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NicknameRegistry] GetValueAsync error: {e.Message}");
            return false;
        }
    }

    public static Task BindOnDisconnectCleanupAsync(string rawName) // 접속 끊기면 자동 해제
    {
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return Task.CompletedTask;

        var node = Root.Child("nicknames").Child(key);
        // ✅ 올바른 API: OnDisconnect().RemoveValue()
        return node.OnDisconnect().RemoveValue();
    }

    public static Task ReleaseAsync(string rawName) // 앱 종료/닉네임 변경 시 수동 해제
    {
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key)) return Task.CompletedTask;

        var node = Root.Child("nicknames").Child(key);
        return node.RemoveValueAsync();
    }

    private static string Normalize(string s)
        => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
