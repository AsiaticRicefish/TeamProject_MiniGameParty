using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMS_Legacy
{
    public class CardFlip : MonoBehaviour
    {
        private RectTransform rt;
        private bool isFlipped = false;
        private bool isFlipping = false;
        private float flipDuration = 0.5f; // 뒤집는 시간
        private Quaternion startRot;
        private Quaternion endRot;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
        }
        public void Flip()
        {
            if (isFlipping) return; // 진행 중이면 무시

            isFlipped = !isFlipped;
            startRot = rt.localRotation;                //시작 회전값을 저장
            endRot = startRot * Quaternion.Euler(0, 180, 0); // y축 회전
            StartCoroutine(FlipAnim());
        }

        private IEnumerator FlipAnim()
        {
            isFlipping = true;
            float time = 0;

            while (time < flipDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / flipDuration);
                rt.localRotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            endRot = startRot;
            isFlipping = false;
        }
    }
}
