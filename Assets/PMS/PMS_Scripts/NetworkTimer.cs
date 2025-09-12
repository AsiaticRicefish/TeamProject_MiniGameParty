using System;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using DesignPattern;

public class NetworkTimer
{
    private double startAt;
    private double endAt;

    private CancellationTokenSource cts;

    private bool running;

    public event Action OnTimerStart; // 타이머 시작 시 이벤트
    public event Action<int> OnTick;  // 남은 시간 UI 갱신용
    public event Action OnTimerEnd;   // 타이머 종료 시 이벤트

    public void OnStartTimer(double startAt, double endAt)
    {
        this.startAt = startAt; 
        this.endAt = endAt;
        StartTimer().Forget();
    }

    public async UniTask StartTimer()
    {
        // 이전 타이머 정리
        StopTimer();
        while (running) await UniTask.Yield();

        cts = new CancellationTokenSource();
        running = true;

        try
        {
            await RunTimerAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("타이머가 취소되었습니다");
        }
        catch (Exception ex)
        {
            Debug.LogError($"타이머 실행 중 오류: {ex}");
        }
        finally
        {
            running = false;
        }
    }

    public void StopTimer()
    {
        OnTimerEnd?.Invoke(); // 강제 종료 시에도 이벤트 호출

        cts?.Cancel();
        cts?.Dispose();
        cts = null;
        running = false; // 타이머 상태 즉시 변경
    }

    private void ResetTimer()
    {
        startAt = 0;
        endAt = 0;
    }

    private async UniTask RunTimerAsync(CancellationToken token)
    {
        Debug.Log("[NetworkTimer] - 타이머 실행");
        OnTimerStart?.Invoke(); //게임 타이머 시작을 알림
        int lastTick = -1;

        try
        {
            // 시작 시간이 이미 지났다면 즉시 시작
            if (PhotonNetwork.Time >= startAt)
            {
                Debug.Log($"시작 시간이 이미 지났습니다. 즉시 타이머 시작 현재시간 {PhotonNetwork.Time}시작 시간:{startAt}");
            }
            else
            {
                Debug.Log("대기중");
            }
            // 시작 시간까지 대기
            await UniTask.WaitUntil(() => PhotonNetwork.Time >= startAt ,cancellationToken: token);

            // 종료 시간까지 매 프레임 체크
            while (PhotonNetwork.Time < endAt && !token.IsCancellationRequested)
            {
                int remaining = Mathf.RoundToInt((float)(endAt - PhotonNetwork.Time)); //내림 처리
                remaining = Mathf.Max(1, remaining); // 최소 1초 이상
                if (remaining != lastTick)
                {
                    lastTick = remaining;
                    OnTick?.Invoke(remaining);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (!token.IsCancellationRequested)
            {
                OnTimerEnd?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // 취소된 경우 무시
        }
    }

    private void OnDestroy()
    {
        StopTimer();
    }
}
