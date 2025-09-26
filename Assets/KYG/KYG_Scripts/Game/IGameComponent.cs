using System.Collections;

namespace KYG.Framework
{
    /// <summary>
    /// 한 프레임 내에 끝나는 초기화
    /// </summary>
    public interface IGameComponent
    {
        void Initialize();
    }

    /// <summary>
    /// 여러 프레임이 필요한(비동기 자원/네트워크 등) 초기화
    /// </summary>
    public interface ICoroutineGameComponent
    {
        IEnumerator InitializeCoroutine();
    }
}