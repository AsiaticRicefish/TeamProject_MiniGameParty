using System;
using Photon.Pun;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class scenecontrollertest : MonoBehaviour
    {
        [SerializeField] private string path;
        
        private void Awake()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.InstantiateRoomObject(path, Vector3.zero, Quaternion.identity);
            }
        }
    }
}