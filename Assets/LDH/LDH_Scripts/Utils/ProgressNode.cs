using System;
using System.Collections.Generic;
using UnityEngine;

namespace LDH_Util
{
    public sealed class ProgressNode
    {
        private readonly Action<float> _onUpdate;    // 루트만 가짐
        private readonly ProgressNode _parent;
        private float _normalizedStart;              
        private float _normalizedSpan;              
        private float _local;    
        
        public ProgressNode(Action<float> onUpdate)
        {
            _onUpdate = onUpdate;
            _parent = null;
            _normalizedStart = 0f;
            _normalizedSpan  = 1f;
        }
        
        // 자식 생성자 (부모 내부에서만 호출)
        private ProgressNode(ProgressNode parent, float start, float span)
        {
            _parent = parent;
            _onUpdate = parent._onUpdate;
            _normalizedStart = start;
            _normalizedSpan  = span;
        }
        
        /// 현재 노드에 0~1 진행률 보고
        public void Report(float local01)
        {
            _local = Mathf.Clamp01(local01);
            PushUp();
        }
        
        public void Complete() => Report(1f);

        private void PushUp()
        {
            float world = _normalizedStart + _normalizedSpan * _local;
            if (_parent != null)
            {
                _parent.Report(world); // 부모 기준으로 전달
            }
            else
            {
                _onUpdate?.Invoke(Mathf.Clamp01(world));
            }
        }

        public ProgressFan Fan(params float[] weights) => new ProgressFan(this, weights);

        public sealed class ProgressFan
        {
            private readonly ProgressNode _owner;
            private readonly float _sum;
            private readonly List<ProgressNode> _children = new();

            public ProgressFan(ProgressNode owner, float[] weights)
            {
                _owner = owner;
                _sum = 0f;
                foreach (var w in weights) _sum += Mathf.Max(0f, w);

                float acc = 0f;
                foreach (var w in weights)
                {
                    float span = (_sum <= 0f) ? 0f : Mathf.Max(0f, w) / _sum;
                    var child = new ProgressNode(owner, acc, span);
                    _children.Add(child);
                    acc += span;
                }
            }

            public ProgressNode this[int i] => _children[i];
            public IReadOnlyList<ProgressNode> Children => _children;
        }

    }
    
}