using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TimingGame : MonoBehaviour
{
    //UI
    [SerializeField] TMP_Text _timeText; //타이머 텍스트
    [SerializeField] Slider _slider; // 슬라이더
    [SerializeField] RectTransform _successZone; // 성공 영역
    [SerializeField] RectTransform _sliderRect;
    [SerializeField] GameObject _sliderGo; // 타이밍 미니게임 Wrap오브젝트
    [SerializeField] GameObject _finishPanel; //성공, 실패 여부 패널
    [SerializeField] TMP_Text _finishText;

    [Header("난이도 UI")]
    [SerializeField] TMP_Text _speedLevelText;
    [SerializeField] TMP_Text _zoneLevelText;

    [Header("난이도 레벨별 색상 (0~4단계)")]
    [SerializeField]
    private Color[] levelColors = new Color[4]
{
    Color.black,                // 레벨 0: 검정
    Color.green,                // 레벨 1: 초록
    Color.yellow,               // 레벨 2: 노랑
    new Color(1f, 0.5f, 0f)    // 레벨 3: 주황  
};

    [Header("깜빡임 설정")]
    [SerializeField] private Color blinkColor = Color.cyan; // 깜빡일 때 색상
    [SerializeField] private int blinkCount = 3;
    [SerializeField] private float blinkSpeed = 0.2f;

    private int _currentSpeedLevel = 0;
    private int _currentZoneLevel = 0;
    private int _prevSpeedLevel = 0;
    private int _prevZoneLevel = 0;

    [Header("타이밍 세팅")]
    [SerializeField] float _limitTime = 5f; // 제한시간
    [SerializeField] float _speed = 1.6f; // 별 이동 속도

    [Header("성공 존")]
    [SerializeField]
    float _zoneWRate = 0.45f;                         // 성공존 가로폭 (슬라이더 폭 비율)
    [SerializeField]
    float _zoneHeight = 40f;                          // 성공존 세로 높이(px)

    bool _isRun = false; // 게임 실행 여부
    float _remainTime; // 잔여 시간
    float _pingPongTimer;
    float _zoneW;
    public event Action<bool, float> OnFinished; // 성공여부 이벤트

    [Header("판정 설정")]
    [SerializeField, Range(0f, 1f)]
    private float requiredOverlapRatio = 0.5f; // 성공 판정에 필요한 겹침 비율 (0.5 = 50%)


    void Awake() => Init();

    void Update()
    {
        if (!_isRun) return;

        //스타 이동
        _pingPongTimer += Time.deltaTime;

        float x = Mathf.PingPong(_pingPongTimer / _speed, 1f);
        _slider.value = Mathf.Lerp(0f, 100f, x);

        bool tapped = false;

        tapped = (Mouse.current?.leftButton.wasPressedThisFrame == true)
              || (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true);

        if (tapped) 
        {
            var (ok, acc) = Calculate(); 
            GameEnd(ok, ok ? acc : 0f); 
        }
    }

    private void Init()
    {
        //슬라이더 초기화
        _slider.minValue = 0f;
        _slider.maxValue = 100f;
        _slider.value = 0f;

        //텍스트 초기화
        _timeText.text = "";

        //성공 존 초기화
        _successZone.gameObject.SetActive(false);
    }

    /// <summary>
    /// 미니 게임 시작
    /// </summary>
    public void GameStart()
    {
        gameObject.SetActive(true);

        if (_isRun) return;

        // 타이밍 게임 시작 사운드
        SoundManager.Instance.PlaySFX("Timing");

        _isRun = true;

        // 성공 존 설정
        _sliderGo.SetActive(true);
        SetSuccesZone();

        // 초기화
        _slider.value = 0f;
        _pingPongTimer = 0f;

        _remainTime = _limitTime;
        _timeText.text = Mathf.CeilToInt(_remainTime).ToString();

        UpdateDifficultyUI(); // UI 갱신
    }

    /// <summary>
    /// 성공존 랜덤 범위 설정
    /// </summary>
    void SetSuccesZone()
    {
        if (!_successZone) return;

        // 성공존을 Handle Slide Area 아래로 강제(좌표계 통일)
        var slideArea = _slider.handleRect ? _slider.handleRect.parent as RectTransform : null;
        if (slideArea && _successZone.parent != slideArea)
        {
            _successZone.SetParent(slideArea, worldPositionStays: false);
        }

        // 앵커를 중앙으로 강제 설정
        _successZone.anchorMin = new Vector2(0.5f, 0.5f);
        _successZone.anchorMax = new Vector2(0.5f, 0.5f);
        _successZone.pivot = new Vector2(0.5f, 0.5f);

        float w = slideArea ? slideArea.rect.width : _sliderRect.rect.width;
        _zoneW = w * _zoneWRate;

        // 가로폭은 비율, 세로 높이는 인스펙터 값 사용
        _successZone.sizeDelta = new(_zoneW, _zoneHeight);

        _successZone.anchoredPosition = Vector2.zero;

        // 그리기 순서: 성공존 뒤, 핸들 앞
        _successZone.SetSiblingIndex(0);                 // 성공존을 맨 뒤로
        _slider.handleRect.SetAsLastSibling();           // 핸들을 맨 앞으로

        _successZone.gameObject.SetActive(true);

        if (slideArea)
        {
            var sliderCorners = new Vector3[4];
            slideArea.GetWorldCorners(sliderCorners);
            Debug.Log($"슬라이더 전체 범위: {sliderCorners[0].x:F2} ~ {sliderCorners[2].x:F2}");
        }

        var corners = new Vector3[4];
        _successZone.GetWorldCorners(corners);
        Debug.Log($"성공존 실제 범위: {corners[0].x:F2} ~ {corners[2].x:F2}");
    }

    public IEnumerator IE_CountDownPublic() => IE_CountDown(); // JengaTimingManager에서 코루틴이 접근하도록 수정

    /// <summary>
    /// 카운트 다운 진행
    /// </summary>
    /// <returns>매 프레임 마다</returns>
    IEnumerator IE_CountDown()
    {
        while (_isRun && _remainTime > 0f)
        {
            _remainTime -= Time.deltaTime;
            _timeText.text = Mathf.CeilToInt(Mathf.Max(0f, _remainTime)).ToString();
            yield return null;
        }
        //시간 초과 시
        if (_isRun) GameEnd(false, 0f);
    }


    (bool isSuccess, float accuracy) Calculate()
    {
        var handleGraphic = _slider.handleRect.GetComponentInChildren<Image>()?.rectTransform
                         ?? _slider.handleRect;

        // 핸들의 월드 코너
        var handleCorners = new Vector3[4];
        handleGraphic.GetWorldCorners(handleCorners);
        float handleLeft = handleCorners[0].x;
        float handleRight = handleCorners[2].x;
        float handleCenter = 0.5f * (handleLeft + handleRight);

        var zoneCorners = new Vector3[4];
        _successZone.GetWorldCorners(zoneCorners);
        float zoneLeft = zoneCorners[0].x;
        float zoneRight = zoneCorners[2].x;
        float zoneCenter = 0.5f * (zoneLeft + zoneRight);
        float zoneHalfWidth = 0.5f * (zoneRight - zoneLeft);

        // 겹치는 영역 계산
        float overlapLeft = Mathf.Max(handleLeft, zoneLeft);
        float overlapRight = Mathf.Min(handleRight, zoneRight);
        float overlapWidth = Mathf.Max(0f, overlapRight - overlapLeft);

        float handleWidth = handleRight - handleLeft;
        float overlapRatio = handleWidth > 0 ? (overlapWidth / handleWidth) : 0f;

        // Inspector에서 설정한 비율 이상 겹쳐야 성공
        if (overlapRatio < requiredOverlapRatio) return (false, 0f);

        // 정확도 계산
        float centerDistance = Mathf.Abs(handleCenter - zoneCenter);
        float acc = 1f - Mathf.Clamp01(centerDistance / zoneHalfWidth);

        return (true, acc);

        #region Removed Code
        //var z = new Vector3[4];
        //_successZone.GetWorldCorners(z);
        //float start = z[0].x;   // left
        //float end   = z[3].x;   // right
        //float center = 0.5f * (start + end);
        //float half   = 0.5f * (end - start);

        //bool inside = (start <= handleCenterX) && (handleCenterX <= end);
        //if (!inside) return (false, 0f);

        //float acc = 1f - Mathf.Clamp01(Mathf.Abs(handleCenterX - center) / half);

        //return (true, acc);
        #endregion
    }

    /// <summary>
    /// 타이밍 미니 게임 종료
    /// </summary>
    /// <param name="isSuccess">성공 여부</param>
    /// <param name="accuracy">정확도 </param>
    void GameEnd(bool isSuccess, float accuracy)
    {
        if (!_isRun) return;
        _isRun = false;

        _sliderGo.SetActive(false);
        StartCoroutine(IE_PanelCount(isSuccess, accuracy));
    }

    /// <summary>
    /// 성공 혹은 실패 여부를 띄우는 UI
    /// </summary>
    /// <param name="isSuccess">타이밍 미니게임 성공 여부</param>
    /// <returns></returns>
    IEnumerator IE_PanelCount(bool isSuccess, float accuracy)
    {
        _finishPanel.SetActive(true);
        if (isSuccess)
        {
            _finishText.text = "성공!";
            // 성공 SFX 실행
            SoundManager.Instance.PlaySFX("Success");
        }
        else
        {
            _finishText.text = "실패...";
            // 실패 SFX 실행
            SoundManager.Instance.PlaySFX("Fail");
        }

        yield return new WaitForSeconds(1f);

        _finishPanel.SetActive(false);
        _finishText.text = "";

        // UI 정리
        _finishPanel.SetActive(false);
        _finishText.text = "";

        gameObject.SetActive(false);

        OnFinished?.Invoke(isSuccess, accuracy);
    }


    public void DifficultyChange(int level)
    {
        //랜덤으로 아래 중 하나 고르기
        bool type = UnityEngine.Random.Range(0, 2) == 0;
        bool isChnaged = type ? DescSpeed(level) || DescZone(level) : DescZone(level) || DescSpeed(level);

        UpdateDifficultyUI();
    }


    /// <summary>
    /// 왕복 속도 감소 로직
    /// 입력받은 레벨에 맞는 속도 지정
    /// </summary>
    /// <param name="level">난이도 레벨, 높을 수록 왕복 속도가 더 빨라짐.</param>
    /// <returns></returns>
    bool DescSpeed(int level)
    {
        float before = _speed;
        float temp = before - level * 0.25f;
        float after = Mathf.Clamp(temp, 0.6f, 1.6f);

        if (after < before)
        {
            _speed = after;
            _currentSpeedLevel = level; // 현재 속도 레벨 저장
            return true;
        }
        return false;
    }

    /// <summary>
    /// 판정 영역 감소 로직
    /// 입력받은 레벨에 맞는 판정역역 지정
    /// </summary>
    /// <param name="level">난이도 레벨, 높을 수록 판정 영역이 더 좁아짐.</param>
    /// <returns></returns>
    bool DescZone(int level)
    {
        float before = _zoneWRate;
        float temp = before - level * 0.05f;
        float after = Mathf.Clamp(temp, 0.25f, 0.45f);

        if (after < before)
        {
            _zoneWRate = after;
            _currentZoneLevel = level; // 현재 영역 레벨 저장
            return true;
        }
        return false;
    }

    #region 난이도 증가 UI
    private void UpdateDifficultyUI()
    {
        // 속도 UI 업데이트
        if (_speedLevelText != null)
        {
            string speedLabel = GetLevelLabel(_currentSpeedLevel);
            _speedLevelText.text = $"속도: {speedLabel}";

            Color baseColor = GetLevelColor(_currentSpeedLevel);
            _speedLevelText.color = baseColor;

            // 레벨이 증가했으면 깜빡임
            if (_currentSpeedLevel > _prevSpeedLevel)
            {
                StartCoroutine(BlinkText(_speedLevelText, baseColor));
                _prevSpeedLevel = _currentSpeedLevel;
            }
        }

        // 성공존 UI 업데이트
        if (_zoneLevelText != null)
        {
            string zoneLabel = GetLevelLabel(_currentZoneLevel);
            _zoneLevelText.text = $"성공존: {zoneLabel}";

            Color baseColor = GetLevelColor(_currentZoneLevel);
            _zoneLevelText.color = baseColor;

            // 레벨이 증가했으면 깜빡임
            if (_currentZoneLevel > _prevZoneLevel)
            {
                StartCoroutine(BlinkText(_zoneLevelText, baseColor));
                _prevZoneLevel = _currentZoneLevel;
            }
        }
    }

    private string GetLevelLabel(int level)
    {
        switch (level)
        {
            case 0: return "기본";
            case 1: return "1단계";
            case 2: return "2단계";
            case 3: return "MAX";
            default: return "MAX";
        }
    }

    private Color GetLevelColor(int level)
    {
        if (level >= 0 && level < levelColors.Length)
            return levelColors[level];

        return levelColors[levelColors.Length - 1];
    }

    private IEnumerator BlinkText(TMP_Text text, Color originalColor)
    {
        for (int i = 0; i < blinkCount; i++)
        {
            text.color = blinkColor;
            yield return new WaitForSeconds(blinkSpeed);
            text.color = originalColor;
            yield return new WaitForSeconds(blinkSpeed);
        }
    }

    #endregion

    float GetWorldCenterX(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return 0.5f * (c[0].x + c[3].x);
    } 
}