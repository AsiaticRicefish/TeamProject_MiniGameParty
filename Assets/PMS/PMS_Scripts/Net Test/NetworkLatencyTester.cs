using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;
using System;

[RequireComponent(typeof(PhotonView))]
public class NetworkLatencyTester : MonoBehaviourPunCallbacks
{
    private List<double> latencySamples = new List<double>();
    private const int TEST_SAMPLES = 100; // 50번으로 증가

    private void Start()
    {
        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(2f);

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError("방에 접속되지 않았습니다!");
            yield break;
        }

        Debug.Log("=== 레이턴시 테스트 시작 ===");
        Debug.Log($"현재 Photon Ping: {PhotonNetwork.GetPing()}ms");
        StartLatencyTest();
    }

    public void StartLatencyTest()
    {
        latencySamples.Clear();
        StartCoroutine(RunLatencyTest());
    }

    private IEnumerator RunLatencyTest()
    {
        for (int i = 0; i < TEST_SAMPLES; i++)
        {
            photonView.RPC("MeasureLatency", RpcTarget.All, PhotonNetwork.Time);
            yield return new WaitForSeconds(0.3f); // 간격 단축
        }

        yield return new WaitForSeconds(1f);
        AnalyzeResults();
    }

    [PunRPC]
    void MeasureLatency(double sendTime)
    {
        double delay = PhotonNetwork.Time - sendTime;

        // 자기 자신(0ms)은 제외!
        if (delay > 0.001) // 1ms 이상만 수집
        {
            latencySamples.Add(delay);
        }
    }

    void AnalyzeResults()
    {
        if (latencySamples.Count == 0)
        {
            Debug.LogError("측정된 샘플이 없습니다!");
            return;
        }

        latencySamples.Sort();

        double avg = latencySamples.Average();
        double min = latencySamples.Min();
        double max = latencySamples.Max();
        double median = latencySamples[latencySamples.Count / 2];

        // 퍼센타일 계산
        int p75Index = (int)(latencySamples.Count * 0.75);
        int p90Index = (int)(latencySamples.Count * 0.90);
        int p95Index = (int)(latencySamples.Count * 0.95);
        int p99Index = (int)(latencySamples.Count * 0.99);

        double p75 = latencySamples[p75Index];
        double p90 = latencySamples[p90Index];
        double p95 = latencySamples[p95Index];
        double p99 = latencySamples[p99Index];

        double variance = latencySamples.Select(x => Math.Pow(x - avg, 2)).Average();
        double stdDev = Math.Sqrt(variance);

        // 추천 lead 계산 (95 퍼센타일 * 1.1)
        double recommendedLead = p95 * 1.1;

        Debug.Log($"<color=yellow>================ Latency Test Report ================</color>");
        Debug.Log($"<b>Client:</b> {PhotonNetwork.LocalPlayer.NickName}");
        Debug.Log($"<b>Valid Samples:</b> {latencySamples.Count} (0ms samples excluded)");
        Debug.Log($"<b>Photon Ping:</b> {PhotonNetwork.GetPing()} ms");
        Debug.Log("");

        Debug.Log($"<b>Average:</b> {avg * 1000:F2} ms");
        Debug.Log($"<b>Median:</b> {median * 1000:F2} ms");
        Debug.Log($"<b>Min:</b> {min * 1000:F2} ms");
        Debug.Log($"<b>Max:</b> {max * 1000:F2} ms");
        Debug.Log("");

        Debug.Log($"<b>75th Percentile (P75):</b> {p75 * 1000:F2} ms");
        Debug.Log($"<b>90th Percentile (P90):</b> {p90 * 1000:F2} ms");
        Debug.Log($"<b>95th Percentile (P95):</b> {p95 * 1000:F2} ms");
        Debug.Log($"<b>99th Percentile (P99):</b> {p99 * 1000:F2} ms");
        Debug.Log($"<b>Standard Deviation:</b> {stdDev * 1000:F2} ms");
        Debug.Log($"<color=yellow>========================================================</color>");

        // 권장사항
        Debug.Log($"<color=yellow> Recommended Lead Time: {recommendedLead * 1000:F0} ms ({recommendedLead:F3}s) </color>");
        Debug.Log($"");

        if (recommendedLead < 0.15)
        {
            Debug.Log("✅ 네트워크 상태: 매우 좋음 (lead 0.15초면 충분)");
        }
        else if (recommendedLead < 0.25)
        {
            Debug.Log("⚠️ 네트워크 상태: 보통 (lead 0.25초 권장)");
        }
        else if (recommendedLead < 0.35)
        {
            Debug.Log("⚠️ 네트워크 상태: 나쁨 (lead 0.35초 권장)");
        }
        else
        {
            Debug.Log("❌ 네트워크 상태: 매우 나쁨 (lead 0.4초 이상 필요)");
        }

        // 표준편차 경고
        if (stdDev > 0.05)
        {
            Debug.Log($"⚠️ 주의: 네트워크가 불안정합니다 (표준편차 {stdDev * 1000:F1}ms)");
            Debug.Log($"   → 여유있는 lead 값 사용 권장");
        }

        Debug.Log($"===========================");
    }
}