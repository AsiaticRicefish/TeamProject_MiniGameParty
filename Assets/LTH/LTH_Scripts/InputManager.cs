using System;
using System.Collections;
using System.Collections.Generic;
using DesignPattern;
using UnityEngine;

namespace InputBlocker
{
    /// <summary>
    /// 입력 타입 플래그 (비트 플래그 조합 가능)
    /// Move, Interaction, UI 입력을 개별적으로 혹은 조합해서 차단할 수 있음
    /// </summary>

    [Flags]
    public enum InputType
    {
        None = 0,
        Move = 1 << 0,
        Interaction = 1 << 1,
        UI = 1 << 2,
        All = ~0
    }

    /// <summary>
    /// IDisposable 기반 입력 잠금 토큰
    /// - Acquire()로 생성 시 활성화
    /// - Dispose()로 해제 (using, try/finally 사용 권장)
    /// </summary>
    public sealed class InputLockToken : IInputLockHandler, IDisposable
    {
        public readonly InputType mask;                         // 어떤 입력을 차단하는지
        public readonly string reason;                          // 디버깅/로그용 사유
        private readonly Action<InputLockToken> _onDispose;     // Dispose 시 호출할 콜백
        private bool _active = false;                           // 현재 잠금 활성화 여부

        public InputLockToken(InputType mask, string reason, Action<InputLockToken> onDispose)
        {
            this.mask = mask;
            this.reason = reason;
            _onDispose = onDispose;
        }

        // 지정된 입력 타입이 현재 차단 중인지 여부
        public bool IsInputBlocked(InputType inputType) => (_active && (mask & inputType) != 0);

        // InputManager.StartInput() 시 호출 → 잠금 활성화
        public bool OnInputStart() { _active = true; return true; }

        // InputManager.EndInput() 시 호출 → 잠금 비활성화
        public bool OnInputEnd() { _active = false; return true; }

        // Dispose 시 InputManager로 해제 요청
        public void Dispose()
        {
            _onDispose?.Invoke(this);
        }
    }

    /// <summary>
    /// 입력 차단/해제 매니저 (CombinedSingleton)
    /// - 여러 토큰을 동시에 관리
    /// - 특정 타입이 하나라도 차단되면 전체적으로 차단 상태로 간주
    /// </summary>
    public class InputManager : CombinedSingleton<InputManager>, IGameComponent
    {
        private readonly List<IInputLockHandler> _activeLocks = new(); // 현재 활성화된 잠금 목록
        public bool IsInitialized { get; private set; }

        protected override void OnAwake()
        {
            // 지금 현재는 전역 유지용으로 사용하지 않고 있음
            // isPersistent = true;로 설정하면 씬 이동 시에도 유지될 순 있음
            isPersistent = false;
        }

        // ==== IGameComponent: 순차 초기화 진입점 ====
        public void Initialize()
        {
            if (IsInitialized) return;

            // 씬 진입 시 혹시 남아있는 잠금 초기화
            ResetAllLocks();

            IsInitialized = true;
            Debug.Log("[InputManager] Initialize complete");
        }

        /// <summary>
        /// 특정 토큰을 활성화 상태로 등록
        /// </summary>

        public void StartInput(IInputLockHandler startInputRequest)
        {
            if (!_activeLocks.Contains(startInputRequest))
            {
                _activeLocks.Add(startInputRequest);
                startInputRequest.OnInputStart();
            }
        }

        /// <summary>
        /// 특정 토큰을 비활성화 상태로 등록 해제
        /// </summary>
        public void EndInput(IInputLockHandler endInputRequest)
        {
            if (_activeLocks.Contains(endInputRequest))
            {
                _activeLocks.Remove(endInputRequest);
                endInputRequest.OnInputEnd();
            }
        }

        /// <summary>
        /// 현재 특정 타입의 입력이 차단되었는지 확인
        /// </summary>

        public bool IsBlocked(InputType type)
        {
            foreach (var handler in _activeLocks)
                if (handler.IsInputBlocked(type)) return true;
            return false;
        }


        /// <summary>
        /// 모든 입력 잠금 강제 해제 (씬 종료/리셋 시)
        /// </summary>
        public void ResetAllLocks()
        {
            foreach (var h in _activeLocks.ToArray())
                EndInput(h);
            _activeLocks.Clear();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // 씬 종료 시 전체 잠금 초기화
            ResetAllLocks();
            IsInitialized = false;
        }

        /// <summary>
        /// 새로운 입력 잠금 토큰 발급
        /// - 반드시 Dispose()로 해제 필요
        /// </summary>
        public InputLockToken Acquire(InputType mask, string reason = "")
        {
            var token = new InputLockToken(mask, reason, OnTokenDisposed);
            StartInput(token);
            return token;
        }


        /// <summary>
        /// using 스코프에서 사용할 수 있는 편의 메서드
        /// - using (InputManager.Instance.Scope(...)) { ... }
        /// </summary>
        public IDisposable Scope(InputType mask, string reason = "")
        {
            var token = Acquire(mask, reason);
            return token;
        }


        /// <summary>
        /// 토큰이 Dispose() 될 때 자동 호출
        /// </summary>
        private void OnTokenDisposed(InputLockToken token)
        {
            EndInput(token);
        }
    }
}