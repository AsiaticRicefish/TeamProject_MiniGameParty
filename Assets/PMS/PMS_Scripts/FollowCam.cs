using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowCam : MonoBehaviour
{
    public Transform target;
    private Vector3 offset = new Vector3(5f, 10f, 0f);

    private void FixedUpdate()
    {
        if (target == null) return;
        Vector3 newPos = target.transform.position + offset;
        transform.position = newPos;
    }
}
