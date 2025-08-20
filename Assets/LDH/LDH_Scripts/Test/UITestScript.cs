using LDH_UI;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class UITestScript : MonoBehaviour
    {

        private int cnt = 0;
        public void ShowToastUI()
        {
            UIManager.Instance.EnqueueToast($"this is test for toast ui {cnt++}");
        }
    }
}