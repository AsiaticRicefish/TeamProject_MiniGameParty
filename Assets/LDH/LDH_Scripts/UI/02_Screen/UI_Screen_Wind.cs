using System;
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
            
        }

        public void SetWindUI(Define_LDH.WindDirection direction, float velocity)
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
        public void SetWindDirection(Define_LDH.WindDirection direction)
        {
            //east를 가리키는 화살표를 기준으로 함
            //이미지 회전 처리
            float angle = direction switch
            {
                Define_LDH.WindDirection.North => 90,
                Define_LDH.WindDirection.West => 180,
                Define_LDH.WindDirection.South => 270,
                Define_LDH.WindDirection.East => 0,
                _ => 0,
            };
            arrowImage.transform.Rotate(0, 0, angle);
        }
        
    }
}