using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Scripts.Core
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        private Transform _vfxParents;

        private Dictionary<ulong, VFXEntity> _effectCache;
        private Dictionary<ulong, ObjectPool<VFXEntity>> _vfxPools;

        // Warming up handles (batch)
        private Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>> _warmUpHandles;
        // Single load handles
        private Dictionary<ulong, AsyncOperationHandle<GameObject>> _handles;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Init();
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Clear();
        }

        private void Init()
        {
            _effectCache ??= new Dictionary<ulong, VFXEntity>();
            _vfxPools ??= new Dictionary<ulong, ObjectPool<VFXEntity>>();
            _warmUpHandles ??= new Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>>();
            _handles ??= new Dictionary<ulong, AsyncOperationHandle<GameObject>>();
        }

        private void EnsureParents()
        {
            if (_vfxParents != null) return;

            GameObject obj = new GameObject("VFX_Root");
            _vfxParents = obj.transform;
            DontDestroyOnLoad(obj);
        }

        private void Clear()
        {
            _effectCache?.Clear();
            _vfxPools?.Clear();

            if (_warmUpHandles != null)
            {
                foreach (var kv in _warmUpHandles)
                {
                    if (kv.Value.IsValid())
                        Addressables.Release(kv.Value);
                }
                _warmUpHandles.Clear();
            }

            if (_handles != null)
            {
                foreach (var kv in _handles)
                {
                    if (kv.Value.IsValid())
                        Addressables.Release(kv.Value);
                }
                _handles.Clear();
            }
        }

        /// <summary>
        /// 씬 진입 시, VFX 리소스 워밍업(배치) 등 준비를 하고 싶을 때 호출하는 진입점
        /// </summary>
        public void OnEnterScene(ulong groupId, ulong[] idList)
        {
            EnsureParents();
            Clear();
            LoadResourcesAsync(groupId, idList);
        }

        public VFXEntity GetVFX(ulong id, Vector3 pos, Quaternion rotation)
        {
            EnsureParents();

            if (!TryLoadFromCache(id, pos, rotation, out var ret))
            {
                LoadResourceAsync(id);
                TryLoadFromCache(id, pos, rotation, out ret);
            }

            if (ret == null)
            {
                Debug.LogWarning($"[VFXManager] VFX not ready yet. id={id}");
                return null;
            }

            ret.SetId(id);
            ret.gameObject.SetActive(true);
            return ret;
        }

        public void DestroyEffect(ulong id, VFXEntity vfx)
        {
            if (vfx == null) return;

            if (CheckPoolingEffect(id) && _vfxPools != null && _vfxPools.TryGetValue(id, out var pool) && pool != null)
            {
                pool.Release(vfx);
                return;
            }

            unloadSingleVFX(id);
            _effectCache?.Remove(id);
            Destroy(vfx.gameObject);
        }

        public void unloadVFXBatch(ulong groupId)
        {
            if (_warmUpHandles == null) return;

            if (_warmUpHandles.TryGetValue(groupId, out var handle) && handle.IsValid())
            {
                Addressables.Release(handle);
                _warmUpHandles.Remove(groupId);
            }
        }

        public void unloadSingleVFX(ulong id)
        {
            if (_handles == null) return;

            if (_handles.TryGetValue(id, out var handle) && handle.IsValid())
            {
                Addressables.Release(handle);
                _handles.Remove(id);
            }
        }

        private bool TryLoadFromCache(ulong id, Vector3 pos, Quaternion rotation, out VFXEntity ret)
        {
            ret = null;

            if (_effectCache == null) Init();
            if (!_effectCache.TryGetValue(id, out var prefab) || prefab == null)
                return false;

            if (CheckPoolingEffect(id) && _vfxPools != null && _vfxPools.TryGetValue(id, out var pool) && pool != null)
            {
                ret = pool.Alloc(pos, rotation);
                return true;
            }

            ret = Instantiate(prefab, pos, rotation, _vfxParents);
            return true;
        }

        private async void LoadResourcesAsync(ulong groupId, ulong[] idList)
        {
            if (_warmUpHandles == null) Init();

            var handle = Addressables.LoadAssetsAsync<GameObject>(groupId.ToString(), _ => { });
            _warmUpHandles[groupId] = handle;

            IList<GameObject> result = await handle.Task;
            if (result == null) return;

            for (int i = 0; i < result.Count && i < idList.Length; i++)
            {
                var go = result[i];
                if (go == null) continue;

                var vfx = go.GetComponent<VFXEntity>();
                if (vfx == null)
                {
                    Debug.LogWarning($"[VFXManager] Loaded object has no VFXEntity. key={idList[i]} name={go.name}");
                    continue;
                }

                OnLoadAsset(idList[i], vfx);
            }
        }

        private async void LoadResourceAsync(ulong id)
        {
            if (_handles == null) Init();

            AsyncOperationHandle<GameObject> handle;
            GameObject loadedObj;

            if (_handles.TryGetValue(id, out handle))
            {
                loadedObj = await handle.Task;
            }
            else
            {
                handle = Addressables.LoadAssetAsync<GameObject>(id.ToString());
                _handles[id] = handle;
                loadedObj = await handle.Task;
            }

            if (loadedObj == null) return;

            var vfx = loadedObj.GetComponent<VFXEntity>();
            if (vfx == null)
            {
                Debug.LogWarning($"[VFXManager] Loaded object has no VFXEntity. id={id} name={loadedObj.name}");
                return;
            }

            OnLoadAsset(id, vfx);
        }

        private bool CheckPoolingEffect(ulong id)
        {
            const ulong PoolingMASK = 0x1000000000000000;
            return (id & PoolingMASK) != 0;
        }

        private void OnLoadAsset(ulong id, VFXEntity obj)
        {
            if (_effectCache == null) Init();
            if (obj == null) return;

            _effectCache[id] = obj;

            if (CheckPoolingEffect(id))
            {
                if (_vfxPools == null) Init();

                if (!_vfxPools.ContainsKey(id))
                {
                    var pool = new ObjectPool<VFXEntity>();
                    pool.Init(30, _vfxParents, obj);
                    _vfxPools.Add(id, pool);
                }
            }
        }
    }
}
