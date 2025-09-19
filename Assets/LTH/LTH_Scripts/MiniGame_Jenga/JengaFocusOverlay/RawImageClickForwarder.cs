using InputBlocker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

/// <summary>
/// RawImage 위 입력을 TowerCam의 Ray로 변환:
/// - 클릭: Overlay.NotifyBlockTapped (선택/교체)
/// - 이동: Overlay.NotifyHover (임시 윤곽선)
/// </summary>
public class RawImageClickForwarder : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private TowerFocusOverlay overlay;
    [SerializeField] private LayerMask jengaMask;
    [SerializeField] private bool compensateLetterbox = true;
    [SerializeField] private bool ignoreClicksOutsideDraw = true;
    [SerializeField] private float rayDistance = 100f;

    private RawImage raw;
    private RectTransform rt;

    private void Awake()
    {
        raw = GetComponent<RawImage>();
        rt = (RectTransform)transform;
        if (!overlay) overlay = GetComponentInParent<TowerFocusOverlay>();
        jengaMask = LayerMask.GetMask("JengaFace");
    }

    public void SetMask(LayerMask mask)
    {
        jengaMask = mask;
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (InputManager.Instance &&
            (InputManager.Instance.IsBlocked(InputType.UI) ||
             InputManager.Instance.IsBlocked(InputType.Interaction)))
        {
            return;
        }

        if (!PrepareRay(eventData, out var ray))
        {
            return;
        }

        var hits = Physics.RaycastAll(ray, rayDistance, jengaMask);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            var proxy = h.collider.GetComponent<FaceHitProxy>();
            if (proxy == null)
            {
                continue;
            }

            var block = proxy.owner;

            if (block != null &&
                JengaTowerManager.Instance != null &&
                JengaTowerManager.Instance.IsArenaMuted(block.OwnerActorNumber))
                return;

            overlay.NotifyBlockTapped(block);
            return;
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (InputManager.Instance && InputManager.Instance.IsBlocked(InputType.Interaction))
        { overlay?.NotifyHover(null); return; }

        if (!PrepareRay(eventData, out var ray)) { overlay?.NotifyHover(null); return; }

        if (Physics.Raycast(ray, out var hit, rayDistance, jengaMask))
        {
            var block = hit.collider.GetComponentInParent<JengaBlock>();

            if (block != null &&
                JengaTowerManager.Instance != null &&
                JengaTowerManager.Instance.IsArenaMuted(block.OwnerActorNumber))
            { overlay?.NotifyHover(null); return; }

            overlay?.NotifyHover(block);
        }
        else
        {
            overlay?.NotifyHover(null);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        overlay?.NotifyHover(null);
    }

    private bool PrepareRay(PointerEventData eventData, out Ray ray)
    {
        ray = default;
        if (overlay == null || !overlay.IsActive)
        {
            return false;
        }

        var cam = overlay.TowerCam;
        if (cam == null)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt, eventData.position, eventData.pressEventCamera, out var local))
        {
            return false;
        }

        Rect draw = GetDrawRectLocal(raw);
        if (!draw.Contains(local))
        {
            if (ignoreClicksOutsideDraw)
            {
                return false;
            }

            local = new Vector2(
                Mathf.Clamp(local.x, draw.xMin, draw.xMax),
                Mathf.Clamp(local.y, draw.yMin, draw.yMax));
        }

        float u = Mathf.InverseLerp(draw.xMin, draw.xMax, local.x);
        float v = Mathf.InverseLerp(draw.yMin, draw.yMax, local.y);
        ray = cam.ViewportPointToRay(new Vector3(u, v, 0f));

        return true;
    }

    private Rect GetDrawRectLocal(RawImage img)
    {
        var r = rt.rect;

        if (img.texture == null || !compensateLetterbox)
        {
            return r;
        }

        float texAspect = (float)img.texture.width / img.texture.height;
        float rectAspect = r.size.x / r.size.y;

        if (Mathf.Approximately(texAspect, rectAspect)) return r;

        if (texAspect > rectAspect)
        {
            float h = r.size.x / texAspect;
            return new Rect(r.xMin, -h * 0.5f, r.size.x, h);
        }
        else
        {
            float w = r.size.y * texAspect;
            return new Rect(-w * 0.5f, r.yMin, w, r.size.y);
        }
    }
}