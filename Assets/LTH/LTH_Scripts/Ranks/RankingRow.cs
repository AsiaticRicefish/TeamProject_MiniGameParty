using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if DOTWEEN
using DG.Tweening;
#endif


/// <summary>
/// 단일 랭킹 행 UI 컴포넌트
/// - 내부 참조는 private로 감추고, 외부에는 행위/읽기 전용만 노출
/// - DOTween 유무에 따라 트윈/코루틴 양쪽 지원
/// - 풀링/재사용을 위한 ResetView/PrepareForReuse 제공
/// </summary>
[DisallowMultipleComponent]

public class RankingRow : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image bg;
    [SerializeField] private CanvasGroup cg;

    [Header("Style")]
    [SerializeField] private Color normalColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    [SerializeField] private Color firstColor = new Color(0.98f, 0.82f, 0.25f, 1f);

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float punchScale = 0.15f;
    [SerializeField] private float punchDuration = 0.40f;

    private RectTransform _rt;
    private float _baseScale = 1f;
    private Coroutine _moveCo;
    private Coroutine _fxCo;

#if DOTWEEN
    private Tween _moveTween;
    private Tween _fadeTween;
    private Tween _punchTween;
#endif

    // 읽기 전용 프로퍼티 (외부 디버그/검증용)
    public string CurrentNickname => nameText ? nameText.text : string.Empty;
    public int CurrentRank
    {
        get
        {
            if (!rankText) return 0;
            return int.TryParse(rankText.text, out var r) ? r : 0;
        }
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;
        if (!cg) cg = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _baseScale = transform.localScale.x;
        // 첫 진입 기본 상태 정리
        ResetView();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 누락 방지: 캔버스 그룹 자동 부착
        if (!cg && gameObject.activeInHierarchy)
        {
            cg = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }
    }
#endif

    /// <summary>
    /// 텍스트/색상 세팅 (데이터 표시)
    /// </summary>
    public void SetContent(int rank, string nickname)
    {
        if (rankText) rankText.text = rank.ToString();
        if (nameText) nameText.text = nickname;
        if (bg) bg.color = (rank == 1) ? firstColor : normalColor;
    }

    /// <summary>
    /// 즉시 위치 셋 (레이아웃 그룹 없이 직접 배치하는 컨테이너 가정)
    /// </summary>
    public void SetPositionInstant(Vector2 anchoredPos)
    {
        KillMove();
        _rt.anchoredPosition = anchoredPos;
    }

    /// <summary>
    /// 지정 위치로 부드럽게 이동
    /// </summary>
    public void AnimateTo(Vector2 target, float duration)
    {
        KillMove();

#if DOTWEEN
        _moveTween = _rt.DOAnchorPos(target, duration);
#else
        _moveCo = StartCoroutine(LerpPos(_rt.anchoredPosition, target, duration));
#endif
    }

    /// <summary>
    /// 1등 강조(페이드 인 + 펀치 스케일)
    /// </summary>
    public void EmphasizeFirstPlace()
    {
        KillFx();

#if DOTWEEN
        cg.alpha = 0f;
        _fadeTween  = cg.DOFade(1f, Mathf.Max(0.01f, fadeInDuration));
        transform.localScale = Vector3.one * _baseScale;
        _punchTween = transform.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 10, elasticity: 0.9f);
#else
        _fxCo = StartCoroutine(BlinkAndPunch());
#endif
    }

    /// <summary>풀링/재사용 대비: 비활성/리셋 시 호출 권장</summary>
    public void PrepareForReuse()
    {
        KillAllTweens();
        // 값 초기화
        if (rankText) rankText.text = string.Empty;
        if (nameText) nameText.text = string.Empty;
        if (bg) bg.color = normalColor;
        ResetView();
    }

    /// <summary>
    /// 기본 비주얼 상태로 되돌림(알파/스케일/회전 등)
    /// </summary>
    public void ResetView()
    {
        if (cg) cg.alpha = 1f;
        transform.localScale = Vector3.one * _baseScale;
    }

    private void OnDisable()
    {
        // 비활성화 시 코루틴/트윈 정리 (풀링 시 안전)
        KillAllTweens();
    }

    // ==== 내부 유틸 ====

    private void KillMove()
    {
#if DOTWEEN
        _moveTween?.Kill();
        _moveTween = null;
#else
        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = null;
#endif
    }

    private void KillFx()
    {
#if DOTWEEN
        _fadeTween?.Kill();
        _punchTween?.Kill();
        _fadeTween = _punchTween = null;
#else
        if (_fxCo != null) StopCoroutine(_fxCo);
        _fxCo = null;
#endif
    }

    private void KillAllTweens()
    {
        KillMove();
        KillFx();
    }

#if !DOTWEEN
    private IEnumerator LerpPos(Vector2 from, Vector2 to, float t)
    {
        if (t <= 0f)
        {
            _rt.anchoredPosition = to;
            yield break;
        }

        float e = 0f;
        while (e < t)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, e / t);
            _rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }
        _rt.anchoredPosition = to;
        _moveCo = null;
    }

    private IEnumerator BlinkAndPunch()
    {
        // 페이드 인
        cg.alpha = 0f;
        float t = 0f;
        float fade = Mathf.Max(0.01f, fadeInDuration);
        while (t < fade)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / fade);
            yield return null;
        }
        cg.alpha = 1f;

        // 펀치 스케일
        float time = Mathf.Max(0.01f, punchDuration);
        float amp = punchScale;
        Vector3 start = Vector3.one * _baseScale;

        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            // 0→π 한 사이클로 튕기는 느낌
            float s = 1f + Mathf.Sin((elapsed / time) * Mathf.PI) * amp;
            transform.localScale = start * s;
            yield return null;
        }
        transform.localScale = start;
        _fxCo = null;
    }
#endif

}
