using Cysharp.Threading.Tasks.Triggers;
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
	/// <summary>
	/// 1. 씬/스테이지 진입 전 필요한 VFX를 미리 로딩
	/// <br/>2. 게임 중 특정 VFX 요청이 오면 캐시에서 꺼내 재생
	/// <br/>3. 풀링 VFX는 재사용, 비풀링 VFX는 사용 후 파괴
	/// </summary>
	public class VFXManager : MonoBehaviour
	{
		public static VFXManager Instance;

		Transform _vfxParents;
		//게임플레이 중 VFX 사용목적 변수들
		//로드된 VFX 프리팹의 저장소
		private Dictionary<eVFXType, VFXEntity> _effectCache; 
		//씬에서 사용할 VFX 타입별 오브젝트 풀
		private Dictionary<eVFXType, ObjectPool<VFXEntity>> _VFXPools;
		//각 스테이지 / 씬에 어떤 VFX리소스가 존재하는지에 대한 딕셔너리.
		//현재는 구현안되어있지만 이후 Preload하는 등 새로 캐시를 불러올때 해당 딕셔너리에 추가할 예정
		private Dictionary<ulong, HashSet<eVFXType>> _BatchLoadedVfxIds;
		
		//Addressable 생명주기 관리용 변수들
		//Addressables 리소스 해제용 handle 저장소
		private Dictionary<ulong, AsyncOperationHandle<IList<GameObject>>> _BatchHandles;
		//개별 VFX를 즉시 로딩했을 때의 단일 handle 저장소
		private Dictionary<eVFXType, AsyncOperationHandle<GameObject>> _Handles;

		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
				Instance.Init();
				DontDestroyOnLoad(gameObject);
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
		/// _effectCache에 로딩되어 있는 VFX 프리팹들을 보고, 풀링 대상이면 _VFXPools에 풀을 만드는 메서드
		/// </summary>
		public void OnEnterScene()
		{
			//기존 루트가 남아있으면 내부의 오브젝트들이 제거되지 않을 수 있어 정리하고 새로 만들어주기
			if (_vfxParents != null)
			{
				Destroy(_vfxParents.gameObject);
				_vfxParents = null;
			}
			GameObject obj = new GameObject("VFX_Root");
			_vfxParents = obj.transform;
			
			_VFXPools.Clear();
			
			foreach ((eVFXType id, VFXEntity vfxObj) in _effectCache)
			{
				//만약 pooling effect면, pooling해주기.
				if (CheckPoolingEffect(id))
				{
					ObjectPool<VFXEntity> objectPool = new ObjectPool<VFXEntity>();
					objectPool.Init((int)DEFAULT_VALUE.PoolingSize, _vfxParents, vfxObj);
					_VFXPools.Add(id, objectPool);
				}
			}
		}

		/// <summary>
		/// VFX cache를 데우는 비동기 함수입니다. 로딩에서 통제합니다. 
		/// </summary>
		public AsyncOperationHandle<IList<GameObject>> PreLoadVFX(ulong groupId, eVFXType[] IdList)
		{
			if (IdList.Length == 0)
			{
				CustomLogger.LogWarning("IDList is EMPTY");
				return default;
			}

			AsyncOperationHandle<IList<GameObject>> handle;
			bool IsLoading = _BatchHandles.TryGetValue(groupId, out handle);
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
			if (!_BatchHandles.TryGetValue(groupId, out var handle)) 
				return;
			
			Addressables.Release(handle);
			_BatchHandles.Remove(groupId);
		}
		/// <summary> 전체 VFX 리소스를 제거하는 메서드 </summary>
		public void unloadVFXBatch()
		{
			_effectCache.Clear();
			_VFXPools.Clear();
			foreach (var handle in _BatchHandles.Values)
			{
				if (handle.IsValid())
					Addressables.Release(handle);
			}
			_BatchHandles.Clear();
		}
		public void unloadSingleVFX(eVFXType id)
		{
			bool flag;
			flag = _Handles.TryGetValue(id, out var handle);
			if (flag)
			{
				Addressables.Release(handle);
				_Handles.Remove(id);
			}
		}

		private async void RequestAsyncLoadAssets(ulong groupId, eVFXType[] IdList)
		{
			IList<GameObject> result;
			AsyncOperationHandle<IList<GameObject>> handle;
			bool IsRequested = _BatchHandles.TryGetValue((ulong)groupId, out handle);
			if (IsRequested)
			{
				return;
			}

			//ToFIx : eVFXType에 있는걸 아이디로 넣어야함.
			IList<string> keys = Array.ConvertAll(IdList, (id) => id.ToString());
			handle = Addressables.LoadAssetsAsync<GameObject>(keys, (loaded) =>
			{

			}, Addressables.MergeMode.Union);
			_BatchHandles.Add((ulong)groupId, handle);

			result = await handle.Task;
			//IdList에 중복이 있거나 MergeMode.Union으로 중복을 합치면 result와 IdList에 차이가 날 수 있음. 로그만 찍고 계속진행
			if (result.Count != IdList.Length)
			{
				CustomLogger.LogWarning($"[VFXManager] Requested VFX count and loaded count differ. Requested:{IdList.Length}, Loaded:{result.Count}");
			}

			/*
			 * Log : 기존 방식은 IDList[i]와 result[i]가 동일하다는 보장이 없음.
			 * ex. IDList = ["HittedVFX", "HittedVFX2"]지만 resuilt = ["HittedVFX2", "HittedVFX"]일 수 있음
			 * 이로인해 오류는 발생하지 않지만 이펙트 출력이 꼬일 위험이 존재해서 이를 수정함
			 */
			foreach (GameObject obj in result)
			{
				//result 프리팹의 이름을 가진 eVFXType을 변환
				//ex. 오브젝트의 이름이 HittedVFX -> eVFXType의 HittedVFX = 2147483649로 변환
				//여기서 주의할점은 프리팹(Assets.Prefabs.VFX)의 이름과 eVFXType내부의 이름이 일치해야함 
				if (!Enum.TryParse(obj.name, out eVFXType key))
				{
					CustomLogger.LogWarning($"[VFXManager] VFX 이름을 eVFXType으로 변환할 수 없습니다: {obj.name}");
					continue;
				}

				VFXEntity resource = obj.GetComponent<VFXEntity>();
				if (resource == null)
				{
					CustomLogger.LogWarning($"[VFXManager] VFXEntity가 없습니다: {obj.name}");
					continue;
				}
				
				CacheAssets(key, resource);
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
				CustomLogger.Log("You Request to load Asset!");
				handle = Addressables.LoadAssetAsync<GameObject>(id.ToString());
				_Handles.Add(id, handle);
				loadedObj = await handle.Task; // nonBlocking, 아래를 실행하지 않고 흐름을 넘김.
			}
			//Callback으로 등록
			resourceVfx = loadedObj.GetComponent<VFXEntity>();
			CacheAssets(id, resourceVfx);
			//만약 pooling effect면, pooling해주기.
			if (CheckPoolingEffect(id))
			{
				ObjectPool<VFXEntity> objectpool = new ObjectPool<VFXEntity>();
				objectpool.Init((int)DEFAULT_VALUE.PoolingSize, _vfxParents, resourceVfx);
				_VFXPools.Add(id, objectpool);
			}

			InstantiateEffect(id, resourceVfx, pos, rotation, out VFXEntity instance);
			instance.SetId(id);
			OnLoaded?.Invoke(instance);
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
		/// <summary> _effectCache에 새로운 VFX 오브젝트 추가</summary>
		private void CacheAssets(eVFXType id, VFXEntity obj)
		{
			if(!_effectCache.TryAdd(id, obj))
			{
				//디버깅용으로 필수는 아님
				CustomLogger.LogWarning($"[VFXManager] VFX already cached: {id}");
			}
		}
		public void Clear()
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
				CustomLogger.LogWarning("Instantiate VFX");
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

