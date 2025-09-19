using UnityEngine;

[CreateAssetMenu(menuName = "Particles/ParticleData")]
public class ParticleData : ScriptableObject
{
    public string id;
    public string addressableKey;

    public int initialPoolSize = 10;
    public float duration = 2f;
}