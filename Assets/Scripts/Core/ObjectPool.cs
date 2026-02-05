using Scripts.Core.inteface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core
{
    public class ObjectPool<T> where T : MonoBehaviour, IPoolable
    {
        public void Init(int capacity, T prefab)
        {
            _stack = new Stack<T>(capacity);
            _capacity = capacity;
            _prefab = prefab;

            T obj;

            for (int i = 0; i < capacity; i++)
            {
                obj = GameObject.Instantiate<T>(prefab, Vector3.zero, Quaternion.identity);
                obj.gameObject.SetActive(false);
                obj.IsActive = false;

                _stack.Push(obj);
            }
        }

        public void Init(int capacity, Transform parent, T prefab)
        {
            _stack = new Stack<T>(capacity);
            _capacity = capacity;
            _prefab = prefab;

            T obj;
            for (int i = 0; i < capacity; i++)
            {
                obj = GameObject.Instantiate<T>(prefab, parent);
                obj.gameObject.SetActive(false);
                obj.IsActive = false;

                _stack.Push(obj);
            }
        }

        public T Alloc(Vector3 position, Quaternion rotate)
        {
            T ret = null;

            bool IsEmpty;
            IsEmpty = _stack.TryPop(out ret);
            if (!IsEmpty)
            {
                ret = GameObject.Instantiate(_prefab, position, rotate);
                ret.OnAlloc();
                ++_capacity;
#if UNITY_EDITOR
                ret.IsActive = true;
#endif
                return ret;
            }

            ret.gameObject.transform.position = position;
            ret.gameObject.transform.rotation = rotate;
#if UNITY_EDITOR
            ret.IsActive = true;
#endif
            ret.OnAlloc();
            return ret;
        }

        public void Release(T obj)
        {
#if UNITY_EDITOR
            if (obj == null)
            {
                Debug.Log("nullptr DeAllocation");
                UnityEngine.Debug.Break();
                return;
            }
#endif
#if UNITY_EDITOR
            if (obj.IsActive == false)
            {

                Debug.Log("Double Deallocation");
                UnityEngine.Debug.Break();
                return;
            }
#endif
            obj.gameObject.SetActive(false);
#if UNITY_EDITOR
            obj.IsActive = false;
#endif
            obj.OnRelease();
            _stack.Push(obj);
        }

        private T _prefab;
        private Stack<T> _stack;
        private int _capacity;
    }
}

