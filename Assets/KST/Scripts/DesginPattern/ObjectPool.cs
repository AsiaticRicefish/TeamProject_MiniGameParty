using System.Collections.Generic;
using UnityEngine;

namespace DesignPattern
{
    public class ObjectPool
    {
        private Stack<PooledObject> _stack;
        private PooledObject _targetPrefab;
        private GameObject _poolObject;

        public ObjectPool(Transform parent, PooledObject targetPrefab, int initSize = 5) => Init(parent, targetPrefab, initSize);

        private void Init(Transform parent, PooledObject targetPrefab, int initSize)
        {
            _stack = new Stack<PooledObject>(initSize);
            _targetPrefab = targetPrefab;
            _poolObject = new GameObject($"{targetPrefab.name} Pool");
            _poolObject.transform.parent = parent;

            for (int i = 0; i < initSize; i++)
            {
                CreatePooledObject();
            }
        }

        public PooledObject PopPool()
        {
            if (_stack.Count == 0) CreatePooledObject();

            PooledObject pooledObject = _stack.Pop();
            pooledObject.gameObject.SetActive(true);
            return pooledObject;
        }

        public void PushPool(PooledObject target)
        {
            target.transform.parent = _poolObject.transform;
            target.gameObject.SetActive(false);
            _stack.Push(target);
        }


        private void CreatePooledObject()
        {
            PooledObject obj = MonoBehaviour.Instantiate(_targetPrefab);
            obj.PooledInit(this);
            PushPool(obj);
        }
    }
}