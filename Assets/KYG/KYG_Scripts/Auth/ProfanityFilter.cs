// 비속어/금칙어 필터 유틸 (TextAsset/Resources/StreamingAssets 모두 지원)

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;
using System.Collections;
using UnityEngine.Networking;

public static class ProfanityFilter
{
    // (기본) 내장 금칙어 — 외부 파일을 지정하지 않으면 이 목록 사용
    private static readonly string[] DefaultBanned = new[]
    {
        "fuck","shit","bitch","asshole",
        "개새","씨발","씨빨","좆","병신","썅","니미"
    };

    private static HashSet<string> _banned;
    private static bool _initialized;

    // “가시문자”만 남기는 정규식: 한글/영문/숫자 외 기호는 제거하여 우회 방지
    private static readonly Regex RxKeepWordChars =
        new Regex(@"[^\p{IsHangul}\p{IsBasicLatin}0-9A-Za-z]", RegexOptions.Compiled);

    /// <summary>
    /// (가장 간단) Inspector에서 넘겨준 TextAsset(줄바꿈 구분)로 초기화
    /// </summary>
    public static void Configure(TextAsset external)
    {
        if (external == null)
        {
            // 외부 파일이 없다면 기본 리스트로 초기화
            ConfigureFromLines(DefaultBanned);
            return;
        }

        var lines = SplitLines(external.text);
        ConfigureFromLines(lines);
    }

    /// <summary>
    /// Resources/profanity_ko.txt 처럼 리소스 경로로 로드
    /// </summary>
    public static void ConfigureFromResources(string resourcePath)
    {
        var ta = Resources.Load<TextAsset>(resourcePath);
        if (ta == null)
        {
            Debug.LogWarning($"[ProfanityFilter] Resources.Load 실패: {resourcePath}. 기본 리스트를 사용합니다.");
            ConfigureFromLines(DefaultBanned);
            return;
        }
        Configure(ta);
    }

    /// <summary>
    /// StreamingAssets 상대경로에서 txt를 로드하는 코루틴 (운영 교체/핫패치에 유리)
    /// 예) StartCoroutine(ProfanityFilter.ConfigureFromStreamingAssetsCoroutine("filters/profanity.txt"));
    /// </summary>
    public static IEnumerator ConfigureFromStreamingAssetsCoroutine(string relativePath)
    {
        string url = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android는 jar 내부 경로 접근 때문에 UnityWebRequest 사용
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (www.result != UnityWebRequest.Result.Success)
#else
            if (www.isNetworkError || www.isHttpError)
#endif
            {
                Debug.LogWarning($"[ProfanityFilter] StreamingAssets 로드 실패: {url}\n{www.error}\n기본 리스트를 사용합니다.");
                ConfigureFromLines(DefaultBanned);
                yield break;
            }
            var text = www.downloadHandler.text;
            ConfigureFromLines(SplitLines(text));
        }
#else
        // Editor/Windows/iOS 등은 파일 직접 접근 가능
        try
        {
            var text = System.IO.File.ReadAllText(url, Encoding.UTF8);
            ConfigureFromLines(SplitLines(text));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ProfanityFilter] StreamingAssets 로드 실패: {url}\n{e.Message}\n기본 리스트를 사용합니다.");
            ConfigureFromLines(DefaultBanned);
        }
        yield return null;
#endif
    }

    /// <summary>
    /// 외부에서 라인 컬렉션을 직접 전달해 초기화 (테스트/툴에서 유용)
    /// </summary>
    public static void ConfigureFromLines(IEnumerable<string> lines)
    {
        _banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 파일 파싱 규칙:
        // - 빈 줄/공백 줄 무시
        // - # 으로 시작하면 주석으로 무시
        // - 라인 끝의 인라인 주석(# 이후) 제거
        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            string line = raw.Trim();

            // 주석 라인
            if (line.StartsWith("#")) continue;

            // 인라인 주석 제거 (예: "씨발  # 주석" -> "씨발")
            int sharp = line.IndexOf('#');
            if (sharp >= 0) line = line.Substring(0, sharp).Trim();

            // 정규화해서 금칙어 표준형으로 저장
            var norm = Normalize(line);
            if (string.IsNullOrEmpty(norm)) continue;

            _banned.Add(norm);
        }

        // 외부 파일이 너무 비어있다면 기본값 보강 (선택)
        if (_banned.Count == 0)
        {
            foreach (var w in DefaultBanned)
                _banned.Add(Normalize(w));
        }

        _initialized = true;
        Debug.Log($"[ProfanityFilter] 초기화 완료. 금칙어 { _banned.Count }개 로드됨.");
    }

    /// <summary>현재 로드된 금칙어 수</summary>
    public static int Count => _banned?.Count ?? 0;

    /// <summary>닉네임에 금칙어가 포함되어 있으면 true (matched에 탐지된 금칙어 표준형 반환)</summary>
    public static bool ContainsBannedWord(string raw, out string matched)
    {
        matched = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        EnsureInit();

        var s = Normalize(raw);

        // 부분 포함 검사 (우회 방지 목적)
        foreach (var w in _banned)
        {
            if (w.Length == 0) continue;
            if (s.Contains(w))
            {
                matched = w;
                return true;
            }
        }
        return false;
    }

    // ---------- 내부 유틸 ----------

    private static void EnsureInit()
    {
        if (_initialized) return;
        // 외부에서 아무 것도 안불렀다면 기본 리스트로 초기화
        ConfigureFromLines(DefaultBanned);
    }

    private static string Normalize(string s)
    {
        // 1) 유니코드 정규화(NFKC) – 유사문자/전각/반각 통합
        var nfkc = s.Normalize(NormalizationForm.FormKC);
        // 2) 한글/영문/숫자만 남기고 기호/공백 제거 – “s h i t”, “sh!t” → “shit”
        nfkc = RxKeepWordChars.Replace(nfkc, "");
        // 3) 소문자화 + 트림
        return nfkc.ToLowerInvariant().Trim();
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
    }
}
