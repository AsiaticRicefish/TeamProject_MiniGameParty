using UnityEngine;

/// <summary>
/// 메인 카메라의 Viewport를 Screen.safeArea에 맞춰 잘라냄(정규화 좌표).
/// </summary>
[RequireComponent(typeof(Camera))]
public class SafeAreaCameraViewport : MonoBehaviour
{
    Camera cam;
    Rect lastSafe;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; 
        cam.backgroundColor = Color.black;
    }

    void OnEnable() { Apply(); }
    void Update()
    {
        if (lastSafe != Screen.safeArea) Apply();
    }

    void Apply()
    {
        var s = Screen.safeArea;
        lastSafe = s;

        float W = Screen.width;
        float H = Screen.height;

        // 정규화 ViewportRect
        var r = new Rect(s.xMin / W, s.yMin / H, s.width / W, s.height / H);
        cam.rect = r;
    }
}