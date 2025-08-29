using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace KYG.Net
{
    /// <summary>
    /// UID 기준으로 Photon Player와 게임 내 객체를 매핑/조회하는 중앙 레지스트리
    /// - OnJoinedRoom/OnPlayerEntered/Left 에서 갱신
    /// - 게임 로직에서 UID만 알면 Player/슬롯/오너쉽 등을 역으로 찾을 수 있음
    /// </summary>
    public class PlayerDirectory : MonoBehaviour
    {
        public static PlayerDirectory Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // UID -> Photon Player
        private readonly Dictionary<string, Player> _uidToPlayer = new();
        // ActorNumber -> UID (역조회)
        private readonly Dictionary<int, string> _actorToUid = new();

        public string LocalUid { get; private set; }  // 빠른 접근

        public event Action<Player> RegistryChanged; // UI 리빌드 등에서 쓰기 편함

        public void RebuildAll()
        {
            _uidToPlayer.Clear();
            _actorToUid.Clear();

            foreach (var p in PhotonNetwork.PlayerList)
            {
                var uid = p.GetUidSafe();
                if (string.IsNullOrEmpty(uid)) continue;

                _uidToPlayer[uid] = p;
                _actorToUid[p.ActorNumber] = uid;
            }

            var localUid = PhotonNetwork.LocalPlayer?.GetUidSafe();
            if (!string.IsNullOrEmpty(localUid)) LocalUid = localUid;

            RegistryChanged?.Invoke(PhotonNetwork.LocalPlayer);
        }

        public void Upsert(Player p)
        {
            if (p == null) return;
            var uid = p.GetUidSafe();
            if (string.IsNullOrEmpty(uid)) return;

            _uidToPlayer[uid] = p;
            _actorToUid[p.ActorNumber] = uid;

            if (p.IsLocal) LocalUid = uid;
            RegistryChanged?.Invoke(p);
        }

        public void Remove(Player p)
        {
            if (p == null) return;
            if (_actorToUid.TryGetValue(p.ActorNumber, out var uid))
            {
                _actorToUid.Remove(p.ActorNumber);
                if (_uidToPlayer.TryGetValue(uid, out var cur) && cur == p)
                    _uidToPlayer.Remove(uid);
            }
            RegistryChanged?.Invoke(p);
        }

        public Player GetPlayerByUid(string uid)
            => string.IsNullOrEmpty(uid) ? null : (_uidToPlayer.TryGetValue(uid, out var p) ? p : null);

        public string GetUidByActor(int actorNumber)
            => _actorToUid.TryGetValue(actorNumber, out var uid) ? uid : null;

        public IReadOnlyDictionary<string, Player> UidMap => _uidToPlayer;
    }
}
