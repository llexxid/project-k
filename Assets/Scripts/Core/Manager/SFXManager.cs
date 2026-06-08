using Scripts.Core.DataStructure;
using Scripts.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEngine.Networking.UnityWebRequest;

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
		[SerializeField]
		AudioSource _bgmSource;

		//SFX DataStore
		private Dictionary<eSFXType, AudioClip> _AudioCache;
		private Dictionary<eSFXType, AsyncOperationHandle<AudioClip>> _Handles;
		//각 스테이지 / 씬에 어떤 SFX리소스가 존재하는지에 대한 딕셔너리.
		//현재는 구현안되어있지만 이후 Preload하는 등 새로 캐시를 불러올때 해당 딕셔너리에 추가할 예정
		private Dictionary<ulong, HashSet<eSFXType>> _BatchLoadedSfxIds; 


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

			if (_bgmSource == null)
				_bgmSource = gameObject.AddComponent<AudioSource>();

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
			foreach (var handle in _Handles.Values)
			{
				Addressables.Release(handle);
			}
			foreach (var handle in _BatchHandles.Values)
			{
				Addressables.Release(handle);
			}
			_Handles.Clear();
			_BatchHandles.Clear();
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
			
			handle = Addressables.LoadAssetAsync<AudioClip>(Id.ToString());
			_Handles.Add(Id, handle);
			clip = await handle.Task;
			
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

			IList<string> keys = Array.ConvertAll(clipsId, (id) => id.ToString());
			handle = Addressables.LoadAssetsAsync<AudioClip>(keys, (loaded) => { }, Addressables.MergeMode.Union);
			_BatchHandles.Add((ulong)groupId, handle);


			clips = await handle.Task;
			foreach (AudioClip clip in clips)
			{
				if (System.Enum.TryParse(clip.name, out eSFXType key) && !_AudioCache.ContainsKey(key))
					_AudioCache.Add(key, clip);
			}
		}

		public void unloadSFXBatch(ulong groupId)
		{
			//
			bool flag;
			flag = _BatchHandles.TryGetValue(groupId, out var handle);
			if (flag)
			{
				Addressables.Release(handle);
				_BatchHandles.Remove(groupId);
			}
		}

		/// <summary> 전체 SFX 리소스를 정리하는 메서드</summary>
		public void unloadSFXBatch()
		{
			_AudioCache.Clear();
			
			foreach (var handle in _BatchHandles.Values)
			{
				if (handle.IsValid())
					Addressables.Release(handle);
			}

			_BatchHandles.Clear();
		}

		public void PlayBGM(eSFXType id)
		{
			AudioClip clip;
			bool isLoaded = _AudioCache.TryGetValue(id, out clip);
			if (isLoaded)
			{
				SetAndPlayBGM(clip);
				return;
			}
			LoadBGMAsync(id);
		}

		public void StopBGM()
		{
			_bgmSource.Stop();
		}

		private void SetAndPlayBGM(AudioClip clip)
		{
			_bgmSource.clip = clip;
			_bgmSource.loop = true;
			_bgmSource.Play();
		}

		private async void LoadBGMAsync(eSFXType id)
		{
			bool isLoaded = _Handles.TryGetValue(id, out var handle);
			if (isLoaded)
			{
				CustomLogger.LogWarning("You requested to load BGM while the system was already in a loading state.");
				return;
			}
			handle = Addressables.LoadAssetAsync<AudioClip>(id.ToString());
			_Handles.Add(id, handle);
			AudioClip clip = await handle.Task;
			_AudioCache.Add(id, clip);
			SetAndPlayBGM(clip);
		}
	}
}

