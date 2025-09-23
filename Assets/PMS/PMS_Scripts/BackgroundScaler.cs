using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundScaler : MonoBehaviour
{
    [SerializeField] private float extraScale = 1.1f; // 여유 비율 (10% 크게)
    void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        FitToScreen(sr);
    }

    void FitToScreen(SpriteRenderer sr)
    {
        Camera cam = Camera.main;

        // 카메라와 배경 사이의 Z 거리
        float distance = Mathf.Abs(transform.position.z - cam.transform.position.z);

        // 해당 거리에서의 화면 높이와 너비 계산
        float screenHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float screenWidth = screenHeight * cam.aspect;

        // 스프라이트 원래 크기
        Vector2 spriteSize = sr.sprite.bounds.size;

        // 화면 대비 스케일
        float scaleX = screenWidth / spriteSize.x;
        float scaleY = screenHeight / spriteSize.y;

        // 여유분: 큰 쪽 기준으로 스케일 맞추기
        float finalScale = Mathf.Max(scaleX, scaleY) * extraScale;

        // 적용
        transform.localScale = new Vector3(finalScale, finalScale, 1f);
    }
}
