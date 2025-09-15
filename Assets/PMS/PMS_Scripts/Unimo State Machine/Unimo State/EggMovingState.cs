using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EggMovingState : UnimoEggStateBase
{
    public override UnimoEggStateType StateType => UnimoEggStateType.Moving;
    
    public override void Enter(UnimoStateController egg)
    {

    }

    public override void Tick(UnimoStateController egg)
    {

    }

    public override void FixedTick(UnimoStateController egg)
    {
        if (egg.rb.velocity.magnitude < egg.stopSpeed)
        {
            egg.RequestStateChange(UnimoEggStateType.Stopped);
        }
    }
}
