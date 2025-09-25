using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class AdMobRewardButton : MonoBehaviour
{
    [SerializeField] private Button showRewardButton;
    [SerializeField] private TMP_Text rewardText;

    [SerializeField] private TMP_Text statusText; // 상태 표시용 텍스트
    [SerializeField] private TMP_Text logText;    // 로그 표시용 텍스트

    private int rewardCount = 0;
    private bool needsUIUpdate = false;

    private string currentStatus = "시작 중...";
    private string logMessages = "";

    private async void Start()
    {
        UpdateStatus("SDK 초기화 중...");

        // SDK 초기화 + 첫 로드
        await AdMobService.InitializeAsync();

        UpdateStatus("초기 광고 로드 완료");

        // 버튼 이벤트 등록
        if (showRewardButton != null)
            showRewardButton.onClick.AddListener(OnClickShowRewarded);

        UpdateRewardText();
        UpdateStatus("준비 완료 - 버튼을 누르세요");
    }

    private void Update()
    {
        // 메인 스레드에서 UI 업데이트 처리
        if (needsUIUpdate)
        {
            needsUIUpdate = false;
            UpdateRewardText();
            UpdateStatusUI();
        }
    }

    private async void OnClickShowRewarded()
    {
        UpdateStatus("버튼 클릭 - 광고 로드 시도 중...");
        AddLog("버튼 클릭됨");

        Debug.Log("[AdMob] Button clicked - loading fresh ad");

        // 기존 광고 무시하고 새로 로드
        bool loaded = await AdMobService.LoadRewardedAsync();
        if (!loaded)
        {
            UpdateStatus("광고 로드 실패!");
            AddLog("로드 실패");

            Debug.LogWarning("[AdMob] 광고 로드 실패");
            return;
        }

        UpdateStatus("광고 로드 성공 - 표시 중...");
        AddLog("로드 성공");

        // 로드 완료 즉시 표시 (최소한의 안정화 시간)
        await Task.Delay(100);

        bool shown = await AdMobService.ShowRewardedAsync(reward =>
        {
            Debug.Log($"[AdMob] Reward callback: {reward.Type}, {reward.Amount}");

            AddLog($"보상 받음: {reward.Type}, {reward.Amount}");
            rewardCount += (int)reward.Amount;
            needsUIUpdate = true;

            Debug.Log($"[AdMob] New reward count: {rewardCount}");
        });

        if (shown)
        {
            UpdateStatus("광고 시청 완료! 보상 지급됨");
            AddLog("광고 표시 성공");
            Debug.LogWarning("[AdMob] 광고 표시 실패");
        }
        else
        {
            UpdateStatus("광고 표시 실패!");
            AddLog("광고 표시 실패");
        }

        await Task.Delay(3000);
        UpdateStatus($"준비 완료 - AdMob Ready: {AdMobService.IsRewardedReady}");
    }

    private void UpdateRewardText()
    {
        if (rewardText != null)
        {
            rewardText.text = $"Reward : {rewardCount}";
            Debug.Log($"[UI] Text updated to: {rewardCount}");
        }
    }

    private void UpdateStatus(string status)
    {
        currentStatus = status;
        needsUIUpdate = true;
        Debug.Log($"[Status] {status}");
    }

    private void AddLog(string message)
    {
        string timeStamp = DateTime.Now.ToString("HH:mm:ss");
        logMessages += $"[{timeStamp}] {message}\n";

        // 로그가 너무 길어지면 처음 부분 제거
        string[] lines = logMessages.Split('\n');
        if (lines.Length > 10)
        {
            logMessages = string.Join("\n", lines, lines.Length - 10, 10);
        }

        needsUIUpdate = true;
        Debug.Log($"[Log] {message}");
    }

    private void UpdateStatusUI()
    {
        if (statusText != null)
        {
            statusText.text = $"상태: {currentStatus}";
        }

        if (logText != null)
        {
            logText.text = logMessages;
        }
    }
}