using Cysharp.Threading.Tasks.Triggers;
using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Scripts.Monster;

namespace Scripts.Core
{
    using Monster = Scripts.Monster.Monster;
    public class MonsterSpawner : MonoBehaviour
    {
        public static MonsterSpawner Instance;
        //스테이지에 어떤 몬스터가 나오는지 리소스 관리
        private Dictionary<eMonsterType, Monster> _monsterCache;
        private Dictionary<eMonsterType, ObjectPool<Monster>> _MonsterPool;

        //Asset
        private Dictionary<int, AsyncOperationHandle<IList<Monster>>> _Handles;
        private Dictionary<eMonsterType, AsyncOperationHandle<Monster>> _SingleHandle;
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
            _monsterCache = new Dictionary<eMonsterType, Monster>();
            _MonsterPool = new Dictionary<eMonsterType, ObjectPool<Monster>>();

            _Handles = new Dictionary<int, AsyncOperationHandle<IList<Monster>>>();
            _SingleHandle = new Dictionary<eMonsterType, AsyncOperationHandle<Monster>>();
        }

        public async void SpawnMonsterForTest(eMonsterType id, Vector3 pos, Quaternion rotate, Action<Monster> callback)
        {
            AsyncOperationHandle<Monster> handle;
            if (_SingleHandle.TryGetValue(id, out handle) == true)
            {
                return;
            }
            handle = Addressables.LoadAssetAsync<Monster>(id.ToString());
            _SingleHandle.Add(id, handle);
            Monster result = await handle.Task;
            //Load한다음, 풀링해서 주기
            ObjectPool<Monster> pool = new ObjectPool<Monster>();
            pool.Init((int)DEFAULT_VALUE.PoolingSize, result);
            _MonsterPool.Add(id, pool);
            Monster mon = pool.Alloc(pos, rotate);

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

        public AsyncOperationHandle<IList<Monster>> LoadMonsterAssets(eStage groupId, eMonsterType[] idList)
        {
            LoadAssetAsync(groupId, idList);
            return _Handles[(int)groupId];
        }

        private async void LoadAssetAsync(eStage groupId, eMonsterType[] id)
        {
            IList<Monster> result;
            AsyncOperationHandle<IList<Monster>> handle;

            bool IsRequested;
            if (IsRequested = _Handles.TryGetValue((int)groupId, out handle))
            {
                return;
            }
            else
            {
                IList<string> keys = Array.ConvertAll(id, (id) => id.ToString());
                Addressables.LoadAssetsAsync<Monster>(keys, (loaded) => { });
                _Handles.Add((int)groupId, handle);
                result = await handle.Task;
            }
            //Stage에 있는 Monster들 생성
            int i = 0;
            foreach (Monster mon in result)
            {
                _monsterCache.Add(id[i], mon);
                ObjectPool<Monster> monPool = new ObjectPool<Monster>();
                monPool.Init((int)DEFAULT_VALUE.PoolingSize, mon);
                _MonsterPool.Add(id[i], monPool);
                i++;
            }
            return;
        }
    }

}
