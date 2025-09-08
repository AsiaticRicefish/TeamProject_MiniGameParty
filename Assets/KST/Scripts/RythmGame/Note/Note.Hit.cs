// using DesignPattern;
// using UnityEngine;

// namespace RhythmGame
// {
//     partial class Note : MonoBehaviour
//     {
//         [SerializeField] PooledObject _hitEffectPrefab;
//         private ObjectPool _effectPool;

//         void Start()
//         {
//             _effectPool = new(null, _hitEffectPrefab, 5);
//         }
//         public void HitEffect()
//         {
//             var effect = _effectPool.PopPool();
//             effect.transform.SetPositionAndRotation(transform.position, Quaternion.identity);

//             var ps = effect.GetComponent<par
//         }
//     }
// }