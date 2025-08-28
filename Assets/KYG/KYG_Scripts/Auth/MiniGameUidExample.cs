using KYG.Net;        // PlayerDirectory
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// 미니게임 진입 시 내 UID/동료 UID를 확인하고, UID 기준으로 접근하는 샘플
/// - 실제 게임 로직에서는 UID로 팀/슬롯/점수판/오너쉽 등 연결
/// </summary>
public class MiniGameUidExample : MonoBehaviour
{
    void Start()
    {
        var dir = PlayerDirectory.Instance;
        if (dir == null)
        {
            Debug.LogWarning("[MiniGameUidExample] PlayerDirectory not found.");
            return;
        }

        // 내 UID
        string myUid = dir.LocalUid;
        Debug.Log($"[MiniGameUidExample] My UID: {myUid}");

        // 내 Photon Player 역조회
        Player me = dir.GetPlayerByUid(myUid);
        Debug.Log($"[MiniGameUidExample] My Player: #{me?.ActorNumber} {me?.NickName}");

        // 다른 모든 플레이어 순회
        foreach (var kv in dir.UidMap)
        {
            string uid = kv.Key;
            Player p = kv.Value;
            Debug.Log($"[MiniGameUidExample] UID:{uid} -> Player:{p?.NickName} (actor:{p?.ActorNumber})");
        }

        // 예: 특정 게임 오브젝트 디렉토리(별도 매니저)에서 UID로 소유자 오브젝트 찾기 등의 패턴으로 확장
    }
}