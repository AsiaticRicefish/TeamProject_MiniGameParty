// 비속어/금칙어 필터 유틸 (TextAsset/Resources/StreamingAssets 모두 지원)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace KYG
{
    /// <summary>
    /// 닉네임에서 비속어(금칙어)를 단순 "부분 포함"으로 검출하는 경량 필터.
    /// - 외부 txt(줄바꿈 구분) 또는 기본 내장 리스트 사용
    /// - 입력/사전 모두 NFKC 정규화 + 소문자 통일 + 기호 제거 후 비교
    /// - 정규식의 \p{IsHangul} 같은 블록 이름은 사용하지 않음(호환성 문제 방지)
    /// </summary>
    public static class ProfanityFilter
    {
        // (기본) 내장 금칙어 — 외부 파일을 지정하지 않으면 이 목록 사용
        private static readonly string[] DefaultBanned =
        {
            "fuck","shit","bitch","asshole",
            "개새","씨발","씨빨","좆","병신","썅","니미"
        };

        // 금칙어 사전 — 대소문자 무시 비교용
        private static HashSet<string> _banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 한 번 이상 초기화 되었는지 여부
        private static bool _initialized;

        // ─────────────────────────────────────────────────────────────
        // 허용 "가시문자"만 남기는 정규식 (기호/공백 제거 → 우회 방지)
        //  * \p{IsHangul} 미사용 → 유니코드 범위를 직접 나열
        //  * 한글(가–힣, 자모/확장) + 영문/숫자만 남기고 나머지는 제거
        // ─────────────────────────────────────────────────────────────
        private static readonly Regex RxKeepWordChars = new Regex(
            @"[^\uAC00-\uD7A3\u1100-\u11FF\u3130-\u318F\uA960-\uA97F\uD7B0-\uD7FFA-Za-z0-9]",
            RegexOptions.Compiled
        );

        /// <summary>
        /// (가장 간단) Inspector에서 넘겨준 TextAsset(줄바꿈 구분)로 초기화
        /// </summary>
        public static void Configure(TextAsset external)
        {
            if (external == null)
            {
                ConfigureFromLines(DefaultBanned);
                return;
            }

            var lines = SplitLines(external.text);
            ConfigureFromLines(lines);
        }

        /// <summary>
        /// Resources/profanity_ko 처럼 리소스 경로로 로드 (확장자 제외)
        /// </summary>
        public static void ConfigureFromResources(string resourcePath)
        {
            var ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null)
            {
                Debug.LogWarning($"[ProfanityFilter] Resources.Load 실패: {resourcePath}. 기본 리스트 사용.");
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
            // Android는 jar 내부 경로 접근 때문에 UnityWebRequest 필요
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (www.result != UnityWebRequest.Result.Success)
#else
                if (www.isNetworkError || www.isHttpError)
#endif
                {
                    Debug.LogWarning($"[ProfanityFilter] StreamingAssets 로드 실패: {url}\n{www.error}\n기본 리스트 사용.");
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
                Debug.LogWarning($"[ProfanityFilter] StreamingAssets 로드 실패: {url}\n{e.Message}\n기본 리스트 사용.");
                ConfigureFromLines(DefaultBanned);
            }
            yield return null;
#endif
        }

        /// <summary>
        /// 외부에서 라인 컬렉션을 직접 전달해 초기화 (툴/테스트에 유용)
        /// - 빈 줄/공백 줄 무시
        /// - '#' 시작 줄은 주석으로 무시
        /// - 인라인 주석(라인 내 '#' 이후) 제거
        /// - NFKC 정규화 + 기호 제거 + 소문자화로 표준화 저장
        /// </summary>
        public static void ConfigureFromLines(IEnumerable<string> lines)
        {
            _banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string line = raw.Trim();

                // 전체 주석 라인
                if (line.StartsWith("#")) continue;

                // 인라인 주석 제거 (예: "씨발  # 주석" -> "씨발")
                int sharp = line.IndexOf('#');
                if (sharp >= 0) line = line.Substring(0, sharp).Trim();

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
            Debug.Log($"[ProfanityFilter] 초기화 완료. 금칙어 {_banned.Count}개 로드됨.");
        }

        /// <summary>현재 로드된 금칙어 수</summary>
        public static int Count => _banned?.Count ?? 0;

        /// <summary>
        /// 닉네임에 금칙어가 포함되어 있으면 true (matched에 탐지된 금칙어 표준형 반환)
        /// - 비교는 "부분 포함" 기준 (예: sh!t → shit → 매치)
        /// </summary>
        public static bool ContainsBannedWord(string raw, out string matched)
        {
            matched = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            EnsureInit();

            var s = Normalize(raw);

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

        // ─────────────────────────────────────────────────────────────
        // 내부 유틸
        // ─────────────────────────────────────────────────────────────

        private static void EnsureInit()
        {
            if (_initialized) return;
            ConfigureFromLines(DefaultBanned);
        }

        /// <summary>
        /// 비교용 표준화:
        /// 1) 유니코드 NFKC 정규화(전각/반각/호환문자 통합)
        /// 2) 한글/자모/영문/숫자만 남기고 기호/공백 제거(우회 방지)
        /// 3) 소문자 통일 + Trim
        /// </summary>
        private static string Normalize(string s)
        {
            var nfkc = s.Normalize(NormalizationForm.FormKC);
            nfkc = RxKeepWordChars.Replace(nfkc, ""); // 허용 외 문자 제거
            return nfkc.ToLowerInvariant().Trim();
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }
    }
}
