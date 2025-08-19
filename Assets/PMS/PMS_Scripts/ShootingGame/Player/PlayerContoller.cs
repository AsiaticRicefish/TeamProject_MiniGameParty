using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerContoller : MonoBehaviour
{
    private Rigidbody _playerRb;
    // Update is called once per frame
    void Update()
    {
        _playerRb = GetComponent<Rigidbody>();
    }

    private void OnMouseDown()
    {
        _playerRb.AddForce(-_playerRb.transform.forward *  10f,ForceMode.Impulse);
    }
}
