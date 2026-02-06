using Cysharp.Threading.Tasks.Triggers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEditor.PlayerSettings;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

namespace Scripts.Core
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance;

        Transform _vfxParents;

        private Dictionary<ulong, VFXEntity> _effectCache;
        private Dictionary<ulong, ObjectPool<VFXEntity>> _VFXPools;

        //LoadAssets한 핸들. 보통 Warming Up함.
        private Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>> _BatchHandles;
        //Warm Up되지 않은 Effect들을 불러온 경우, 해당 Handle을 들고 있어야함. 
        private Dictionary<ulong, AsyncOperationHandle<GameObject>> _Handles;

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
        /// <summary>
        /// 씬에 진압할 때, VFXManager에서 부르는 함수입니다.
        /// </summary>
        public void OnEnterScene(ulong groupId, ulong[] idList)
        {
            //Parent가 될 GameObject를 만들어야함.
            if (_vfxParents == null)
            {
                GameObject obj = new GameObject("VFX_Root");
                _vfxParents = obj.transform;
            }
            Clear();
            //ResoucreLoad
            LoadResourcesAsync(groupId, idList);
        }

        public VFXEntity GetVFX(ulong id, Vector3 pos, Quaternion rotation)
        {
            VFXEntity ret;
            bool IsCached;
            IsCached = TryLoadFromCache(id, pos, rotation, out ret);
            if (!IsCached)
            {
                LoadResourceAysnc(id);
                //Load다 됐을 수도, 안됐을 수도 있다. 한번 시도함.
                TryLoadFromCache(id, pos, rotation, out ret);
            }
            ret.SetId(id);
            ret.gameObject.SetActive(true);
            return ret;
        }

        public void DestroyEffect(ulong id, VFXEntity vfx)
        {
            if (CheckPoolingEffect(id))
            {
                _VFXPools.TryGetValue(id, out ObjectPool<VFXEntity> pool);
                pool.Release(vfx);
                return;
            }
            //일회성 이펙트였다면..
            unloadSingleVFX(id);
            _effectCache.Remove(id);
            Destroy(vfx);
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
        public void unloadSingleVFX(ulong id)
        {
            bool flag;
            flag = _Handles.TryGetValue(id, out var handle);
            if (flag)
            {
                Addressables.Release(handle);
            }
        }

        private bool TryLoadFromCache(ulong id, Vector3 pos, Quaternion rotation, out VFXEntity ret)
        {
            VFXEntity vfx;
            bool IsPrefabLoaded = _effectCache.TryGetValue(id, out vfx);
            if (!IsPrefabLoaded)
            {
                ret = null;
                return false;
            }

            if (CheckPoolingEffect(id))
            {
                ret = GameObject.Instantiate<VFXEntity>(vfx, pos, rotation);
                return true;
            }

            ret = _VFXPools[id].Alloc(pos, rotation);
            return true;
        }
        private async void LoadResourcesAsync(ulong groupId, ulong[] IdList)
        {
            bool IsLoading = _BatchHandles.TryGetValue(groupId, out var handle);
            IList<GameObject> result;
            if (IsLoading)
            {
                Debug.Log("VFX들을 Load 처리 중 또 요청이 되었습니다");
                result = await handle.Task;
            } 
            else
            {
                handle = Addressables.LoadAssetsAsync<GameObject>(groupId.ToString(), (loaded) => { });
                _BatchHandles.Add(groupId, handle);
                result = await handle.Task;
            }

            if (result.Count != IdList.Length)
            {
                Debug.Log("VFX의 키 배열과 Addressable에 등록된 크기가 다릅니다.");
                UnityEngine.Debug.Break();
            }
            VFXEntity vfx;
            for (int i = 0; i < result.Count; i++)
            {
                vfx = gameObject.GetComponent<VFXEntity>();
                OnLoadAsset(IdList[i], vfx);
            }
        }
        private async void LoadResourceAysnc(ulong id)
        {
            GameObject loadedObj;
            AsyncOperationHandle<GameObject> handle;
            VFXEntity vfx;
            //Load중에 또 요청하는 경우
            bool IsLoading = _Handles.TryGetValue(id, out handle);
            if (IsLoading)
            {
                Debug.Log("VFX들을 Load 처리 중 또 요청이 되었습니다");
                loadedObj = await handle.Task;
            }
            //처음 Load하는 경우
            else
            {
                handle = Addressables.LoadAssetAsync<GameObject>(id.ToString());
                _Handles.Add(id, handle);
                loadedObj = await handle.Task; // nonBlocking, 아래를 실행하지 않고 흐름을 넘김.
            }
            // 다 로딩이 됬다는 가정하에,
            vfx = loadedObj.GetComponent<VFXEntity>();
            OnLoadAsset(id, vfx);
        }
        private bool CheckPoolingEffect(ulong id)
        {
            ulong PoolingMASK = 0x1000000000000000;
            if ((id & PoolingMASK) != 0)
            {
                return true;
            }
            return false;
        }
        private void OnLoadAsset(ulong id, VFXEntity obj)
        {
            _effectCache.Add(id, obj);
            //만약 pooling effect면, pooling해주기.
            if (CheckPoolingEffect(id))
            {
                ObjectPool<VFXEntity> objectpool = new ObjectPool<VFXEntity>();
                objectpool.Init(30, _vfxParents, obj);
                _VFXPools.Add(id, objectpool);
            }
        }
    }
}

