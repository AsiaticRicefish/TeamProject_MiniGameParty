using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public class Test_PointerProbe : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData e)
    {
        Dump("DOWN", e);
    }
    public void OnDrag(PointerEventData e)
    {
        Dump("DRAG", e);
    }
    public void OnPointerUp(PointerEventData e)
    {
        Dump("UP", e);
    }
    void Dump(string tag, PointerEventData e)
    {
        var sb = new StringBuilder();
        sb.Append($"[{tag}] pid={e.pointerId} pos={e.position}");
        var go = e.pointerCurrentRaycast.gameObject;
        sb.Append($" hit={(go != null ? go.name : "NULL")}");
        Debug.Log(sb.ToString());
    }
} 
