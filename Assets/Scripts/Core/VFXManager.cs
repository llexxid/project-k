using System.Collections;
using System.Collections.Generic;
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
        static VFXManager Instance;

        [SerializeField]
        Transform _vfxParents;

        private Dictionary<ulong, VFXEntity> _effectCache;
        private Dictionary<ulong, AsyncOperationHandle<GameObject>> _Handles;
        private Dictionary<ulong, ObjectPool<VFXEntity>> _VFXPools;
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

        //풀링이 되어야하는 VFXId 10000000000....
        //풀링이 되지 않아야하는 VFXId 0.......
        
        private void Init()
        {

        }

        public void WarmUpCache(ulong[] id)
        {

        }

        public VFXEntity Active(ulong id, Vector3 pos, Quaternion rotation)
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
            return ret;
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

        private async void LoadResourceAysnc(ulong id)
        {
            GameObject loadedObj;
            AsyncOperationHandle<GameObject> handle;
            VFXEntity vfx;
            //Load중에 또 요청하는 경우
            bool IsLoading = _Handles.TryGetValue(id, out handle);
            if (IsLoading)
            {
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
                objectpool.Init(30, obj);
                _VFXPools.Add(id, objectpool);
            }
        }

        public void unloadVFX(ulong id)
        {

        }


        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

