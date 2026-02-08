using System;
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
        private GameObject _vfxRootGO;

        private Dictionary<ulong, VFXEntity> _effectCache;
        private Dictionary<ulong, ObjectPool<VFXEntity>> _vfxPools;

        private Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>> _batchHandles;
        private Dictionary<ulong, AsyncOperationHandle<GameObject>> _handles;

        // AssetId의 마스크와 동기화(복잡도 낮추려고 const로 유지)
        private const ulong VFX_POOLING_MASK = 0x1000000000000000;
        private const ulong VFX_NOTPOOLING_MASK = 0x1100000000000000;

        private bool _initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitIfNeeded();
        }

        private void InitIfNeeded()
        {
            if (_initialized) return;
            _initialized = true;

            _effectCache = new Dictionary<ulong, VFXEntity>();
            _vfxPools = new Dictionary<ulong, ObjectPool<VFXEntity>>();
            _batchHandles = new Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>>();
            _handles = new Dictionary<ulong, AsyncOperationHandle<GameObject>>();
        }

        /// <summary>
        /// 씬 진입 시 호출. VFX 루트 생성 + 캐시/풀 정리 + 필요한 리소스 워밍업
        /// </summary>
        public void OnEnterScene(GroupId groupId, ulong[] idList)
        {
            InitIfNeeded();

            // 이전 루트가 남아있으면 정리(씬 전환 시 대부분 파괴되지만, 안전하게 처리)
            if (_vfxRootGO != null)
            {
                Destroy(_vfxRootGO);
                _vfxRootGO = null;
                _vfxParents = null;
            }

            _vfxRootGO = new GameObject("VFX_Root");
            _vfxParents = _vfxRootGO.transform;

            Clear();
            WarmingUpResourcesAsync(groupId, idList);
        }

        /// <summary>
        /// 지정한 VFX id로 이펙트 인스턴스를 요청.
        /// 캐시에 있으면 즉시 생성, 없으면 Addressables 로드 후 생성.
        /// </summary>
        public void GetVFX(ulong id, Vector3 pos, Quaternion rotation, Action<VFXEntity> onLoaded)
        {
            InitIfNeeded();

            if (TryLoadFromCache(id, pos, rotation, out VFXEntity cachedInstance))
            {
                cachedInstance.SetId(id);
                cachedInstance.gameObject.SetActive(true);
                onLoaded?.Invoke(cachedInstance);
                return;
            }

            LoadNotCachedResourceAsync(id, pos, rotation, onLoaded);
        }

        public void DestroyEffect(ulong id, VFXEntity vfx)
        {
            if (vfx == null) return;

            if (CheckPoolingEffect(id))
            {
                if (_vfxPools != null && _vfxPools.TryGetValue(id, out ObjectPool<VFXEntity> pool) && pool != null)
                {
                    pool.Release(vfx);
                    return;
                }

                // 풀링 대상인데 풀이 없다면 안전장치(즉시 파괴)
                Destroy(vfx.gameObject);
                return;
            }

            // 1회성 이펙트라면
            Destroy(vfx.gameObject);
            UnloadSingleVFX(id);
            _effectCache?.Remove(id);
        }

        public void UnloadVFXBatch(ulong groupId)
        {
            if (_batchHandles != null && _batchHandles.TryGetValue(groupId, out var handle))
            {
                Addressables.Release(handle);
                _batchHandles.Remove(groupId);
            }
        }

        public void UnloadSingleVFX(ulong id)
        {
            if (_handles != null && _handles.TryGetValue(id, out var handle))
            {
                Addressables.Release(handle);
                _handles.Remove(id);
            }
        }

        /// <summary>
        /// VFX cache를 워밍업(일괄 로드)합니다.
        /// </summary>
        public async void WarmingUpResourcesAsync(GroupId groupId, ulong[] idList)
        {
            InitIfNeeded();

            if (idList == null)
            {
                CustomLogger.LogError("[VFXManager] idList is null.");
                return;
            }

            try
            {
                ulong key = (ulong)groupId;

                if (!_batchHandles.TryGetValue(key, out var handle))
                {
                    handle = Addressables.LoadAssetsAsync<GameObject>(groupId.ToString(), _ => { });
                    _batchHandles.Add(key, handle);
                }
                else
                {
                    CustomLogger.LogWarning("You requested to load VFX while the system was already in a loading state.");
                }

                IList<GameObject> result = await handle.Task;

                if (result == null)
                {
                    CustomLogger.LogError($"[VFXManager] Failed to load VFX batch. groupId={groupId}");
                    return;
                }

                if (result.Count != idList.Length)
                {
                    CustomLogger.LogError("The number of resources requested to load is not the same as the number of id arrays. check idList[]");
                }

                int count = Mathf.Min(result.Count, idList.Length);
                for (int i = 0; i < count; i++)
                {
                    VFXEntity resource = result[i] != null ? result[i].GetComponent<VFXEntity>() : null;
                    if (resource == null)
                    {
                        CustomLogger.LogError($"[VFXManager] VFXEntity component missing. index={i}");
                        continue;
                    }

                    OnLoadAsset(idList[i], resource);
                }
            }
            catch (Exception e)
            {
                CustomLogger.LogError($"[VFXManager] WarmingUpResourcesAsync exception: {e}");
            }
        }

        private bool TryLoadFromCache(ulong id, Vector3 pos, Quaternion rotation, out VFXEntity ret)
        {
            if (_effectCache != null && _effectCache.TryGetValue(id, out VFXEntity prefab) && prefab != null)
            {
                InstantiateEffect(id, prefab, pos, rotation, out ret);
                return true;
            }

            ret = default;
            return false;
        }

        private async void LoadNotCachedResourceAsync(ulong id, Vector3 pos, Quaternion rotation, Action<VFXEntity> onLoaded)
        {
            InitIfNeeded();

            try
            {
                if (!_handles.TryGetValue(id, out var handle))
                {
                    handle = Addressables.LoadAssetAsync<GameObject>(id.ToString());
                    _handles.Add(id, handle);
                }
                else
                {
                    CustomLogger.LogWarning("You requested to load while the system was already in a loading state.");
                }

                GameObject loadedObj = await handle.Task;
                if (loadedObj == null)
                {
                    CustomLogger.LogError($"[VFXManager] Failed to load VFX. id={id}");
                    return;
                }

                VFXEntity resourceVfx = loadedObj.GetComponent<VFXEntity>();
                if (resourceVfx == null)
                {
                    CustomLogger.LogError($"[VFXManager] Loaded prefab has no VFXEntity. id={id}");
                    return;
                }

                OnLoadAsset(id, resourceVfx);

                InstantiateEffect(id, resourceVfx, pos, rotation, out VFXEntity instance);
                instance.SetId(id);
                onLoaded?.Invoke(instance);
            }
            catch (Exception e)
            {
                CustomLogger.LogError($"[VFXManager] LoadNotCachedResourceAsync exception: {e}");
            }
        }

        private bool CheckPoolingEffect(ulong id)
        {
            // 명시적으로 NotPooling 마스크가 있으면 non-pooling
            if ((id & VFX_NOTPOOLING_MASK) == VFX_NOTPOOLING_MASK)
                return false;

            // Pooling 마스크가 있으면 pooling
            if ((id & VFX_POOLING_MASK) == VFX_POOLING_MASK)
                return true;

            // 기존 구현(항상 true)을 최대한 유지하기 위해 fallback은 true
            return true;
        }

        private void OnLoadAsset(ulong id, VFXEntity prefab)
        {
            _effectCache ??= new Dictionary<ulong, VFXEntity>();

            // 중복 로드 방지
            _effectCache[id] = prefab;

            if (CheckPoolingEffect(id))
            {
                _vfxPools ??= new Dictionary<ulong, ObjectPool<VFXEntity>>();

                if (!_vfxPools.ContainsKey(id))
                {
                    ObjectPool<VFXEntity> pool = new ObjectPool<VFXEntity>();
                    pool.Init(30, _vfxParents, prefab);
                    _vfxPools.Add(id, pool);
                }
            }
        }

        private void Clear()
        {
            _effectCache?.Clear();
            _vfxPools?.Clear();

            if (_batchHandles != null)
            {
                foreach (var item in _batchHandles)
                {
                    Addressables.Release(item.Value);
                }
                _batchHandles.Clear();
            }

            if (_handles != null)
            {
                foreach (var item in _handles)
                {
                    Addressables.Release(item.Value);
                }
                _handles.Clear();
            }
        }

        private void InstantiateEffect(ulong id, VFXEntity prefab, Vector3 pos, Quaternion rotation, out VFXEntity vfx)
        {
            if (CheckPoolingEffect(id) == false)
            {
                vfx = Instantiate(prefab, pos, rotation);
            }
            else
            {
                if (_vfxPools != null && _vfxPools.TryGetValue(id, out var pool) && pool != null)
                {
                    vfx = pool.Alloc(pos, rotation);
                }
                else
                {
                    // 풀링 대상인데 풀이 없다면 안전장치로 Instantiate
                    vfx = Instantiate(prefab, pos, rotation);
                }
            }
        }
    }
}
