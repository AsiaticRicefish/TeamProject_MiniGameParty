using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EffectType { Dash, Collision }
public class PlayerEffectController : MonoBehaviour
{
    [SerializeField] private float defaultLifetime = 2f;

    [SerializeField] private GameObject dashEffect;
    [SerializeField] private GameObject collisionEffect;

    public void Play(EffectType effectType, Vector3 position, Quaternion rotation)
    {
        GameObject effect = null;

        switch (effectType)
        {
            case EffectType.Dash:
                rotation = Quaternion.Euler(0, 90, 0); // Y축 기준 90도 회전
                effect = Instantiate(dashEffect, position, rotation);
                break;
            case EffectType.Collision:
                effect = Instantiate(collisionEffect, position, rotation);
                break;
        }

        if (effect != null)
            Destroy(effect, defaultLifetime); // 자동 제거
    }
}
