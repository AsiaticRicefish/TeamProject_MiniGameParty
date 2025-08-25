using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test_JengaCameraFocus : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] float moveTime = .35f;
    Vector3 _originPos; float _originSize;

    void Awake() { _originPos = cam.transform.position; _originSize = cam.orthographicSize; }

    public void FocusLayer(Vector3 layerCenter, float zoomSize)
    {
        StopAllCoroutines();
        StartCoroutine(FocusRoutine(layerCenter, zoomSize));
    }

    IEnumerator FocusRoutine(Vector3 targetPos, float zoom)
    {
        Vector3 startPos = cam.transform.position;
        float startSize = cam.orthographicSize;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveTime;
            cam.transform.position = Vector3.Lerp(startPos, new Vector3(targetPos.x, startPos.y, targetPos.z - 5f), t);
            cam.orthographicSize = Mathf.Lerp(startSize, zoom, t);
            yield return null;
        }
    }

    public void ResetFocus() => FocusLayer(_originPos, _originSize);
}
