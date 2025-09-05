using System;
using Managers;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class ScreenUISpawnTEst : MonoBehaviour
    {
        private MainGameDebugPanel _debugUI;
        private void Start()
        {
            _debugUI = Manager.UI.CreateScreenUI<MainGameDebugPanel>();
            Debug.Log(_debugUI==null);
            Manager.UI.ShowScreenUI(_debugUI);
        }
    }
}