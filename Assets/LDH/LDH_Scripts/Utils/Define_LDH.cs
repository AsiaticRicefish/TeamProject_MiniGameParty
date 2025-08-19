using UnityEngine;

namespace LDH_Util
{
    public class Define_LDH
    {
        public enum Mode
        {
            Anchors, // anchorMin/Max를 안전영역에 맞춤
            Padding  // 부모 전체 스트레치 + offset으로 안전영역만큼 여백
        }

    }
}