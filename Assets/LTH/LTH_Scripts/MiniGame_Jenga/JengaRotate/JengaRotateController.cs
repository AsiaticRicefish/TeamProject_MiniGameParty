using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JengaRotateController : MonoBehaviour
{
    [Header("회전 옵션")]
    [SerializeField] float angle = 90f;
    [SerializeField] float duration = 0.15f; // 회전 시간

    Quaternion _baseRot;
    Quaternion _rotated;
    bool _toggled;
    Coroutine _co;

    void Awake()
    {
        _baseRot = transform.rotation;
        _rotated = _baseRot * Quaternion.Euler(0f, angle, 0f);
    }

    /// <summary>
    /// 버튼에서 호출: 90° ↔ 원복
    /// </summary>
    public void Toggle()
    {
        _toggled = !_toggled;
        var target = _toggled ? _rotated : _baseRot;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(RotateTo(target));
    }

    /// <summary>
    /// 오버레이 닫힐 때 원복 보장
    /// </summary>
    public void ResetToBase()
    {
        _toggled = false;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(RotateTo(_baseRot));
    }

    IEnumerator RotateTo(Quaternion target)
    {
        if (duration <= 0f) { transform.rotation = target; yield break; }

        var start = transform.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            transform.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.rotation = target;

        yield return null; // 1프레임 대기 (RT / RectTransform 반영 대기)

        var overlay = FindFirstObjectByType<TowerFocusOverlay>(FindObjectsInactive.Include);
        if (overlay != null)
        {
            overlay.Reframe();          // 카메라 위치/RT 재계산
            overlay.TowerCam?.Render(); // RenderTexture 강제 갱신
        }
    }
}
