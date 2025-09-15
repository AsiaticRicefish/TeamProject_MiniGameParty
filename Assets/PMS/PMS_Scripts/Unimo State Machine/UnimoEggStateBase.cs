using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class UnimoEggStateBase : IUnimoEggState
{
    //Local 환경에서 할려면 
    public abstract UnimoEggStateType StateType { get; }

    public virtual void Enter(UnimoStateController egg) { Debug.Log($"[UnimoEggState] - {StateType} 상태 Enter"); }

    public virtual void Tick(UnimoStateController egg) { Debug.Log($"[UnimoEggState] - {StateType} 상태 Tick"); }

    public virtual void FixedTick(UnimoStateController egg) { Debug.Log($"[UnimoEggState] - {StateType} 상태 FixedTick"); }

    public virtual void Exit(UnimoStateController egg) { Debug.Log($"[UnimoEggState] - {StateType} 상태 Exit"); }
}
