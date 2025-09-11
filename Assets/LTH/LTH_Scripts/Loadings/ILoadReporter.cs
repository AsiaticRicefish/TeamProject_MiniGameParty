using System.Threading;
using Cysharp.Threading.Tasks;

public interface ILoadReporter
{
    /// 0~1 범위의 누적 진행률
    float Progress { get; }

    /// 현재 단계 텍스트(상태 텍스트로 표시용, 선택)
    string CurrentStep { get; }

    /// 초기화 시작 내부에서 Addressables 로드/캐시 준비/오브젝트 생성 등 수행
    UniTask BeginAsync(CancellationToken ct);
}
