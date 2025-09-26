using UnityEngine;

// 메테오 씬 카메라 강제 프리셋(런타임 적용)
// - 실행 초기에 Culling Mask를 Everything으로 맞추고,
// - 나중에 원하시면 AllowList/BlockList 방식으로 특정 레이어만 남기세요.
namespace KYG
{
    
[DisallowMultipleComponent]
public class MeteorCameraPreset : MonoBehaviour
{
    [Header("카메라를 지정하지 않으면 GetComponent로 찾아요")]
    public Camera targetCamera;

    [Tooltip("실행 시작 시 Everything으로 강제 설정")]
    public bool forceEverythingMaskAtStart = true;

    [Tooltip("Everything 이후 허용할 레이어만 남기기 (선택)")]
    public string[] allowLayers = new string[] { "Default", "UI", "BackGround", "Unimo", "Arena_0", "Arena_1", "Arena_2", "Arena_3" };

    void Awake()
    {
        if (!targetCamera) targetCamera = GetComponent<Camera>();
        if (!targetCamera) return;

        // 1) 일단 모든 레이어를 보도록 설정 (원인 파악을 위해)
        if (forceEverythingMaskAtStart)
        {
            targetCamera.cullingMask = ~0; // Everything
        }

        // 2) 필요 시 허용 레이어로만 좁히기 (선택)
        if (allowLayers != null && allowLayers.Length > 0)
        {
            int mask = 0;
            foreach (var layer in allowLayers)
            {
                int layerIndex = LayerMask.NameToLayer(layer);
                if (layerIndex >= 0) mask |= (1 << layerIndex);
            }
            // 모든 허용 레이어가 유효하게 계산된 경우에만 적용
            if (mask != 0) targetCamera.cullingMask = mask;
        }

        // 안전 기본값(원하는 값으로 수정 가능)
        targetCamera.clearFlags = CameraClearFlags.Skybox;
        targetCamera.orthographic = false;
        targetCamera.nearClipPlane = 0.1f;
        targetCamera.farClipPlane = 1000f;
    }
}
}
