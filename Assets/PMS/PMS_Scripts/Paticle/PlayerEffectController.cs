using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Managers;
using Cysharp.Threading.Tasks;

public enum EffectType { Dash, Collision }
public class PlayerEffectController : MonoBehaviour
{
    public void Play(string particleID, Vector3 position, Quaternion rotation)
    {
        switch (particleID)
        {
            case ParticleIDs.SH_UnimoCollisionEffect:
                rotation = Quaternion.Euler(0, 90, 0); // Y축 기준 90도 회전
                Manager.Particle.PlayAsync(particleID, position, rotation).Forget();
                break;
            case ParticleIDs.SH_UnimoShot:
                Manager.Particle.PlayAsync(particleID, position, rotation).Forget();
                break;
        }
    }
}
