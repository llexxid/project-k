using Cysharp.Threading.Tasks.Triggers;
using Scripts.Core;
using Scripts.Core.DataStructure;
using Scripts.Core.SO;
using Scripts.Monster;
using System;
using System.Collections;
using System.Collections.Generic;
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
		//  Ͱ  ҽ 
		private Dictionary<eMonsterType, Monster> _monsterCache;
		private Dictionary<eMonsterType, ObjectPool<Monster>> _MonsterPool;

		//Asset
		private Dictionary<long, AsyncOperationHandle<IList<GameObject>>> _Handles;
		private Dictionary<eMonsterType, AsyncOperationHandle<GameObject>> _SingleHandle;

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

			_Handles = new Dictionary<long, AsyncOperationHandle<IList<GameObject>>>();
			_SingleHandle = new Dictionary<eMonsterType, AsyncOperationHandle<GameObject>>();
			_monsterInfo.Init();
		}

		public void OnEnterScene()
		{
			GameObject obj = new GameObject("MON_ROOT");
			_monParents = obj.transform;

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

			foreach (var item in _Handles)
			{
				Addressables.Release(item.Value);
			}
			foreach (var item in _SingleHandle)
			{
				Addressables.Release(item.Value);
			}
			_Handles.Clear();
			_SingleHandle.Clear();
		}

		public MonsterInfo GetMonsterInfo(eMonsterType type)
		{
			MonsterInfo ret;
			_monsterInfo.TryGetMonsterInfo(type, out ret);
			return ret;
		}

		public async void SpawnMonsterForTest(eMonsterType id, Vector3 pos, Quaternion rotate, Action<Monster> callback)
		{
			AsyncOperationHandle<GameObject> handle;
			if (_SingleHandle.TryGetValue(id, out handle) == true)
			{
				return;
			}
			string str = id.ToString();
			handle = Addressables.LoadAssetAsync<GameObject>(id.ToString());
			_SingleHandle.Add(id, handle);
			var result = await handle.Task;

			Monster component = result.GetComponent<Monster>();
			//LoadѴ, Ǯؼ ֱ
			ObjectPool<Monster> pool = new ObjectPool<Monster>();
			pool.Init((int)DEFAULT_VALUE.PoolingSize, component);
			_MonsterPool.Add(id, pool);
			Monster mon = pool.Alloc(pos, rotate);
			mon.gameObject.SetActive(true);
            
            // 테스트용 스폰 시에도 태그 강제 설정
            mon.tag = "Enemy";

			//Todo : MonsterStat ϱ
			mon.Init(id, new Monster.MonsterStat(10, 0, 5, 1, 1), 0);
			callback?.Invoke(mon);
			return;
		}

		public void SpawnMonster(eMonsterType id, double ratio, Vector3 pos, Quaternion rotate, out Monster monster)
		{
			ObjectPool<Monster> pool;

			//ʹ ⺻ Ǯ ü
			bool IsExistMonster = _MonsterPool.TryGetValue(id, out pool);
			if (!IsExistMonster)
			{
				// ?ㅽ뀒?댁? ?꾪솚 ??pool? 吏?뚯죱吏留?cache?먮뒗 ?덈뒗 寃쎌슦 利됱꽍 ?앹꽦
				Monster cached;
				if (_monsterCache.TryGetValue(id, out cached) && _monParents != null)
				{
					pool = new ObjectPool<Monster>();
					pool.Init((int)DEFAULT_VALUE.PoolingSize, _monParents, cached);
					_MonsterPool.Add(id, pool);
				}
				else
				{
					CustomLogger.LogWarning("Pooling?먯꽌 李얠쓣 ???녿뒗 紐ъ뒪???붿껌???덉뒿?덈떎.");
					monster = default;
					return;
				}
			}
            
			monster = pool.Alloc(pos, rotate);
            
            // 몬스터 태그 강제 설정 (PlayerDetection 인식 보장)
            monster.tag = "Enemy";

			_monsterInfo.TryGetMonsterInfo(id, out MonsterInfo info);

			//���� ���� �ʱ�ȭ�ؼ� �ֱ�
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
				CustomLogger.LogWarning("Pooling   ݳ û߽ϴ.");
				return;
			}
			pool.Release(monster);
			return;
		}

		public AsyncOperationHandle<IList<GameObject>> LoadMonsterAssets(eStage groupId, eMonsterType[] idList)
		{
			if (_Handles.ContainsKey((long)groupId))
			{
				return _Handles[(long)groupId];
			}
			LoadAssetAsync(groupId, idList);
			return _Handles[(long)groupId];
		}

		private async void LoadAssetAsync(eStage groupId, eMonsterType[] id)
		{
			IList<GameObject> result;
			AsyncOperationHandle<IList<GameObject>> handle;
			bool IsRequested = _Handles.TryGetValue((long)groupId, out handle);
			if (IsRequested)
			{
				return;
			}

			IList<string> keys = Array.ConvertAll(id, (id) => id.ToString());
			handle = Addressables.LoadAssetsAsync<GameObject>(keys, (loaded) => { }, Addressables.MergeMode.Union);
			_Handles.Add((long)groupId, handle);
	
			result = await handle.Task;
			//Stage ִ Monster 
			int i = 0;
			foreach (GameObject mon in result)
			{
				Monster monComponent = mon.GetComponent<Monster>();
				_monsterCache.Add(id[i], monComponent);
				i++;
			}
			return;
		}
	}

}
