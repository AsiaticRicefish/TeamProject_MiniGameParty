using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    [Tooltip("순서대로 요요 애니메이션을 줄 UI 이미지들")]
    public RectTransform[] items;
    [Tooltip("각 이미지 사이 시작 지연 시간(초)")]
    public float staggerDelay = 0.2f;
    [Tooltip("요요 높이(px)")]
    public float jumpHeight = 30f;
    [Tooltip("한 사이클 유지 시간(초)")]
    public float duration = 0.5f;

    [Header("Start Image 반짝임")]
    public Image startButtonImage;
    public float blinkDuration = 0.5f;
    [Range(0f, 1f)] public float minAlpha = 0f;

    private Vector2[] originalPositions;
    private void Awake()
    {
        DOTween.Init();

        // 원본 위치 저장
        originalPositions = new Vector2[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            originalPositions[i] = items[i].anchoredPosition;
        }
    }
    private void Start()
    {
        for (int i = 0; i < items.Length; i++)
        {
            RectTransform rt = items[i];
            float startY = originalPositions[i].y;
            float delay = i * staggerDelay;

            rt.DOAnchorPosY(startY + jumpHeight, duration)
              .SetDelay(delay)
              .SetLoops(-1, LoopType.Yoyo)
              .SetEase(Ease.OutQuad)
              .OnStepComplete(() =>
              {
                  // 각 이미지가 착지할 때 실행
                  Debug.Log($"Item {i} landed");
              });
        }

        // 알파 값을 minAlpha까지 낮췄다가 원래대로 돌아오는 무한 요요
        startButtonImage
            .DOFade(minAlpha, blinkDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.Linear)
            .SetLink(startButtonImage.gameObject, LinkBehaviour.KillOnDisable);
    }

    private IEnumerator Test()
    {
        yield return new WaitForSeconds(5.0f);
        startButtonImage.gameObject.SetActive(false);
    }
}
