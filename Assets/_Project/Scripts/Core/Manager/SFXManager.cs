using Scripts.Core.DataStructure;
using Scripts.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
		[SerializeField] AudioClip[] _lobbyMusic;
		[SerializeField] AudioClip[] _combatMusic;
		[SerializeField, Range(0f, 1f)] float _musicVolume = .65f;
		AudioSource _crossfadeSource;
		AudioSource _activeMusic;
		Coroutine _playlistRoutine;
		[System.NonSerialized] eSFXType _musicId;
		int _musicRequest;
		bool _applicationPaused;
		float _bgmGain, _crossfadeGain;
		readonly WaitForSecondsRealtime _musicTick = new(.1f);
		public string CurrentMusic => _activeMusic != null && _activeMusic.clip != null ? _activeMusic.clip.name : "";
		public AudioSource CurrentMusicSource => _activeMusic;

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
			KingdomIdle.UGUI.GameAudioSettings.Apply();
			KingdomIdle.UGUI.GameAudioSettings.Changed += ApplyMusicVolume;
			_sfxParents = gameObject.transform;

			if (_bgmSource == null)
				_bgmSource = gameObject.AddComponent<AudioSource>();
			_bgmSource.playOnAwake = false;
			_crossfadeSource = gameObject.AddComponent<AudioSource>();
			_crossfadeSource.playOnAwake = false;
			_crossfadeSource.spatialBlend = 0f;
			_activeMusic = _bgmSource;

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
			// Configured streaming playlists replace the legacy music Addressables.
			clipsId = Array.FindAll(clipsId, id => !HasConfiguredMusic(id));
			if (clipsId.Length == 0) return default;
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
			LoadClipAsync(Id, pos, rotation, OnLoaded).Forget();
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
		private async UniTask LoadClipAsync(eSFXType Id, Vector3 pos, Quaternion rotation, Action<SFXEntity> OnLoaded)
		{
			try
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
			}
			catch (OperationCanceledException)
			{
				CustomLogger.LogWarning($"[SFXManager] AudioClip loading canceled: {Id}");
			}
			catch (Exception ex)
			{
				CustomLogger.LogError($"[SFXManager] AudioClip loading failed: {Id}\n{ex}");
			}
		}
		/// <summary>
		/// 필요한 SFX들 로드하는 함수
		/// </summary>
		/// <param name="groupId"></param>
		/// <param name="clipsId"></param>
		public async UniTask LoadClipsAsync(ulong groupId, eSFXType[] clipsId)
		{
			bool IsLoaded = _BatchHandles.TryGetValue(groupId, out var handle);
			IList<AudioClip> clips;
			if (IsLoaded)
			{
				CustomLogger.LogWarning("You requested to load SFX while the system was already in a loading state.");
				return;
			}

			IList<string> keys = Array.ConvertAll(clipsId, (id) => id.ToString());
			handle = Addressables.LoadAssetsAsync<AudioClip>(keys, (loaded) => { }, Addressables.MergeMode.Union);
			_BatchHandles.Add((ulong)groupId, handle);

			clips = await handle.Task;
			foreach (AudioClip clip in clips)
			{
				if (Enum.TryParse(clip.name, out eSFXType key) && !_AudioCache.ContainsKey(key))
					_AudioCache.Add(key, clip);
			}
		}

		public void unloadSFXBatch(ulong groupId)
		{
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

		bool HasConfiguredMusic(eSFXType id) =>
			(id == eSFXType.TITLE && _lobbyMusic != null && _lobbyMusic.Length > 0) ||
			((id == eSFXType.BGM || id == eSFXType.MainBGM) && _combatMusic != null && _combatMusic.Length > 0);

		public void PlayBGM(eSFXType id)
		{
			var playlist = id == eSFXType.TITLE ? _lobbyMusic :
				(id == eSFXType.BGM || id == eSFXType.MainBGM ? _combatMusic : null);
			if (playlist != null && playlist.Length > 0)
			{
				if (_playlistRoutine != null && _musicId == id) return;
				_musicRequest++;
				_musicId = id;
				if (_playlistRoutine != null) StopCoroutine(_playlistRoutine);
				_playlistRoutine = StartCoroutine(PlayPlaylist(playlist));
				return;
			}
			_musicRequest++;
			if (_playlistRoutine != null) { StopCoroutine(_playlistRoutine); _playlistRoutine = null; }
			_crossfadeSource.Stop();
			AudioClip clip;
			bool isLoaded = _AudioCache.TryGetValue(id, out clip);
			if (isLoaded)
			{
				SetAndPlayBGM(clip);
				return;
			}
			LoadBGMAsync(id, _musicRequest).Forget();
		}

		public void StopBGM()
		{
			_musicRequest++;
			if (_playlistRoutine != null) { StopCoroutine(_playlistRoutine); _playlistRoutine = null; }
			_bgmSource.Stop();
			_crossfadeSource.Stop();
		}

		void OnApplicationPause(bool paused) => _applicationPaused = paused;

		void OnDestroy()
		{
			KingdomIdle.UGUI.GameAudioSettings.Changed -= ApplyMusicVolume;
			if (Instance == this) Instance = null;
		}
		void ApplyMusicVolume()
		{
			float gain = KingdomIdle.UGUI.GameAudioSettings.Music;
			if (_bgmSource != null) _bgmSource.volume = _bgmGain * gain;
			if (_crossfadeSource != null) _crossfadeSource.volume = _crossfadeGain * gain;
		}
		void SetMusicGain(AudioSource source, float gain)
		{
			if (source == _bgmSource) _bgmGain = gain; else _crossfadeGain = gain;
			source.volume = gain * KingdomIdle.UGUI.GameAudioSettings.Music;
		}

		IEnumerator PlayPlaylist(AudioClip[] playlist)
		{
			int index = 0;
			while (true)
			{
				var clip = playlist[index];
				index = (index + 1) % playlist.Length;
				if (clip == null) { yield return _musicTick; continue; }
				var outgoing = _activeMusic;
				var incoming = outgoing == _bgmSource ? _crossfadeSource : _bgmSource;
				incoming.Stop();
				incoming.clip = clip;
				incoming.loop = false;
				SetMusicGain(incoming, 0f);
				incoming.Play();
				_activeMusic = incoming;
				float from = outgoing == _bgmSource ? _bgmGain : _crossfadeGain;
				float elapsed = 0f;
				while (elapsed < 1.2f)
				{
					if (!_applicationPaused)
					{
						elapsed += Time.unscaledDeltaTime;
						float t = Mathf.Clamp01(elapsed / 1.2f);
						SetMusicGain(incoming, _musicVolume * t);
						SetMusicGain(outgoing, from * (1f - t));
					}
					yield return null;
				}
				outgoing.Stop();
				outgoing.clip = null;
				SetMusicGain(incoming, _musicVolume);
				while (_applicationPaused || incoming.time < clip.length - 1.3f)
				{
					if (!_applicationPaused && !incoming.isPlaying && incoming.time == 0f) break;
					yield return _musicTick;
				}
			}
		}

		private void SetAndPlayBGM(AudioClip clip)
		{
			_bgmSource.clip = clip;
			SetMusicGain(_bgmSource, _musicVolume);
			_activeMusic = _bgmSource;
			_bgmSource.loop = true;
			_bgmSource.Play();
		}

		private async UniTask LoadBGMAsync(eSFXType id, int request)
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
			if (request == _musicRequest && this != null) SetAndPlayBGM(clip);
		}
	}
}

