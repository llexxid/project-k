using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        private Dictionary<ulong, AudioClip> _AudioCache;
        private Dictionary<ulong, AsyncOperationHandle<AudioClip>> _Handles;
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
            _AudioCache = new Dictionary<ulong, AudioClip>();
            _BatchHandles = new Dictionary<ulong, AsyncOperationHandle<IList<AudioClip>>>();
            _Handles = new Dictionary<ulong, AsyncOperationHandle<AudioClip>>();

            _AudioSourcePool = new ObjectPool<SFXEntity>();
            _AudioSourcePool.Init(24, _sfxParents, _sfxPrefab);
        }
        public void OnEnterScene(ulong groupId, ulong[] clipsId)
        {
            //Clip들 로딩
            Clear();
            LoadClipsAsync(groupId, clipsId);
        }
        public void GetSFX(ulong Id, Vector3 pos, Quaternion rotation, Action<SFXEntity> OnLoaded)
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
            LoadClipAsync(Id, pos,rotation, OnLoaded);
            return;
        }
        public void DestroySFX(SFXEntity sfx)
        {
            _AudioSourcePool.Release(sfx);
        }
        private void Clear()
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
        private async void LoadClipAsync(ulong Id, Vector3 pos, Quaternion rotation, Action<SFXEntity> OnLoaded)
        {
            bool IsLoaded = _Handles.TryGetValue(Id, out var handle);
            AudioClip clip;

            if (IsLoaded)
            {
                CustomLogger.LogWarning("You requested to load SFX while the system was already in a loading state.");
                clip = await handle.Task;
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
        private async void LoadClipsAsync(ulong groupId, ulong[] clipsId)
        {
            //만약 여러번 요청한다면..
            bool IsLoaded = _BatchHandles.TryGetValue(groupId, out var handle);
            IList<AudioClip> clips;

            if (IsLoaded)
            {
                //이럴일은 없겠지만..있어서도 안되겠지만..
                CustomLogger.LogWarning("You requested to load SFX while the system was already in a loading state.");
                clips = await handle.Task;
            }
            else 
            {
                handle = Addressables.LoadAssetsAsync<AudioClip>(groupId.ToString(), (loaded) => { });
                _BatchHandles.Add(groupId, handle);
                clips = await handle.Task;
            }

            if (clips.Count != clipsId.Length)
            {
                CustomLogger.LogError("The number of resources requested SFX to load is not the same as the number of id arrays.");
            }
            int i = 0;
            foreach (AudioClip clip in clips)
            {
                _AudioCache.Add(clipsId[i], clip);
                ++i;
            }
        }

    }
}

