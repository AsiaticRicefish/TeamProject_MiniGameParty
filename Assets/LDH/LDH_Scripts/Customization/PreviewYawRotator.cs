using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Customization
{
    public class PreviewYawRotator : MonoBehaviour, IDragHandler
    {
        [Header("UI")]
        [SerializeField] private RawImage rawImage;
        
        [Header("Target")]
        [SerializeField] private Transform target;              // 회전시킬 캐릭터(루트)
        
        
        [Header("Control")]
        [SerializeField, Tooltip("픽셀당 회전각(도)")]  private float yawPerPixel = 0.25f;     // 픽셀 당 회전각(도)
        [SerializeField] private bool invert = false;  // 드래그 방향 반전
        [SerializeField] private bool useWorldSpace = false; // 월드 Y축 기준 회전

        
        
        private Quaternion _originRotation;    // 초기 회전 상태
        private bool _hasOrigin;
        
        
        void Awake()
        {
            if (!rawImage) rawImage = GetComponent<RawImage>();
            if (!rawImage)
            {
                Debug.LogWarning("[PreviewYawRotator] RawImage가 없습니다.", this);
            }
            else
            {
                rawImage.raycastTarget = true;
            }
            
        }

        
        public void OnDrag(PointerEventData eventData)
        {
            if (!target) return;
            
            float sign = invert ? -1f : 1f;
            float yaw  = - eventData.delta.x * yawPerPixel * sign;
            
            var space = useWorldSpace ? Space.World : Space.Self;  // 월드 기준 or 로컬 축 기준
            target.Rotate(Vector3.up, yaw, space);
            
        }
        

        /// <summary>
        /// 현재 target의 회전을 원래 회전으로 저장
        /// </summary>
        public void CaptureOrigin()
        {
            if (!target) { _hasOrigin = false; return; }
            _originRotation = target.rotation;
            _hasOrigin = true;
        }
        
        
        /// <summary>
        /// 원래 회전으로 복구
        /// </summary>
        public void ResetRotate()
        {
            if (!target || !_hasOrigin) return;
            target.rotation = _originRotation;
        }
    }
}