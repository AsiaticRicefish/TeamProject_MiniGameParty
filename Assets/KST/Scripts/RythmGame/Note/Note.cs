using UnityEngine;
using DesignPattern;

namespace RhythmGame
{
    public partial class Note : MonoBehaviour
    {
        private PooledObject _pooled;

        void Awake() => _pooled = GetComponent<PooledObject>();
        public void ReturnPool() => _pooled.ReturnPool();
    }
}