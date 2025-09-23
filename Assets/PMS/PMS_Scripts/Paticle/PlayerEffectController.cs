using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Managers;
using Cysharp.Threading.Tasks;
using static UnityEngine.ParticleSystem;
using UnityEngine.UIElements;

public enum EffectType { Dash, Collision }
public class PlayerEffectController : MonoBehaviour
{
    public void CollisionEffectPlay(string particleID, Vector3 position, Quaternion rotation)
    {
        Manager.Particle.PlayAsync(particleID, position, rotation).Forget();
    }

    public void ShotEffectPlay(string particleID, Vector3 position, Quaternion rotation, Func<bool> condition)
    {
        rotation = Quaternion.Euler(0, 90, 0); // Y축 기준 90도 회전
        Manager.Particle.PlayUntilAsync(particleID, position, rotation,condition,transform).Forget();
    }
}
