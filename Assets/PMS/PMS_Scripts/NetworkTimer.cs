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
    public event Action OnTimerCancel; // 타이머 강제 종료시 이벤트

    public void OnStartTimer(double startAt, double endAt)
    {
        if (endAt <= startAt)
        {
            Debug.LogError("endAt must be greater than startAt");
            return;
        }

        this.startAt = startAt;
        this.endAt = endAt;
        StartTimer().Forget();
    }

    public async UniTask StartTimer()
    {
        CancelTimer();
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
            //취소든 정상 종료든 무조건 여기로 옴
            cts?.Dispose();
            cts = null;
            running = false;         
        }
    }
    

    public void CancelTimer()
    {
        if (!running) return; // 이미 중단된 상태라면 무시
        cts?.Cancel();
        // 이벤트 알림은 RunTimerAsync → catch 에서 처리
    }

    private void ResetEvents()
    {
        OnTimerStart = null;
        OnTick = null;
        OnTimerEnd = null;
        OnTimerCancel = null;
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
                int remaining = Mathf.CeilToInt((float)(endAt - PhotonNetwork.Time)); 
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
            OnTimerCancel?.Invoke();
        }
    }
}
