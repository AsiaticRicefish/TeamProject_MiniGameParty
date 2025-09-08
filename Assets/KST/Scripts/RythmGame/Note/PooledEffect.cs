using System.Collections;
using DesignPattern;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(PooledObject))]

public class PooledEffect : MonoBehaviour
{
    [SerializeField] ParticleSystem _ps;
    PooledObject _pooled;

    void Start()
    {
        _pooled = GetComponent<PooledObject>();
    }

    public void PlayEffect(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);

        //파티클 한 번 정리
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _ps.Play(true);

        StartCoroutine(IE_Return());
    }

    IEnumerator IE_Return()
    {
        yield return new WaitWhile(() => _ps.IsAlive(true));
        _pooled.ReturnPool();
    }
}