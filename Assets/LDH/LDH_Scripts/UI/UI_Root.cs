using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LDH_UI
{
    public class UI_Root : MonoBehaviour
    {
        [SerializeField] private GameObject _topArea;
        [SerializeField] private GameObject _centerArea;
        [SerializeField] private GameObject _bottomArea;
        [SerializeField] private GameObject _defaultArea;

        public GameObject TopArea => _topArea;
        public GameObject CenterArea => _centerArea;
        public GameObject BottomArea => _bottomArea;
        public GameObject DefaultArea => _defaultArea;
    }
}