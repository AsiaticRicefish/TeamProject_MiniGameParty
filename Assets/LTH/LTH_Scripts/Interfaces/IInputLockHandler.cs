using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InputBlocker
{
    /// <summary>
    /// 입력 차단 핸들러 인터페이스
    /// - InputLockToken이 이를 구현
    /// - InputManager는 IInputLockHandler 목록을 관리
    /// </summary>
    public interface IInputLockHandler
    {
        bool IsInputBlocked(InputType inputType);   // 지정된 타입이 차단 중인지 여부
        bool OnInputStart();                        // 잠금 시작 시 호출
        bool OnInputEnd();                          // 잠금 해제 시 호출
    }

}