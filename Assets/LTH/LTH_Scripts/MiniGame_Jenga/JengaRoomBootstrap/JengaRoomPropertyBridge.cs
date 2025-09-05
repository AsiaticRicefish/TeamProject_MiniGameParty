using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class JengaRoomPropertyBridge : MonoBehaviourPunCallbacks
{
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        var gm = JengaGameManager.Instance;
        if (gm == null) return;

        // 1) 상태 변경 반영 (네트워크 브로드캐스트의 보조 안전망)
        if (JengaRoomProps.TryGet<byte>(propertiesThatChanged, JengaRoomProps.KEY_STATE, out var stateByte))
        {
            var newState = (JengaGameState)stateByte;
            if (gm.currentState != newState)
                gm.ApplyGameStateChange(newState);
        }

        // 2) 타이머 동기화 (START_TIME + DURATION → 남은 시간 역산)
        if (JengaRoomProps.TryGet<double>(propertiesThatChanged, JengaRoomProps.KEY_START_TIME, out var startTime))
        {
            double duration = 0.0;
            if (JengaRoomProps.TryGet<double>(propertiesThatChanged, JengaRoomProps.KEY_DURATION, out var dDouble))
                duration = dDouble;
            else if (JengaRoomProps.TryGet<int>(propertiesThatChanged, JengaRoomProps.KEY_DURATION, out var dInt))
                duration = dInt;
            else if (JengaRoomProps.TryGet<float>(propertiesThatChanged, JengaRoomProps.KEY_DURATION, out var dFloat))
                duration = dFloat;

            if (duration > 0.0)
                gm.ApplySyncedTimerFromRoomProps(startTime, duration);
        }

        // 3) 랭킹 수신 (메인 씬에서 주로 소비, 필요 시 젠가 씬에서도 활용 가능)
        if (JengaRoomProps.TryGet<string[]>(propertiesThatChanged, JengaRoomProps.KEY_RANK_UIDS, out var uidArr) &&
            JengaRoomProps.TryGet<int[]>(propertiesThatChanged, JengaRoomProps.KEY_RANK_VALS, out var rkArr))
        {
            if (uidArr != null && rkArr != null && uidArr.Length == rkArr.Length && uidArr.Length > 0)
            {
                var dict = uidArr.Zip(rkArr, (u, r) => (u, r)).ToDictionary(x => x.u, x => x.r);
                Debug.Log($"[JengaRoomPropertyBridge] Rankings received: {string.Join(", ", dict.Select(p => $"{p.Key}:{p.Value}"))}");
                // 필요하면 여기서 gm.OnGameFinished?.Invoke(dict); 등으로 UI 갱신 가능
            }
        }
    }

    // 재입장/재연결: 현재 룸 프로퍼티 스냅샷으로 복원
    public override void OnJoinedRoom()
    {
        var gm = JengaGameManager.Instance;
        if (gm == null || PhotonNetwork.CurrentRoom == null) return;

        var roomProps = PhotonNetwork.CurrentRoom.CustomProperties;

        if (JengaRoomProps.TryGet<byte>(roomProps, JengaRoomProps.KEY_STATE, out var stateByte))
            gm.ApplyGameStateChange((JengaGameState)stateByte);

        if (JengaRoomProps.TryGet<double>(roomProps, JengaRoomProps.KEY_START_TIME, out var startTime))
        {
            double duration = 0.0;
            if (JengaRoomProps.TryGet<double>(roomProps, JengaRoomProps.KEY_DURATION, out var dDouble))
                duration = dDouble;
            else if (JengaRoomProps.TryGet<int>(roomProps, JengaRoomProps.KEY_DURATION, out var dInt))
                duration = dInt;
            else if (JengaRoomProps.TryGet<float>(roomProps, JengaRoomProps.KEY_DURATION, out var dFloat))
                duration = dFloat;

            if (duration > 0.0)
                gm.ApplySyncedTimerFromRoomProps(startTime, duration);
        }
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        // 내가 새 마스터가 된 경우만 처리
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;

        var gm = JengaGameManager.Instance;
        var props = PhotonNetwork.CurrentRoom.CustomProperties;

        // 이미 기록돼 있으면 스킵
        bool hasStart = props.ContainsKey(JengaRoomProps.KEY_START_TIME);
        bool hasDur = props.ContainsKey(JengaRoomProps.KEY_DURATION);

        if (!hasStart || !hasDur)
        {
            // 현재 UI의 remainingTime과 gameTime을 이용해 startTime을 역산
            //   start ≈ now - (dur - remain)
            double dur = hasDur ? System.Convert.ToDouble(props[JengaRoomProps.KEY_DURATION])
                                : (double)gm.GetRemainingTime() + 1.0; // 안전값
            double now = PhotonNetwork.Time;
            double start = now - (dur - gm.GetRemainingTime());

            var set = new ExitGames.Client.Photon.Hashtable {
            { JengaRoomProps.KEY_START_TIME, start },
            { JengaRoomProps.KEY_DURATION,   dur },
        };
            PhotonNetwork.CurrentRoom.SetCustomProperties(set);
            // 모든 클라가 브릿지 통해 재계산해서 이어 달림
        }

        // 상태도 보정
        if (!props.ContainsKey(JengaRoomProps.KEY_STATE) && gm.currentState == JengaGameState.Playing)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable {
                { JengaRoomProps.KEY_STATE, (byte)JengaGameState.Playing }
                }
            );
        }
    }
}