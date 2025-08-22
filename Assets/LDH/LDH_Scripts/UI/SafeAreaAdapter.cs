using UnityEngine;

namespace LDH_UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAdapter : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _last;

        private void OnEnable() => Init();

        private void Init()
        {
            FindComponent();
            Apply(true);
        }

        // 화면 크기/회전/디바이스 시뮬레이터 변경 시 호출
        private void OnRectTransformDimensionsChange()
        {
            if(!isActiveAndEnabled) return;     // enabled == true이고 계층에서 활성화 상태가 아니면 return

            FindComponent();
            Apply();
        }

        private void FindComponent()
        {
            if (_rt == null)
                _rt = GetComponent<RectTransform>();
        }
        
        private void Apply(bool force = false)
        {
            Rect safeArea = Screen.safeArea;
            if (!force && safeArea == _last) return;

            // root canvas 가져오기
            var root = GetComponentInParent<Canvas>()?.rootCanvas;
            Rect canvasRect = root != null
                ? root.pixelRect
                : new Rect(0, 0, Screen.width, Screen.height);


            Vector2 min = safeArea.position; // 좌하단 (픽셀)
            Vector2 max = min + safeArea.size; // 우상단 (픽셀)


            // 캔버스 픽셀Rect 기준으로 정규화
            min.x = (min.x - canvasRect.xMin) / canvasRect.width;
            min.y = (min.y - canvasRect.yMin) / canvasRect.height;
            max.x = (max.x - canvasRect.xMin) / canvasRect.width;
            max.y = (max.y - canvasRect.yMin) / canvasRect.height;

            // 앵커 변경 (offset은 0으로)
            if (_rt.anchorMin != min || _rt.anchorMax != max)
            {
                _rt.anchorMin = min;
                _rt.anchorMax = max;
                _rt.offsetMin = Vector2.zero;
                _rt.offsetMax = Vector2.zero;
            }

            _last = safeArea;
        }
    }
}