using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimingGame : MonoBehaviour
{
    //UI
    [SerializeField] TMP_Text _timeText; //Ÿ�̸� �ؽ�Ʈ
    [SerializeField] Slider _slider; // �����̴�
    [SerializeField] RectTransform _successZone; // ���� ����
    [SerializeField] RectTransform _sliderRect;
    [SerializeField] GameObject _sliderGo; // Ÿ�̹� �̴ϰ��� Wrap������Ʈ
    [SerializeField] GameObject _finishPanel; //����, ���� ���� �г�
    [SerializeField] TMP_Text _finishText;


    //����
    [SerializeField] float _limitTime = 5f; //���ѽð� -> 5�ʷ� �����ϱ�
    [SerializeField] float _speed = 1.6f; //�� �̵� �ӵ�
    [SerializeField] float _zoneW; //���� �� �ʺ�
    [SerializeField] float _zoneWRate = 0.45f; //���� �� �ʺ� ����
    bool _isRun = false; //���� ���� ����
    float _remainTime; // �ܿ� �ð�
    float _pingPongTimer;
    public event Action<bool, float> OnFinished; //�������� �̺�Ʈ

    void Awake() =>
        Init();

    void Update()
    {
        if (!_isRun) return;

        //��Ÿ �̵�
        _pingPongTimer += Time.deltaTime;

        float x = Mathf.PingPong(_pingPongTimer / _speed, 1f);
        _slider.value = Mathf.Lerp(0f, 100f, x);

        //TODO ����� : ȭ�� ��ġ ���� ����(�ӽ�), ����� ���� ��ġ�� ���� �ʿ�.
        if (Input.GetMouseButtonDown(0))
        {
            //���� �����ϱ�.
            var (ok, acc) = Calculate();
            GameEnd(ok, ok ? acc : 0f);
        }
    }

    private void Init()
    {
        //�����̴� �ʱ�ȭ
        _slider.minValue = 0f;
        _slider.maxValue = 100f;
        _slider.value = 0f;

        //�����̴� �ڵ� ũ��(�� ũ��) ����
        RectTransform handle = _slider.handleRect;

        float handleW = _sliderRect.rect.width * 0.15f;
        float handleH = _sliderRect.rect.height * 0.15f;

        handle.sizeDelta = new(handleW, handleH);

        //�ؽ�Ʈ �ʱ�ȭ
        _timeText.text = "";

        //���� �� �ʱ�ȭ
        _successZone.gameObject.SetActive(false);
    }

    /// <summary>
    /// �̴� ���� ����
    /// </summary>
    public void GameStart()
    {
        gameObject.SetActive(true);

        if (_isRun) return;

        _isRun = true;

        //TODO ����� : Ÿ�̹� ���� BGM ����

        //���� �� ����
        _sliderGo.SetActive(true);
        SetSuccesZone();

        //�ʱ�ȭ
        _slider.value = 0f;
        _pingPongTimer = 0f;
        _speed = 1.6f;
        _zoneWRate = 0.45f;

        _remainTime = _limitTime;
        gameObject.SetActive(true);

        _timeText.text = Mathf.CeilToInt(_remainTime).ToString();
        StartCoroutine(IE_CountDown());

        Debug.Log($"���� �ӵ� {_speed} ���� ���� {_zoneWRate}");
    }

    /// <summary>
    /// ������ ���� ���� ����
    /// </summary>
    void SetSuccesZone()
    {
        if (!_successZone) return;

        float sliderWidth = _sliderRect.rect.width;

        _zoneW = sliderWidth * _zoneWRate;

        _successZone.sizeDelta = new(_zoneW, _successZone.sizeDelta.y);
        _successZone.anchoredPosition = new((sliderWidth - _zoneW) / 2f, 0f);

        _successZone.gameObject.SetActive(true);
    }

    /// <summary>
    /// ī��Ʈ �ٿ� ����
    /// </summary>
    /// <returns>�� ������ ����</returns>
    IEnumerator IE_CountDown()
    {
        while (_isRun && _remainTime > 0f)
        {
            _remainTime -= Time.deltaTime;
            _timeText.text = Mathf.CeilToInt(Mathf.Max(0f, _remainTime)).ToString();
            yield return null;
        }
        //�ð� �ʰ� ��
        if (_isRun) GameEnd(false, 0f);
    }

    /// <summary>
    /// �����̴� ��ġ ���� ���� ���� �� ���� ���ο� ��Ȯ�� ��� ����
    /// </summary>
    /// <returns>���� ���� �� ��Ȯ�� ��ȯ.</returns>
    (bool isSuccess, float accuracy) Calculate()
    {
        //�����̴� ��(px)�� ���ϱ�
        float width = _sliderRect.rect.width;

        //�����̴� �ڵ� ��ġ�� �ȼ� ������ ����
        float nowPos = Mathf.Lerp(0f, width, _slider.value / 100f);

        //���� �� ����, �� ����
        float start = _successZone.anchoredPosition.x;
        float end = start + _successZone.rect.width;

        bool isInside = (start <= nowPos) && (nowPos <= end);

        if (!isInside)
            //���� ����(���� ó��)
            return (false, 0f);

        //��Ȯ�� �����ϱ�
        float center = (start + end) * 0.5f;
        float half = (end - start) * 0.5f;
        float acc = 1f - Mathf.Clamp01(Mathf.Abs(nowPos - center) / half);

        return (true, acc);
    }

    /// <summary>
    /// Ÿ�̹� �̴� ���� ����
    /// </summary>
    /// <param name="isSuccess">���� ����</param>
    /// <param name="accuracy">��Ȯ�� </param>
    void GameEnd(bool isSuccess, float accuracy)
    {
        if (!_isRun) return;
        _isRun = false;

        Debug.Log($"���� ���� : {isSuccess}, ��Ȯ�� : {accuracy}");

        _sliderGo.SetActive(false);
        StartCoroutine(IE_PanelCount(isSuccess));

        //�̺�Ʈ �ۺ���
        OnFinished?.Invoke(isSuccess, accuracy);
    }

    /// <summary>
    /// ���� Ȥ�� ���� ���θ� ���� UI
    /// </summary>
    /// <param name="isSuccess">Ÿ�̹� �̴ϰ��� ���� ����</param>
    /// <returns></returns>
    IEnumerator IE_PanelCount(bool isSuccess)
    {
        _finishPanel.SetActive(true);
        if (isSuccess)
        {
            _finishText.text = "Success!";
            //TODO ����� : ���� SFX ����
        }
        else
        {
            _finishText.text = "Fail!";
            //TODO ����� : ���� SFX ����
            //TODO ����� : ���� ���� ���� �ִϸ��̼� �߰�.
        }

        yield return new WaitForSeconds(2f);

        _finishPanel.SetActive(false);
        _finishText.text = "";

        //��Ȱ��ȭ(�׽�Ʈ�� ���� ��� ��Ȱ��ȭ)
        gameObject.SetActive(false);
    }

    /// <summary>
    /// ���� ���� ���� 5% ����(30% ���Ϸ� ���� �Ұ�)
    /// Ȥ�� �����̴� ������Ʈ�� �պ� �ð� 0.2�� ����(1�� ���Ϸ� ���� �Ұ�)
    /// </summary>
    /// <param name="level">���̵� ����, �������� ���� �������.</param>
    public void DifficultyChange(int level)
    {
        if (level < 0) return;
        //�������� �Ʒ� �� �ϳ� ����
        bool type = UnityEngine.Random.Range(0, 2) == 0;
        bool isChnaged = type ? DescSpeed(level) || DescZone(level) : DescZone(level) || DescSpeed(level);

        if (!isChnaged)
            Debug.Log("�ְ� ���̵��Դϴ�.");
    }

    /// <summary>
    /// �պ� �ӵ� ���� ����
    /// �Է¹��� ������ �´� �ӵ� ����
    /// </summary>
    /// <param name="level">���̵� ����, ���� ���� �պ� �ӵ��� �� ������.</param>
    /// <returns></returns>
    bool DescSpeed(int level)
    {
        float before = _speed;
        float temp = before - level * 0.2f;
        float after = Mathf.Clamp(temp, 1f, 1.6f);

        if (after < before)
        {
            _speed = after;
            Debug.Log($"after : {after} before : {before}");
            Debug.Log($"�պ� �ð� ���� {_speed}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// ���� ���� ���� ����
    /// �Է¹��� ������ �´� �������� ����
    /// </summary>
    /// <param name="level">���̵� ����, ���� ���� ���� ������ �� ������.</param>
    /// <returns></returns>
    bool DescZone(int level)
    {
        float before = _zoneWRate;
        float temp = before - level * 0.05f;
        float after = Mathf.Clamp(temp, 0.3f, 0.45f);

        if (after < before)
        {
            _zoneWRate = after;
            Debug.Log($"after : {after} before : {before}");
            Debug.Log($"���� ���� ���� {_zoneWRate}");
            return true;
        }
        return false;
    }


}