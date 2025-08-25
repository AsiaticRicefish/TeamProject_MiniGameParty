using DesignPattern;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class JengaUIManager : CombinedSingleton<JengaUIManager>, IGameComponent
{
    [Header("UI ���")]
    [SerializeField] private TMP_Text timerText; // Ÿ�̸� ��� UI

    [Header("ī��Ʈ�ٿ� UI")]
    [SerializeField] private GameObject countdownPanel;      // ī��Ʈ�ٿ� �г�
    [SerializeField] private TMP_Text countdownText;         // ī��Ʈ�ٿ� �ؽ�Ʈ (3, 2, 1, START!)

    protected override void OnAwake()
    {
        base.isPersistent = false; // ���� �������� ���
        base.OnAwake();
    }

    public void Initialize()
    {
        Debug.Log("[JengaUIManager - Initialize] UI �Ŵ��� �ʱ�ȭ ����");

        // ���� �Ŵ����� �ð� ������Ʈ �̺�Ʈ ����
        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.OnTimeUpdated += UpdateTimerUI;
            JengaGameManager.Instance.OnGameStateChanged += OnGameStateChanged; // ���� ���� ���� �̺�Ʈ ����

            // �ʱ� �ð� ����
            UpdateTimerUI(JengaGameManager.Instance.GetRemainingTime());
            Debug.Log("[JengaUIManager - Initialize] �̺�Ʈ ���� �Ϸ�");
        }
        else
        {
            Debug.LogWarning("[JengaUIManager - Initialize] JengaGameManager.Instance�� null�Դϴ�.");
        }

        // UI ��ҵ� �ʱ� ���� ����
        InitializeUI();

        Debug.Log("[JengaUIManager - Initialize] UI �Ŵ��� �ʱ�ȭ �Ϸ�");
    }

    #region UI �ʱ� Ȱ��ȭ ����
    /// <summary>
    /// UI ��ҵ� �ʱ� ���� ����
    /// </summary>
    private void InitializeUI()
    {
        // Ÿ�̸� �ؽ�Ʈ Ȱ��ȭ (���� ���� ������ ��)
        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
        }

        // ī��Ʈ�ٿ� �г� ��Ȱ��ȭ (�ʿ��� ���� Ȱ��ȭ)
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        // ī��Ʈ�ٿ� �ؽ�Ʈ�� �̸� ����
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }
    }
    #endregion

    #region ī��Ʈ�ٿ� UI
    /// <summary>
    /// ���� ���� ���� �� ȣ��Ǵ� �̺�Ʈ �ڵ鷯
    /// </summary>
    private void OnGameStateChanged(JengaGameState newState)
    {
        switch (newState)
        {
            case JengaGameState.Playing:
                // ���� ���� �� ī��Ʈ�ٿ� UI ���� (Ȥ�� �������� ��츦 ���)
                HideCountdown();
                break;

            case JengaGameState.Finished:
                // ���� ���� �� ó��
                break;
        }
    }

    /// <summary>
    /// ��Ʈ��ũ�� ���� ����ȭ�� ī��Ʈ�ٿ� ���� (��� Ŭ���̾�Ʈ���� ���� ����)
    /// </summary>
    public void StartCountdown(float duration)
    {
        if (countdownPanel != null && countdownText != null)
        {
            countdownPanel.SetActive(true);
            StartCoroutine(CountdownCoroutine(duration));
        }
    }

    /// <summary>
    /// ī��Ʈ�ٿ� UI �����
    /// </summary>
    public void HideCountdown()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }

    /// <summary>
    /// ����ȭ�� ī��Ʈ�ٿ� �ڷ�ƾ
    /// </summary>
    private IEnumerator CountdownCoroutine(float duration)
    {
        int countdown = Mathf.RoundToInt(duration);

        // ���� ī��Ʈ�ٿ� (3, 2, 1)
        while (countdown > 0)
        {
            countdownText.text = countdown.ToString();
            StartCoroutine(ScaleAnimation(countdownText.transform));

            yield return new WaitForSeconds(1f);
            countdown--;
        }

        // "START!" ǥ��
        countdownText.text = "START!";
        StartCoroutine(ScaleAnimation(countdownText.transform));

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// ������ ������ �ִϸ��̼� (�ӽ÷� ���� �ڵ�� �����ϰų� ���� ���� ����)
    /// </summary>
    private IEnumerator ScaleAnimation(Transform target)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.localScale;
        Vector3 largeScale = originalScale * 1.2f;

        // Ŀ����
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            target.localScale = Vector3.Lerp(originalScale, largeScale, elapsed / 0.2f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ���� ũ���
        elapsed = 0f;
        while (elapsed < 0.3f)
        {
            target.localScale = Vector3.Lerp(largeScale, originalScale, elapsed / 0.3f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localScale = originalScale;
    }

    #endregion

    #region Ÿ�̸� UI
    private void UpdateTimerUI(float remainingTime)
    {
        if (timerText != null && JengaGameManager.Instance != null)
        {
            timerText.text = JengaGameManager.Instance.GetFormattedTime();
        }
    }

    #endregion

    protected override void OnDestroy()
    {
        // �޸� ���� ������ ���� �̺�Ʈ ���� ����
        if (JengaGameManager.Instance != null)
        {
            JengaGameManager.Instance.OnTimeUpdated -= UpdateTimerUI;
            JengaGameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }
        base.OnDestroy();
    }

}