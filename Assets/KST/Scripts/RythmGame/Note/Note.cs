using UnityEngine;
using DesignPattern;
using System;

namespace RhythmGame
{
    public partial class Note : MonoBehaviour
    {
        private PooledObject _pooled;

        public event Action<Note> OnDespawn;

        void Awake() => _pooled = GetComponent<PooledObject>();
        public void ReturnPool()
        {
            Status = NoteStatus.None;
            OnDespawn?.Invoke(this);
            _pooled.ReturnPool();

        } 

        void OnDisable()
        {
            OnDespawn?.Invoke(this);
        }
    }
}