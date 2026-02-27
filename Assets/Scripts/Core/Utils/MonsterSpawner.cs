using Cysharp.Threading.Tasks.Triggers;
using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Scripts.Monster;
using Scripts.Core.DataStructure;

namespace Scripts.Core.Utils
{
    using Monster = Scripts.Monster.Monster;
    public class MonsterSpawner : MonoBehaviour
    {
        public static MonsterSpawner Instance;
        //스테이지에 어떤 몬스터가 나오는지 리소스 관리
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
            //Load한다음, 풀링해서 주기
            ObjectPool<Monster> pool = new ObjectPool<Monster>();
            pool.Init((int)DEFAULT_VALUE.PoolingSize, component);
            _MonsterPool.Add(id, pool);
            Monster mon = pool.Alloc(pos, rotate);
            mon.gameObject.SetActive(true);
            //Todo : MonsterStat정보 정하기
            mon.Init(id, new Monster.MonsterStat(10,0,5,1,1), 0);
            callback?.Invoke(mon);
            return;
        }

        public void SpawnMonster(eMonsterType id, Vector3 pos, Quaternion rotate, out Monster monster)
        {
            ObjectPool<Monster> pool;

            //몬스터는 기본적으로 풀링 개체
            bool IsExistMonster = _MonsterPool.TryGetValue(id, out pool);
            if (!IsExistMonster)
            {
                CustomLogger.LogWarning("Pooling되지 않은 몬스터 스폰을 요청했습니다.");
                //여기서 부터는 사실상 예외처리. 해주려면 Callback을 받아야함.

                monster = default;
                return;
            }
            monster = pool.Alloc(pos, rotate);
            monster.gameObject.SetActive(true);
            return;
        }

        public void ReleaseMonster(eMonsterType id, Monster monster)
        {
            ObjectPool<Monster> pool;
            bool IsExistMonster = _MonsterPool.TryGetValue(id, out pool);
            if (!IsExistMonster)
            {
                CustomLogger.LogWarning("Pooling되지 않은 몬스터 반납을 요청했습니다.");
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
            else
            {
                IList<string> keys = Array.ConvertAll(id, (id) => id.ToString());
				handle = Addressables.LoadAssetsAsync<GameObject>(keys, (loaded) => { }, Addressables.MergeMode.Union);
                _Handles.Add((long)groupId, handle);
                result = await handle.Task;
            }
            //Stage에 있는 Monster들 생성
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
