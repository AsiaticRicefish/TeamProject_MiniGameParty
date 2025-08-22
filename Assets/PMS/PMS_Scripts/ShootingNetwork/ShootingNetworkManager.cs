using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;
using Photon.Pun;

namespace ShootingScene
{
    [RequireComponent(typeof(PhotonView))]
    public class ShootingNetworkManager : PunSingleton<ShootingGameManager>, IGameComponent
    {
        protected override void OnAwake()
        {
            base.isPersistent = false;
        }

        public void Initialize()
        {
            throw new System.NotImplementedException();
        }
    }
}