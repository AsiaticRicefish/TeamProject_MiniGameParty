using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ShootingScene;
using Photon.Pun;

public class EggStoppedState : UnimoEggStateBase
{
    public override UnimoEggStateType StateType => UnimoEggStateType.Stopped;

    public override void Enter(UnimoStateController egg)
    {
        ShootingCameraManager.Instance.StopFollowTarget();

        if (egg.photonView.IsMine && !egg.turnEnded)
        {
            egg.turnEnded = true;

            TurnManager.Instance.photonView.RPC("RequestTurnEnd", RpcTarget.MasterClient);
        }
    }
}
