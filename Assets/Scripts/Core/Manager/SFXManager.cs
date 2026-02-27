using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Scripts.Core.DataStructure;
using Scripts.Core.Utils;

namespace Scripts.Core
{
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance;
        //AudioSource Pooling
        private ObjectPool<SFXEntity> _AudioSourcePool;

        [SerializeField]
        Transform _sfxParents;
        [SerializeField]
        SFXEntity _sfxPrefab;

        //SFX DataStore 
        private Dictionary<eSFXType, AudioClip> _AudioCache;
        private Dictionary<eSFXType, AsyncOperationHandle<AudioClip>> _Handles;

        private Dictionary<ulong, AsyncOperationHandle<IList<AudioClip>>> _BatchHandles;

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
            _sfxParents = gameObject.transform;

			_AudioCache = new Dictionary<eSFXType, AudioClip>();
            _BatchHandles = new Dictionary<ulong, AsyncOperationHandle<IList<AudioClip>>>();
            _Handles = new Dictionary<eSFXType, AsyncOperationHandle<AudioClip>>();

            //AudioSource는 어딜가든 초기화 x
            _AudioSourcePool = new ObjectPool<SFXEntity>();
            _AudioSourcePool.Init(24, _sfxParents, _sfxPrefab);
        }
        public AsyncOperationHandle<IList<AudioClip>> PreLoadSFX(ulong groupId, eSFXType[] clipsId)
        {
            if (clipsId.Length == 0)
            {
                CustomLogger.LogWarning("Clips Id List is Empty");
                return default;
            }
            //Clip들 로딩
            AsyncOperationHandle<IList<AudioClip>> ret;
            bool IsRequested = _BatchHandles.TryGetValue((ulong)groupId, out ret);
            if (IsRequested)
            {
                return ret;
            }

            LoadClipsAsync(groupId, clipsId);
            _BatchHandles.TryGetValue((ulong)groupId, out ret);
            return ret;
        }
        public void GetSFX(eSFXType Id, Vector3 pos, Quaternion rotation, Action<SFXEntity> OnLoaded)
        {
            AudioClip clip;
            SFXEntity ret;

            bool IsLoaded = _AudioCache.TryGetValue(Id, out clip);
            if (IsLoaded)
            {
                ret = _AudioSourcePool.Alloc(pos, rotation);
                ret.SetClip(clip);
                OnLoaded?.Invoke(ret);
                return;

            }
            //Load해야함.
            LoadClipAsync(Id, pos, rotation, OnLoaded);
            return;
        }
        public void DestroySFX(SFXEntity sfx)
        {
            _AudioSourcePool.Release(sfx);
        }
        public void Clear()
        {
            _AudioCache.Clear();
            foreach (var handle in _Handles)
            {
                Addressables.Release(handle);
            }
            foreach (var handle in _BatchHandles)
            {
                Addressables.Release(handle);
            }
        }
        private async void LoadClipAsync(eSFXType Id, Vector3 pos, Quaternion rotation, Action<SFXEntity> OnLoaded)
        {
            bool IsLoaded = _Handles.TryGetValue(Id, out var handle);
            AudioClip clip;

            if (IsLoaded)
            {
                CustomLogger.LogWarning("You requested to load SFX while the system was already in a loading state.");
                return;
            }
            else
            {
                handle = Addressables.LoadAssetAsync<AudioClip>(Id.ToString());
                _Handles.Add(Id, handle);
                clip = await handle.Task;
            }
            SFXEntity sfx;
            _AudioCache.Add(Id, clip);
            sfx = _AudioSourcePool.Alloc(pos, rotation);
            sfx.SetClip(clip);
            OnLoaded?.Invoke(sfx);
            return;
        }
        /// <summary>
        /// 필요한 SFX들 로드하는 함수
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="clipsId"></param>
        public async void LoadClipsAsync(ulong groupId, eSFXType[] clipsId)
        {
            //만약 여러번 요청한다면..
            bool IsLoaded = _BatchHandles.TryGetValue((ulong)groupId, out var handle);
            IList<AudioClip> clips;
            if (IsLoaded)
            {
                //이럴일은 없겠지만..있어서도 안되겠지만..
                CustomLogger.LogWarning("You requested to load SFX while the system was already in a loading state.");
                return;
            }
            else
            {
				IList<string> keys = Array.ConvertAll(clipsId, (id) => id.ToString());
				handle = Addressables.LoadAssetsAsync<AudioClip>(keys, (loaded) => { }, Addressables.MergeMode.Union);
                _BatchHandles.Add((ulong)groupId, handle);
                clips = await handle.Task;
            }

            if (clips.Count != clipsId.Length)
            {
				CustomLogger.LogWarning("You may Request Same SFX in one Batch. Please Check Your ExcelFile!\n");
				CustomLogger.LogError("The number of resources requested SFX to load is not the same as the number of id arrays.\n");
            }
            int i = 0;
            foreach (AudioClip clip in clips)
            {
                if (_AudioCache.ContainsKey(clipsId[i]) == false)
                {
					_AudioCache.Add(clipsId[i], clip);
				}
                ++i;
            }
        }
       
    }
}

