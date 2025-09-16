using UnityEngine;
using UnityEngine.UI;


public class UnimoRTBBinder : MonoBehaviour
{
    [SerializeField] private Camera unimoCam;
    [SerializeField] private RawImage rawImage;

    private RenderTexture _rt;

    void Awake()
    {
        if (!unimoCam || !rawImage)
        {
            Debug.LogError("[UnimoRTBinder] Camera/RawImage reference missing");
            return;
        }

        unimoCam.fieldOfView = 15f;

        var size = PickRTSizeFromRawImage(rawImage);
        _rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
        {
            useMipMap = false,
            antiAliasing = 1
        };
        _rt.Create();

        unimoCam.targetTexture = _rt;
        rawImage.texture = _rt;
        rawImage.raycastTarget = false; // 로딩 중 클릭 방지
    }

    int PickRTSizeFromRawImage(RawImage ri)
    {
        var rect = (ri.transform as RectTransform).rect;
        int px = Mathf.RoundToInt(Mathf.Max(rect.width, rect.height));

        if (px >= 1080) return 1080;
        if (px >= 900) return 900;
        if (px >= 720) return 720;
        return 512;
    }

    void OnDestroy()
    {
        if (unimoCam) unimoCam.targetTexture = null;
        if (rawImage) rawImage.texture = null;
        if (_rt)
        {
            _rt.Release();
            Destroy(_rt);
        }
    }
}