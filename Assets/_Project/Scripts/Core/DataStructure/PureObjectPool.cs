using Scripts.Core.inteface;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.Utils;

namespace Scripts.Core
{
    public class PureObjectPool<T> where T : IPoolable
    {
        public void Init(int capacity, Func<T> generator)
        {
            _stack = new Stack<T>(capacity);
            _capacity = capacity;

            T obj;
            for (int i = 0; i < capacity; i++)
            {
                obj = generator.Invoke();
				_stack.Push(obj);
            }
        }

        public T Alloc()
        {
            T ret;

            bool IsPop;
            IsPop = _stack.TryPop(out ret);
            if (!IsPop)
            {
                ret.OnAlloc();
                ++_capacity;
                ret.IsActive = true;
                return ret;
            }

            ret.IsActive = true;
            ret.OnAlloc();
            return ret;
        }

        public void Release(T obj)
        {
            if (obj == null)
            {
                CustomLogger.LogError("nullptr DeAllocation In MemoryPool");
                return;
            }

            if (obj.IsActive == false)
            {
                CustomLogger.LogError("Double Deallocation In MemoryPool");
                return;
            }
            obj.OnRelease();
            _stack.Push(obj);
        }

        private Func<T> _generator;
        private Stack<T> _stack;
        private int _capacity;
    }
}

