using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

namespace LDH.LDH_Scripts.Test
{
    public class SceneControllerCreator : MonoBehaviour
    {
        [SerializeField] private string path;
        private void Awake()
        {
            StartCoroutine(CreateMainGameSceneController());
        }

        private void Start()
        {
            
        }

        private IEnumerator CreateMainGameSceneController()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.InstantiateRoomObject(path, Vector3.zero, Quaternion.identity);
            }

            yield return null;
        }
    }
}