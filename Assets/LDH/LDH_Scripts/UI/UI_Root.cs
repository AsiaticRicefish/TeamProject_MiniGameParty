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


        public GameObject TopArea => _topArea;
        public GameObject CenterArea => _centerArea;
        public GameObject BottomArea => _bottomArea;

    }
}