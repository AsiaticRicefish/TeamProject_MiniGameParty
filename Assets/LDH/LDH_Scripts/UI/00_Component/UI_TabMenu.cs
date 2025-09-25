using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_TabMenu : MonoBehaviour
    {
        [Serializable]
        public class TabPage
        {
            public string id;
            public Toggle toggle;

            public GameObject[] showObjects;
            public GameObject[] hideObjects;

            public Image tabIcon;
            [Range(0,1)]
            public float inactiveAlpha;
            
            public UnityEvent<string> onSelected;
            public UnityEvent<string> onDeselected;
        }
        
        
        [SerializeField] private ToggleGroup tabGroup;
        [SerializeField] private List<TabPage> pages;
        [SerializeField] private int defaultIndex = 0;
        

        private void Awake()
        {
            // 토글 그룹 설정
            foreach (TabPage tabPage in pages)
            {
                if (tabPage.toggle != null)
                    tabPage.toggle.group = tabGroup;
            }
            
            // 리스너 등록
            for (int i = 0; i < pages.Count; i++)
            {
                int idx = i;
                var t = pages[idx].toggle;
                if(t==null) continue;
                
                t.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) SetActiveTab(idx);
                });
            }
        }

        private void Start()
        {
            defaultIndex = Mathf.Clamp(defaultIndex, 0, pages.Count - 1);
            
            if (pages.Count > 0 && pages[defaultIndex].toggle != null)
            {
                pages[defaultIndex].toggle.SetIsOnWithoutNotify(true);
            }
        }

        /// <summary>
        /// index번째 탭을 활성화하고 나머지는 비활성화
        /// </summary>
        public void SetActiveTab(int index)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                var p = pages[i];
                bool active = (i == index);
                SetActive(p, active, invokeEvent:true);
            }
        }
        
        
        private void SetActive(TabPage p, bool active, bool invokeEvent)
        {
            if (p == null) return;

            // 아이콘
            var color = p.tabIcon.color;
            color.a = active? 1: p.inactiveAlpha;
            p.tabIcon.color = color;
            
            // 보이기/숨기기
            if (p.showObjects != null)
                foreach (var go in p.showObjects)
                    if (go) go.SetActive(active);

            if (p.hideObjects != null)
                foreach (var go in p.hideObjects)
                    if (go) go.SetActive(!active);
            

            // 콜백
            if (invokeEvent)
            {
                if (active) p.onSelected?.Invoke(p.id);
                else        p.onDeselected?.Invoke(p.id);
            }
        }
     
    }
}