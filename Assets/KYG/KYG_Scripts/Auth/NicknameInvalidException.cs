using System;

namespace KYG
{
    /// <summary>닉네임이 규칙(길이/허용문자/비속어 등)에 맞지 않을 때</summary>
    public class NicknameInvalidException : Exception
    {
        public NicknameInvalidException(string message) : base(message) { }
    }

    /// <summary>네트워크 지연/타임아웃 등 중복확인 처리 실패</summary>
    public class NicknameTimeoutException : Exception
    {
        public NicknameTimeoutException(string message) : base(message) { }
    }
}