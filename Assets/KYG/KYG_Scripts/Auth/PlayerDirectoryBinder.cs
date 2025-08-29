using KYG.Net;            // PlayerDirectory
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// PlayerDirectory 를 Photon 이벤트에 자동 연동:
/// - 방 입장: 전체 리빌드
/// - 플레이어 입/퇴장: 업서트/삭제
/// - 플레이어 프로퍼티 변경(특히 uid): 업서트
/// - 방 퇴장: 맵 초기화(다음 룸 대비)
/// </summary>
public class PlayerDirectoryBinder : MonoBehaviourPunCallbacks
{
    private PlayerDirectory Dir => PlayerDirectory.Instance;

    private static PlayerDirectoryBinder _instance;
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnJoinedRoom()
    {
        Dir?.RebuildAll();
        Debug.Log("[PlayerDirectoryBinder] RebuildAll on JoinedRoom");
    }

    public override void OnLeftRoom()
    {
        Dir?.RebuildAll(); // 비움과 동일(플레이어 리스트 0)
        Debug.Log("[PlayerDirectoryBinder] RebuildAll on LeftRoom");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Dir?.Upsert(newPlayer);
        Debug.Log($"[PlayerDirectoryBinder] Upsert on Enter: {newPlayer?.NickName}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Dir?.Remove(otherPlayer);
        Debug.Log($"[PlayerDirectoryBinder] Remove on Left: {otherPlayer?.NickName}");
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // uid/슬롯/레디 등 바뀌었을 때 UI 갱신이 자연스럽도록 항상 업서트
        Dir?.Upsert(targetPlayer);
    }
}