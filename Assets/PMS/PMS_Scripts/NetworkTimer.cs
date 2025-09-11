using System;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using DesignPattern;

public class NetworkTimer : PunSingleton<NetworkTimer>
{
    private double startAt;
    private double endAt;
    private double lead = 0.3f;
    private CancellationTokenSource cts;

    private bool running;

    public event Action OnTimerStart; // 타이머 시작 시 이벤트
    public event Action<int> OnTick;  // 남은 시간 UI 갱신용
    public event Action OnTimerEnd;   // 타이머 종료 시 이벤트

    public void StartTimerNetworked(double durationSec)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC("RPC_StartTimer", RpcTarget.All, durationSec);
    }

    [PunRPC]
    private void RPC_StartTimer(double durationSec)
    {
        StartTimer(durationSec);
    }

    public async UniTaskVoid StartTimer(double durationSec)
    {
        // 이전 타이머 정리
        StopTimer();
        while (running) await UniTask.Yield();

        this.startAt = PhotonNetwork.Time + lead;
        this.endAt = startAt + durationSec;

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
        if (!running) return; // 이미 종료된 경우 무시

        cts?.Cancel();
        cts?.Dispose();
        cts = null;

        running = false; // 타이머 상태 즉시 변경
        OnTimerEnd?.Invoke(); // 강제 종료 시에도 이벤트 호출
    }

    private async UniTask RunTimerAsync(CancellationToken token)
    {
        OnTimerStart?.Invoke(); //게임 타이머 시작을 알림
        int lastTick = -1;

        try
        {
            // 시작 시간까지 대기
            await UniTask.WaitUntil(() => PhotonNetwork.Time >= startAt,cancellationToken: token);

            // 종료 시간까지 매 프레임 체크
            while (PhotonNetwork.Time < endAt && !token.IsCancellationRequested)
            {
                int remaining = Mathf.RoundToInt((float)(endAt - PhotonNetwork.Time));
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
