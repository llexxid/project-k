using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Scripts.Core
{
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        // AudioSource Pooling
        private ObjectPool<SFXEntity> _audioSourcePool;

        [Header("Pool Setup")]
        [SerializeField] private Transform _sfxParents;
        [SerializeField] private SFXEntity _sfxPrefab;
        [SerializeField] private int initialPoolSize = 24;

        // SFX DataStore
        private Dictionary<ulong, AudioClip> _audioCache;
        private Dictionary<ulong, AsyncOperationHandle<AudioClip>> _handles;
        private Dictionary<ulong, AsyncOperationHandle<IList<AudioClip>>> _batchHandles;

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

            _audioCache = new Dictionary<ulong, AudioClip>();
            _handles = new Dictionary<ulong, AsyncOperationHandle<AudioClip>>();
            _batchHandles = new Dictionary<ulong, AsyncOperationHandle<IList<AudioClip>>>();

            if (_sfxParents == null)
            {
                var root = new GameObject("SFX_Root");
                root.transform.SetParent(transform, false);
                _sfxParents = root.transform;
            }

            if (_sfxPrefab == null)
            {
                CustomLogger.LogError("[SFXManager] _sfxPrefab is not assigned.");
                return;
            }

            _audioSourcePool = new ObjectPool<SFXEntity>();
            _audioSourcePool.Init(Mathf.Max(0, initialPoolSize), _sfxParents, _sfxPrefab);
        }

        /// <summary>
        /// 씬 진입 시 필요한 SFX 일괄 로드 (기존 캐시/핸들 정리 후 로드)
        /// groupId는 Addressables Label(or key)로 사용
        /// clipsId는 result 순서에 대응하는 개별 ID 목록
        /// </summary>
        public void OnEnterScene(ulong groupId, ulong[] clipsId)
        {
            InitIfNeeded();
            Clear();
            LoadClipsAsync(groupId, clipsId);
        }

        public void GetSFX(ulong id, Vector3 pos, Quaternion rotation, Action<SFXEntity> onLoaded)
        {
            InitIfNeeded();

            if (_audioCache != null && _audioCache.TryGetValue(id, out AudioClip cached) && cached != null)
            {
                var sfx = _audioSourcePool.Alloc(pos, rotation);
                sfx.SetClip(cached);
                onLoaded?.Invoke(sfx);
                return;
            }

            LoadClipAsync(id, pos, rotation, onLoaded);
        }

        public void DestroySFX(SFXEntity sfx)
        {
            if (_audioSourcePool == null) return;
            _audioSourcePool.Release(sfx);
        }

        private void Clear()
        {
            if (_audioCache != null) _audioCache.Clear();

            if (_handles != null)
            {
                foreach (var kv in _handles)
                {
                    Addressables.Release(kv.Value);
                }
                _handles.Clear();
            }

            if (_batchHandles != null)
            {
                foreach (var kv in _batchHandles)
                {
                    Addressables.Release(kv.Value);
                }
                _batchHandles.Clear();
            }
        }

        private async void LoadClipAsync(ulong id, Vector3 pos, Quaternion rotation, Action<SFXEntity> onLoaded)
        {
            InitIfNeeded();

            if (_handles == null || _audioCache == null)
            {
                CustomLogger.LogError("[SFXManager] Not initialized.");
                return;
            }

            try
            {
                if (!_handles.TryGetValue(id, out var handle))
                {
                    handle = Addressables.LoadAssetAsync<AudioClip>(id.ToString());
                    _handles.Add(id, handle);
                }
                else
                {
                    CustomLogger.LogWarning("You requested to load SFX while the system was already in a loading state.");
                }

                AudioClip clip = await handle.Task;

                if (clip == null)
                {
                    CustomLogger.LogError($"[SFXManager] Failed to load AudioClip. id={id}");
                    return;
                }

                _audioCache[id] = clip;

                var sfx = _audioSourcePool.Alloc(pos, rotation);
                sfx.SetClip(clip);
                onLoaded?.Invoke(sfx);
            }
            catch (Exception e)
            {
                CustomLogger.LogError($"[SFXManager] LoadClipAsync exception: {e}");
            }
        }

        private async void LoadClipsAsync(ulong groupId, ulong[] clipsId)
        {
            InitIfNeeded();

            if (_batchHandles == null || _audioCache == null)
            {
                CustomLogger.LogError("[SFXManager] Not initialized.");
                return;
            }

            try
            {
                if (!_batchHandles.TryGetValue(groupId, out var handle))
                {
                    handle = Addressables.LoadAssetsAsync<AudioClip>(groupId.ToString(), _ => { });
                    _batchHandles.Add(groupId, handle);
                }
                else
                {
                    CustomLogger.LogWarning("You requested to load SFX while the system was already in a loading state.");
                }

                IList<AudioClip> clips = await handle.Task;

                if (clips == null)
                {
                    CustomLogger.LogError($"[SFXManager] Failed to load batch AudioClips. groupId={groupId}");
                    return;
                }

                if (clipsId == null)
                {
                    CustomLogger.LogError("[SFXManager] clipsId array is null.");
                    return;
                }

                if (clips.Count != clipsId.Length)
                {
                    CustomLogger.LogError("The number of resources requested SFX to load is not the same as the number of id arrays.");
                }

                int count = Mathf.Min(clips.Count, clipsId.Length);
                for (int i = 0; i < count; i++)
                {
                    _audioCache[clipsId[i]] = clips[i];
                }
            }
            catch (Exception e)
            {
                CustomLogger.LogError($"[SFXManager] LoadClipsAsync exception: {e}");
            }
        }
    }
}
