using UnityEngine;
using Firebase;
using Cysharp.Threading.Tasks;

/// <summary>
/// [전역 단일 초기화 게이트]
/// - FirebaseApp.CheckAndFixDependenciesAsync()를 앱 전체에서 "딱 1번"만 수행
/// - 나머지 스크립트는 이 게이트가 끝날 때까지 기다림(경합 방지)
/// - 실패하면 false 반환, 성공하면 true
/// </summary>
public static class FirebaseInitGate
{
    private static bool _started;
    private static bool _ready;
    private static UniTaskCompletionSource<bool> _tcs;

    /// <summary>
    /// Firebase을 사용할 모든 코드의 맨 앞에서 호출하세요.
    /// ex) if (!await FirebaseInitGate.EnsureReadyAsync()) return;
    /// </summary>
    public static async UniTask<bool> EnsureReadyAsync()
    {
        // 이미 성공한 적 있으면 즉시 true
        if (_ready) return true;

        // 누군가 시작한 적 있으면 그 결과를 기다림
        if (_started && _tcs != null)
            return await _tcs.Task;

        // 최초 시작
        _started = true;
        _tcs = new UniTaskCompletionSource<bool>();

        var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dep != DependencyStatus.Available)
        {
            Debug.LogError($"[FirebaseInitGate] dependencies: {dep}");
            _tcs.TrySetResult(false);
            return false;
        }

        _ready = true;
        _tcs.TrySetResult(true);
        Debug.Log("[FirebaseInitGate] Firebase ready.");
        return true;
    }
}