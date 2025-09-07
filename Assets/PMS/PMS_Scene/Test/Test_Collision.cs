using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test_Collision : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;

    [SerializeField] private float remainDistance;

    bool shootRequested = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) shootRequested = true;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (shootRequested)
        {
            remainDistance = GetEstimatedDistance(rb, transform.forward * 30, ForceMode.Impulse);
            rb.AddForce(transform.forward * 30, ForceMode.Impulse);
            shootRequested = false;
        }
    }

    float GetEstimatedDistance(Rigidbody rb, Vector3 force, ForceMode mode)
    {
        if (mode == ForceMode.Impulse)
        {
            Vector3 v0 = force / rb.mass;   // 순간 속도
            float distance = v0.magnitude / rb.drag; // 드래그 적용
            return distance;
        }
        else
        {
            // 지속 ForceMode Force는 시간과 반복 적용 필요
            return 0f; // 단순화
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        
    }
}
