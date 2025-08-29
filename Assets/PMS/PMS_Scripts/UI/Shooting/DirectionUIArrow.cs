using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class DirectionUIArrow : MonoBehaviourPun
{
    [Header("Swing Settings")]
    public float swingAngle = 45f;
    public float swingSpeed = 2f;
    private RectTransform arrowTransform; // UI 화살표
    [SerializeField]private RectTransform TargetPos;

    private float currentAngle;
    private bool isSwing = true; // 스윙 상태
    private float freezeAngle;   // 멈췄을 때 각도

    [SerializeField] GameObject player;

    public float CurrentAngle => isSwing ? currentAngle : freezeAngle;

    public Vector3 CurrentDir
    {
        get
        {
            // UI 좌표 기준 화살표 → 플레이어 방향
            //Vector2 arrowPos = arrowTransform.position;
            Vector3 playerPos = player.transform.position;//RectTransformUtility.WorldToScreenPoint(Camera.main, player.transform.position);

            Vector3 dir = TargetPos.transform.position - playerPos;
            dir.y = 0; 
            return dir.normalized;
        }
    }

    private void Awake()
    {
        arrowTransform = GetComponent<RectTransform>();
        player = transform.parent.parent.gameObject;
    }

    private void Update()
    {
        if (isSwing) currentAngle = Mathf.Sin(Time.time * swingSpeed) * swingAngle;

        if (arrowTransform != null)
            arrowTransform.localRotation = Quaternion.Euler(0f, 0f, isSwing ? currentAngle : freezeAngle);

        //테스트 코드
        /*if(Input.anyKeyDown)
        {
            Freeze();
        }*/
    }

    public void Freeze()
    {
        freezeAngle = currentAngle;
        isSwing = false;
        //arrowTransform.gameObject.SetActive(false);
    }

    public void Resume() => isSwing = true;
}
