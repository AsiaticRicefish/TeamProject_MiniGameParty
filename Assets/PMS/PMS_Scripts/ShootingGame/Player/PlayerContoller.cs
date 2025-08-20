using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerContoller : MonoBehaviour
{
    private Rigidbody _playerRb;
    private Vector2 startTouchPos;
    private Vector2 endTouchPos;

    [SerializeField] private float forceMultiplier = 10f;

    private void Awake()
    {
        _playerRb = GetComponent<Rigidbody>();
    }


    //Send Message
    /*private void OnTouchPosition(InputValue value)
    {
        Vector3 inputStartPosition = value.Get<Vector3>();
        Debug.Log($"[PlayerContoller] - Ŭ�� ��ǥ : X : {inputStartPosition.x}, Z : {inputStartPosition.z}");
    }*/

    //C# Invoke
    public void OnTouchPress(InputAction.CallbackContext ctx) //InputAction.CallbackContext context
    {
        Vector2 pos = ctx.ReadValue<Vector2>();

        if (ctx.started)
            Debug.Log("��ġ ����");
        else if (ctx.performed)
            Debug.Log("�巡�� ��");
        else if (ctx.canceled)
            Debug.Log("��ġ ����");

        Debug.Log("��ǥ: " + pos);
    }

}
