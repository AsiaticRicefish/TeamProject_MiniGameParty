using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IUnimoEggState
{
    UnimoEggStateType StateType { get; }  // 반드시 구현해야 하는 프로퍼티 -> enum type기반 외부에서 접근 편하게 가능하도록
    void Enter(UnimoStateController egg);       // 상태 진입 시 호출
    void Tick(UnimoStateController egg);        // Update에서 호출
    void FixedTick(UnimoStateController egg);   // FixedUpdate에서 호출
    void Exit(UnimoStateController egg);        // 상태 종료 시 호출
}
