using Scripts.Core.inteface;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core
{
    // 간단 Stack 기반 오브젝트 풀
    public class ObjectPool<T> where T : MonoBehaviour, IPoolable
    {
        public void Init(int capacity, T prefab)
        {
            Init(capacity, null, prefab);
        }

        public void Init(int capacity, Transform parent, T prefab)
        {
            _stack = new Stack<T>(capacity);
            _capacity = Mathf.Max(0, capacity);
            _prefab = prefab;
            _parent = parent;

            if (_prefab == null)
            {
                CustomLogger.LogError("ObjectPool prefab is null.");
                return;
            }

            for (int i = 0; i < _capacity; i++)
            {
                T obj = (_parent == null)
                    ? Object.Instantiate(_prefab, Vector3.zero, Quaternion.identity)
                    : Object.Instantiate(_prefab, _parent);

                obj.gameObject.SetActive(false);
                obj.IsActive = false;
                _stack.Push(obj);
            }
        }

        public T Alloc(Vector3 position, Quaternion rotation)
        {
            if (_prefab == null)
            {
                CustomLogger.LogError("ObjectPool is not initialized (prefab is null).");
                return null;
            }

            if (_stack != null && _stack.TryPop(out T ret) && ret != null)
            {
                ret.transform.position = position;
                ret.transform.rotation = rotation;
                ret.IsActive = true;
                ret.OnAlloc();
                return ret;
            }

            // 풀에 남은게 없으면 확장 생성
            T created = (_parent == null)
                ? Object.Instantiate(_prefab, position, rotation)
                : Object.Instantiate(_prefab, _parent);

            created.transform.position = position;
            created.transform.rotation = rotation;
            created.IsActive = true;
            created.OnAlloc();
            ++_capacity;
            return created;
        }

        public void Release(T obj)
        {
            if (obj == null)
            {
                CustomLogger.LogError("nullptr DeAllocation In ObjectPool");
                return;
            }

            if (obj.IsActive == false)
            {
                CustomLogger.LogError("Double Deallocation In ObjectPool");
                return;
            }

            obj.gameObject.SetActive(false);
            obj.IsActive = false;
            obj.OnRelease();

            _stack ??= new Stack<T>();
            _stack.Push(obj);
        }

        private T _prefab;
        private Stack<T> _stack;
        private int _capacity;
        private Transform _parent;
    }
}
