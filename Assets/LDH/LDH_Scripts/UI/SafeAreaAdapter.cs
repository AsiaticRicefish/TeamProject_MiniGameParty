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
        
        private static bool IsValidRect(Rect r)
            => !(float.IsNaN(r.x) || float.IsNaN(r.y) || float.IsNaN(r.width) || float.IsNaN(r.height))
               && r.width > 0f && r.height > 0f;

        
        private void Apply(bool force = false)
        {
            
            // root canvas 가져오기
            var root = GetComponentInParent<Canvas>()?.rootCanvas;
            
            Rect canvasRect;
            if (root != null)
            {
                canvasRect = root.pixelRect;
                // 아직 초기 프레임/에디터 상태로 width/height가 0일 수 있음 → 다음 이벤트에 재시도
                if (canvasRect.width <= 0f || canvasRect.height <= 0f || root.scaleFactor <= 0f) return;
            }
            else
            {
                if (Screen.width <= 0 || Screen.height <= 0) return;
                canvasRect = new Rect(0, 0, Screen.width, Screen.height);
            }
            
            // 3) 세이프 에어리어 확보 (유효하지 않으면 풀 스크린으로 대체)
            var safeArea = Screen.safeArea;
            if (!IsValidRect(safeArea))
                safeArea = new Rect(0, 0, Screen.width, Screen.height);
            
            if (!force && safeArea == _last) return;
            
            // 4) 정규화 (클램프로 NaN/Infinity 방지)
            Vector2 min = safeArea.position; // 좌하단 (픽셀)
            Vector2 max = min + safeArea.size; // 우상단 (픽셀)
            
            
            float w = canvasRect.width;
            float h = canvasRect.height;

            if (w <= 0f || h <= 0f) return;
            min.x = Mathf.Clamp01((min.x - canvasRect.xMin) / w);
            min.y = Mathf.Clamp01((min.y - canvasRect.yMin) / h);
            max.x = Mathf.Clamp01((max.x - canvasRect.xMin) / w);
            max.y = Mathf.Clamp01((max.y - canvasRect.yMin) / h);

            // 5) NaN 최종 방지
            if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y))
                return;
            

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