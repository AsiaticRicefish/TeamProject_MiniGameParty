using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using DesignPattern; 
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using LDH_MainGame;

public class TaskSyncManager : PunSingleton<TaskSyncManager>
{
    //ShootingGamePlayerPropertyKeys

    // 모든 클라이언트: 작업 완료 시 호출
    public void SetTaskDone(ShootingGamePlayerPropertyKeys.TaskType tasktype)
    {
        var props = new PhotonHashtable();
        string key = ShootingGamePlayerPropertyKeys.TaskKeys[tasktype];
        props[key] = true; // 내 UserId 기준으로 완료 처리
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    // 마스터만 확인하는 함수
    public bool AreAllTasksDone(ShootingGamePlayerPropertyKeys.TaskType tasktype)
    {
        string key = ShootingGamePlayerPropertyKeys.TaskKeys[tasktype];
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (!p.CustomProperties.ContainsKey(key) || !(bool)p.CustomProperties[key])
            {
                return false; // 아직 안 끝난 플레이어 있음
            }
        }
        return true;
    }

    // 마스터: 다른 플레이어의 상태 업데이트 감지
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (AreAllTasksDone(ShootingGamePlayerPropertyKeys.TaskType.Initialized))
        {
            Debug.Log("모든 플레이어 초기화 완료!");
            MainGameManager.Instance?.NotifyMiniGameStart();
            RoomPropertyObserver.Instance.SetRoomProperty(ShootingGamePropertyKeys.State, "CardSelectState");
            ResetAllPlayerTasks(ShootingGamePlayerPropertyKeys.TaskType.Initialized);
        }
    }

    public void ResetAllPlayerTasks(ShootingGamePlayerPropertyKeys.TaskType tasktype)
    {
        string key = ShootingGamePlayerPropertyKeys.TaskKeys[tasktype];
        foreach (var p in PhotonNetwork.PlayerList)
        {
            var props = new PhotonHashtable();
            props[key] = false;
            p.SetCustomProperties(props);
        }
    }
}
