using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimingGame : MonoBehaviour
{
    //UI
    [SerializeField] TMP_Text _timeText; //타이머 텍스트
    [SerializeField] Slider _slider; // 슬라이더
    [SerializeField] RectTransform _successZone; // 성공 영역
    [SerializeField] RectTransform _sliderRect;

    //설정
    float _limitTime = 1000f; //제한시간
    float _speed; //별 이동 속도
    [SerializeField] Vector2 speedRange = new(0.4f, 0.8f); //별 이동 속도 범위
    [SerializeField] Vector2 _succesZoneSize = new(100f, 200f); //성공 영역 사이즈

    public event Action<bool, float> OnFinished; //성공여부 이벤트

    bool _isRun = false; //게임 실행 여부
    float _remainTime; // 잔여 시간
    float _pingPongTimer;

    void Awake() =>
        Init();

    void Update()
    {
        if (!_isRun) return;

        //스타 이동
        _pingPongTimer += Time.deltaTime;

        float x = Mathf.PingPong(_pingPongTimer / _speed, 1f);
        _slider.value = Mathf.Lerp(0f, 100f, x);

        //TODO 김승태 : 화면 터치 관련 로직(임시)
        if (Input.GetMouseButtonDown(0))
        {
            //사운드 삽입하기.
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
        if (_isRun) return;

        _isRun = true;

        //사운드 삽입하기

        SetSuccesZone();

        //초기화
        _slider.value = 0f;
        _pingPongTimer = 0f;
        _speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);

        _remainTime = _limitTime;
        gameObject.SetActive(true);

        _timeText.text = Mathf.CeilToInt(_remainTime).ToString();
        StartCoroutine(IE_CountDown());
    }

    /// <summary>
    /// 성공존 랜덤 범위 설정
    /// </summary>
    void SetSuccesZone()
    {
        if (!_successZone) return;

        float sliderWidth = _sliderRect.rect.width;

        //성공 존의 랜덤 폭 선택
        float zoneWidth = Mathf.Clamp(
            UnityEngine.Random.Range(_succesZoneSize.x, _succesZoneSize.y),
            _succesZoneSize.x, sliderWidth);

        //성공 존 시작 x 좌표 설정
        float start = UnityEngine.Random.Range(0f, sliderWidth - zoneWidth);

        //성공존 가로 폭 설정
        _successZone.sizeDelta = new(zoneWidth, _successZone.sizeDelta.y);

        //성공 존 좌측 모서리를 start존 위치에 맞게 설정
        _successZone.anchoredPosition = new(start, 0f);

        _successZone.gameObject.SetActive(true);
    }

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

    /// <summary>
    /// 슬라이더 위치 값에 따른 성공 존 내부 여부와 정확도 계산 로직
    /// </summary>
    /// <returns>성공 여부 및 정확도 반환.</returns>
    (bool isSuccess, float accuracy) Calculate()
    {
        //슬라이더 폭(px)을 구하기
        float width = _sliderRect.rect.width;
        
        //슬라이더 핸들 위치를 픽셀 값으로 매핑
        float nowPos = Mathf.Lerp(0f, width, _slider.value / 100f);

        //성공 존 시작, 끝 지점
        float start = _successZone.anchoredPosition.x;
        float end = start + _successZone.rect.width;

        bool isInside = (start <= nowPos) && (nowPos <= end);

        if (!isInside)
        {
            //게임 종료(실패 처리)
            return (false, 0f);
        }

        //정확도 판정하기
        float center = (start + end) * 0.5f;
        float half = (end - start) * 0.5f;
        float acc = 1f - Mathf.Clamp01(Mathf.Abs(nowPos - center) / half);

        return (true, acc);
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


        //이벤트 퍼블리싱
        OnFinished?.Invoke(isSuccess, accuracy);

        Debug.Log($"성공 여부 : {isSuccess}, 정확도 : {accuracy}");

        //비활성화(테스트를 위해 잠시 비활성화)
        // gameObject.SetActive(false);
    }


}