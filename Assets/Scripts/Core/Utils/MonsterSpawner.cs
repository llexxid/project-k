using Cysharp.Threading.Tasks.Triggers;
using Scripts.Core;
using Scripts.Core.DataStructure;
using Scripts.Core.SO;
using Scripts.Monster;
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Scripts.Core.Utils
{
	using Monster = Scripts.Monster.Monster;
	public class MonsterSpawner : MonoBehaviour
	{
		[SerializeField]
		MonsterInfoSO _monsterInfo;

		public static MonsterSpawner Instance;
		
		// 몬스터 리소스 캐시
		private Dictionary<eMonsterType, ObjectPool<Monster>> _MonsterPool;
		private Dictionary<eMonsterType, Monster> _monsterCache;

		//Asset
		private Dictionary<long, MonsterAssetGroupCache> _monsterAssetGroup;

		private Transform _monParents;
		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
				Instance.Init();
				DontDestroyOnLoad(gameObject);
				return;
			}
			Destroy(gameObject);
			return;
		}
		private void Init()
		{
			_monsterCache = new Dictionary<eMonsterType, Monster>();
			_MonsterPool = new Dictionary<eMonsterType, ObjectPool<Monster>>();
			_monsterAssetGroup = new Dictionary<long, MonsterAssetGroupCache>();
			_monsterInfo.Init();
		}

		public void OnEnterScene()
		{
			GameObject obj = new GameObject("MON_ROOT");
			_monParents = obj.transform;
			_MonsterPool.Clear();
			foreach (var Item in _monsterCache)
			{
				eMonsterType key = Item.Key;
				Monster monObj = Item.Value;
				ObjectPool<Monster> monPool = new ObjectPool<Monster>();
				monPool.Init((int)DEFAULT_VALUE.PoolingSize, _monParents, monObj);

				_MonsterPool.Add(key, monPool);
			}
		}

		public void Clear()
		{
			_monsterCache.Clear();
			_MonsterPool.Clear();

			foreach (var item in _monsterAssetGroup.Values)
			{
				item.Release();
			}

			_monsterAssetGroup.Clear();
		}

		public MonsterInfo GetMonsterInfo(eMonsterType type)
		{
			MonsterInfo ret;
			_monsterInfo.TryGetMonsterInfo(type, out ret);
			return ret;
		}


		public void SpawnMonster(eMonsterType id, double ratio, Vector3 pos, Quaternion rotate, out Monster monster)
		{
			ObjectPool<Monster> pool;

			// 기본 풀 존재 여부 확인
			bool IsExistMonster = _MonsterPool.TryGetValue(id, out pool);
			if (!IsExistMonster)
			{
				// 스테이지 전환 중 pool은 지워졌지만 cache에는 남아 있는 경우 즉석 생성
				Monster cached;
				if (_monsterCache.TryGetValue(id, out cached) && _monParents != null)
				{
					pool = new ObjectPool<Monster>();
					pool.Init((int)DEFAULT_VALUE.PoolingSize, _monParents, cached);
					_MonsterPool.Add(id, pool);
				}
				else
				{
					CustomLogger.LogWarning("Requested monster was not found in the pool.");
					monster = default;
					return;
				}
			}
            
			monster = pool.Alloc(pos, rotate);
            
            // 몬스터 태그 강제 설정 (PlayerDetection 인식 보장)
            monster.tag = "Enemy";

			_monsterInfo.TryGetMonsterInfo(id, out MonsterInfo info);

			// 배율을 적용해 몬스터 스탯 초기화
			Monster.MonsterStat stat = new Monster.MonsterStat(
				(long)(info._baseHp * ratio), 
				0, 
				(ulong)(info._baseAtk * ratio), 
				info._baseMoveSpeed, 
				info._baseAtkSpeed
				);
			monster.Exp = (long)info._exp;
			monster.Ratio = ratio;
			monster.Init(id, stat, info._dropTableNumber);
			monster.gameObject.SetActive(true);
			return;
		}

		public void ReleaseMonster(eMonsterType id, Monster monster)
		{
			ObjectPool<Monster> pool;
			bool IsExistMonster = _MonsterPool.TryGetValue(id, out pool);
			if (!IsExistMonster)
			{
				CustomLogger.LogWarning("Tried to release a monster that does not have a pool.");
				return;
			}
			pool.Release(monster);
			return;
		}

		/// <summary>
		/// 스테이지 그룹에 필요한 몬스터 프리팹을 비동기로 로딩해 캐시에 등록한다.
		/// 같은 그룹 로딩이 이미 진행 중이면 기존 Task를 반환한다.
		/// </summary>
		/// <param name="groupId">스테이지 리소스 그룹 ID</param>
		/// <param name="ids">해당 그룹에서 사용할 몬스터 타입 목록</param>
		/// <returns>몬스터 프리팹 캐싱이 끝날 때 완료되는 Task</returns>
		public UniTask LoadMonsterAssets(eStage groupId, eMonsterType[] ids)
		{
			long key = (long)groupId;
			//같은 스테이지 그룹 로딩 요청이 이미 있으면 기존 LoadTask를 반환한다.
			if (_monsterAssetGroup.TryGetValue(key, out MonsterAssetGroupCache cache))
			{
				return cache.LoadTask;
			}
			cache = new MonsterAssetGroupCache();
			_monsterAssetGroup.Add((long)groupId, cache);
			cache.LoadTask = LoadAssetAsync(cache, ids);
			// 호출자는 이 Task를 await해서 몬스터 캐시가 준비될 때까지 기다릴 수 있다
			return cache.LoadTask;
		}

		private async UniTask LoadAssetAsync(MonsterAssetGroupCache cache, eMonsterType[] ids)
		{
			var tasks = new List<UniTask>();
			// LoadAssetsAsync는 결과 순서가 요청 순서와 다를 수 있으므로
			// 몬스터 타입별로 단일 LoadAssetAsync를 요청하고 id와 handle을 직접 매핑
			foreach (eMonsterType id in ids)
			{
				//해당 몬스터가 이미 로딩되어 있거나, 핸들에 있을 때 넘기기(cache.Handles 덮어쓰기 방지)
				if(_monsterCache.ContainsKey(id) || cache.Handles.ContainsKey(id))
					continue;
				
				var handle = Addressables.LoadAssetAsync<GameObject>(id.ToString());
				cache.Handles.Add(id, handle);
				tasks.Add(CacheMonster(id, handle));
			}

			await UniTask.WhenAll(tasks);
		}

		private async UniTask CacheMonster(eMonsterType id, AsyncOperationHandle<GameObject> handle)
		{
			// Addressables 로드가 끝난 prefab에서 Monster 컴포넌트를 꺼내 전역 몬스터 캐시에 등록
			GameObject prefab = await handle.Task;
			Monster monster = prefab.GetComponent<Monster>();
			if (monster == null)
			{
				Debug.LogWarning($"[MonsterSpawner] No monster types for AsyncOperationHandle<GameObject>: {handle}");
				return;
			}
			_monsterCache.TryAdd(id, monster);
		}

		private class MonsterAssetGroupCache
		{
			public UniTask LoadTask;

			public Dictionary<eMonsterType, AsyncOperationHandle<GameObject>> Handles =
				new Dictionary<eMonsterType, AsyncOperationHandle<GameObject>>();

			public bool Contains(eMonsterType type)
			{
				return Handles.ContainsKey(type);
			}

			public void Release()
			{
				foreach (var handle in Handles.Values)
				{
					if (handle.IsValid())
					{
						Addressables.Release(handle);
					}
				}
				Handles.Clear();
			}
		}
	}
	
}
