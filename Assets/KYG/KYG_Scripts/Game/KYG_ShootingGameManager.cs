// ShootingGameManager.cs (신규)
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using DesignPattern;


namespace KYG
{
    [RequireComponent(typeof(PhotonView))]
    public class ShootingGameManager : PunSingleton<ShootingGameManager>
    {
        private readonly HashSet<int> _eliminated = new(); // ActorNumber
        public int ActiveCount => PhotonNetwork.CurrentRoom != null
            ? PhotonNetwork.CurrentRoom.PlayerCount - _eliminated.Count
            : 0;

        public bool IsEliminated(int actor) => _eliminated.Contains(actor);
        public bool IsGameOver() => ActiveCount <= 1;

        // 마스터만 호출
        public void Eliminate(int actor)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_eliminated.Contains(actor)) return;

            _eliminated.Add(actor);

            // ✅ PlayerRootManager 업데이트
            var prm = UnityEngine.Object.FindObjectOfType<KYG.PlayerRootManager>(true);
            if (prm && prm.enabled)
                prm.RefreshVisibility_AllExceptEliminated();

            // TODO: 방 속성 "elim" 기록 → 후입장 플레이어 복구할 수 있도록 하면 완벽
            // PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { "elim", _eliminated.ToArray() } });

            if (IsGameOver())
                AnnounceWinnerAndEnd();
        }

        private void AnnounceWinnerAndEnd()
        {
            // 남아있는 Actor(=승자)
            int winnerActor = -1;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (!_eliminated.Contains(p.ActorNumber))
                {
                    winnerActor = p.ActorNumber;
                    break;
                }
            }

            // 룸 상태 전환 (기존 TurnManager의 종료 상태 키와 정합)
            RoomPropertyObserver.Instance?.SetRoomProperty(
                ShootingGamePropertyKeys.State, "CheckGameWinnerState"
            ); // TurnManager에 이미 동일 문자열 사용 중 :contentReference[oaicite:2]{index=2}

            // TODO: 승리 연출/씬 전환
            // e.g., SceneManager.LoadScene("VictoryScene");
            Debug.Log($"[ShootingGameManager] Winner Actor = {winnerActor}");
        }
    }
}