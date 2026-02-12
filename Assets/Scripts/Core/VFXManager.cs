using Cysharp.Threading.Tasks.Triggers;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEngine.Networking.UnityWebRequest;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

namespace Scripts.Core
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance;

        Transform _vfxParents;

        private Dictionary<eVFXType, VFXEntity> _effectCache;
        private Dictionary<eVFXType, ObjectPool<VFXEntity>> _VFXPools;

        private Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>> _BatchHandles;
        private Dictionary<eVFXType, AsyncOperationHandle<GameObject>> _Handles;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Instance.Init();
                DontDestroyOnLoad(this);
                return;
            }
            Destroy(this);
            return;
        }
        private void Init()
        {
            _effectCache = new Dictionary<eVFXType, VFXEntity>();
            _VFXPools = new Dictionary<eVFXType, ObjectPool<VFXEntity>>();
            _BatchHandles = new Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>>();
            _Handles = new Dictionary<eVFXType, AsyncOperationHandle<GameObject>>();
        }
        /// <summary>
        /// 씬에 진압할 때, VFXManager 리소스 정리함수입니다.
        /// </summary>
        public void OnEnterScene()
        {
            GameObject obj = new GameObject("VFX_Root");
            _vfxParents = obj.transform;
            Clear();
        }

        /// <summary>
        /// VFX cache를 데우는 비동기 함수입니다. 로딩에서 통제합니다. 
        /// </summary>
        public AsyncOperationHandle<IList<GameObject>> PreLoadVFX(eStage groupId, eVFXType[] IdList)
        {
            AsyncOperationHandle<IList<GameObject>> handle;
            bool IsLoading = _BatchHandles.TryGetValue((ulong)groupId, out handle);
            if (IsLoading)
            {
                CustomLogger.LogWarning("You requested to load VFX while the system was already in a loading state.");
                return handle;
            }
            //요청한 뒤, Handle반환
            RequestAsyncLoadAssets(groupId, IdList);
            _BatchHandles.TryGetValue((ulong)groupId, out handle);
            return handle;
        }
        /// <summary>
        /// 지정된 리소스ID로 효과를 연출하는 함수입니다. 
        /// 로딩시, Play하는 함수를 Callback으로 주면 됩니다.
        /// </summary>
        public void GetVFX(eVFXType id, Vector3 pos, Quaternion rotation, Action<VFXEntity> OnLoaded)
        {
            VFXEntity ret;
            bool IsCached;
            IsCached = TryLoadFromCache(id, pos, rotation, out ret);

            if (IsCached)
            {
                
                ret.SetId(id);
                ret.gameObject.SetActive(true);
                OnLoaded.Invoke(ret);
                return;
            }
            // Load하는걸 허용해준다!
            // Load하는 그 딜레이를 허용해줌. 혹은, Load되었을 때, 실행할 Callback을 던져줘야함. 
            RequestAsyncLoadAsset(id, pos, rotation, OnLoaded);
            return;
        }
        public void DestroyEffect(eVFXType id, VFXEntity vfx)
        {
            if (CheckPoolingEffect(id))
            {
                _VFXPools.TryGetValue(id, out ObjectPool<VFXEntity> pool);
                pool.Release(vfx);
                return;
            }

            //일회성 이펙트였다면..
            Destroy(vfx.gameObject);
            unloadSingleVFX(id);
            _effectCache.Remove(id);
            return;
        }
        public void unloadVFXBatch(ulong groupId)
        {
            bool flag;
            flag = _BatchHandles.TryGetValue(groupId, out var handle);
            if (flag)
            {
                Addressables.Release(handle);
            }
        }
        public void unloadSingleVFX(eVFXType id)
        {
            bool flag;
            flag = _Handles.TryGetValue(id, out var handle);
            if (flag)
            {
                Addressables.Release(handle);
            }
        }       
        
        private async void RequestAsyncLoadAssets(eStage groupId, eVFXType[] IdList)
        {
            IList<GameObject> result;
            AsyncOperationHandle<IList<GameObject>> handle;
            bool IsRequested = _BatchHandles.TryGetValue((ulong)groupId, out handle);
            if (IsRequested)
            {
                result = await handle.Task;
            }
            else 
            {
                 handle = Addressables.LoadAssetsAsync<GameObject>(groupId.ToString(), (loaded) => { });
                _BatchHandles.Add((ulong)groupId, handle);
                result = await handle.Task;
            }
            //완료가 되었을 때 하는 로직
            if (result.Count != IdList.Length)
            {
                CustomLogger.LogError("The number of resources requested to load is not the same as the number of id arrays. check IdList[]");
            }

            VFXEntity resource;
            for (int i = 0; i < result.Count; i++)
            {
                resource = result[i].gameObject.GetComponent<VFXEntity>();
                OnLoadAsset(IdList[i], resource);
            }
        }
        private bool TryLoadFromCache(eVFXType id, Vector3 pos, Quaternion rotation, out VFXEntity ret)
        {
            VFXEntity vfx;
            bool IsPrefabLoaded = _effectCache.TryGetValue(id, out vfx);
            if (!IsPrefabLoaded)
            {
                ret = default;
                return false;
            }
            InstantiateEffect(id, vfx, pos, rotation, out ret);
            return true;
        }
        private async void RequestAsyncLoadAsset(eVFXType id, Vector3 pos, Quaternion rotation, Action<VFXEntity> OnLoaded)
        {
            GameObject loadedObj;
            AsyncOperationHandle<GameObject> handle;
            VFXEntity resourceVfx;
            //Load중에 또 요청하는 경우
            bool IsLoading = _Handles.TryGetValue(id, out handle);
            if (IsLoading)
            {
                CustomLogger.LogWarning("You requested to load while the system was already in a loading state.");
                return;
            }
            //처음 Load하는 경우
            else
            {
                handle = Addressables.LoadAssetAsync<GameObject>(id.ToString());
                _Handles.Add(id, handle);
                loadedObj = await handle.Task; // nonBlocking, 아래를 실행하지 않고 흐름을 넘김.
            }
            //Callback으로 등록
            resourceVfx = loadedObj.GetComponent<VFXEntity>();
            OnLoadAsset(id, resourceVfx);
            InstantiateEffect(id, resourceVfx, pos, rotation, out VFXEntity instance);
            OnLoaded?.Invoke(instance);
            instance.SetId(id);
            return;
        }
        private bool CheckPoolingEffect(eVFXType id)
        {
            if (((ulong)id & (ulong)AssetIdMask.VFX_NotPooling_MASK) == (ulong)AssetIdMask.VFX_NotPooling_MASK)
            {
                return false;
            }
            return true;
        }
        private void OnLoadAsset(eVFXType id, VFXEntity obj)
        {
            _effectCache.Add(id, obj);
            //만약 pooling effect면, pooling해주기.
            if (CheckPoolingEffect(id))
            {
                ObjectPool<VFXEntity> objectpool = new ObjectPool<VFXEntity>();
                objectpool.Init((int)DEFAULT_VALUE.PoolingSize, _vfxParents, obj);
                _VFXPools.Add(id, objectpool);
            }
        }
        private void Clear()
        {
            _effectCache.Clear();
            _VFXPools.Clear();
            foreach (var item in _BatchHandles)
            {
                Addressables.Release(item.Value);
            }
            foreach (var item in _Handles)
            {
                Addressables.Release(item.Value);
            }
            _BatchHandles.Clear();
            _Handles.Clear();
        }
        private void InstantiateEffect(eVFXType id, VFXEntity resource, Vector3 pos, Quaternion rotation, out VFXEntity vfx)
        {
            if (CheckPoolingEffect(id) == false)
            {
                vfx = GameObject.Instantiate<VFXEntity>(resource, pos, rotation);
            }
            else
            {
                vfx = _VFXPools[id].Alloc(pos, rotation);
            }
            return;
        }
    }
}

