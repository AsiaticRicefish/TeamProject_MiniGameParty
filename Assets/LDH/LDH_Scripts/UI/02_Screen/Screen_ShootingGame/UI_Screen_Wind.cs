using System;
using System.Collections;
using LDH_Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Screen_Wind : UI_Screen
    {
        [SerializeField] private Image arrowImage;
        [SerializeField] private TMP_Text velocityText;

        //todo: 바람을 바꿔주는 대상의 이벤트를 구독하면 자동으로 바뀌게 연결하기
        protected override void Init()
        {
            base.Init();
            //구독 (set wind ui)
            //WindSystem.Instance.windChanged += SetWindUI;
        }

        private void OnEnable()
        {
            WindSystem.Instance.windChanged += SetWindUI;
        }

        private IEnumerator WaitForInitWindSystem()
        {
            yield return new WaitUntil(() => WindSystem.Instance != null);
            Debug.Log("윈드 시스템 변경값에 UI갱신 구독");
            WindSystem.Instance.windChanged += SetWindUI;
        }

        public void SetWindUI(WindDirection direction, float velocity)
        {

            SetWindDirection(direction);
            SetWindVelocity(velocity);
        }


        public void SetWindVelocity(float velocity)
        {
            velocityText.text = $"{velocity}m/s";
        }

        /// <summary>
        /// 향하는 방향을 direction에 넣어주면 된다.
        /// </summary>
        /// <param name="direction"></param>
        public void SetWindDirection(WindDirection direction)
        {
            //east를 가리키는 화살표를 기준으로 함
            //이미지 회전 처리
            float angle = direction switch
            {
                WindDirection.Up => 90,
                WindDirection.Left => 180,
                WindDirection.Down => 270,
                WindDirection.Right => 0,
                _ => 0,
            };
            arrowImage.transform.eulerAngles = new Vector3(0f, 0f, angle);
        }
        
    }
}