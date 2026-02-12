using ExcelDataReader;
using Scripts.Core.SO;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Scripts.Core.SO.StageMetaDataSO;

namespace Scripts.Core
{
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance;
        private Dictionary<int, List<StageInfo_v>> _StageCache;
        [SerializeField]
        private StageMetaDataSO _stageSO;
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Init();
                DontDestroyOnLoad(this);
                return;
            }
            Destroy(this);
            return;
        }
        private void Init()
        {
            _StageCache = new Dictionary<int, List<StageInfo_v>>();
            _stageSO.Init();
            //PreLoadStageFile();
        }

        /// <summary>
        /// 스테이지가 바뀔때, Stage에 필요한 정보들을 비동기적으로 Load하는 함수입니다.
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        public AsyncOperationHandle<IList<Monster>> LoadAssets(eStage stage)
        {
            //스테이지 정보에 있는 Monster Type들 Load
            List<StageInfo_v> stageInfoList;

            bool IsCached;
            IsCached = _StageCache.TryGetValue((int)stage, out stageInfoList);
            if (!IsCached)
            {
                //이럴일은 없긴함.
                CustomLogger.LogWarning("NoPreLoad_StageInfo. Do Preload Stage");
                return default;
            }

            //stage정보 가져옴.
            int SpawnMonsterCount = stageInfoList.Count;
            eMonsterType[] SpawnMonsterTypes = new eMonsterType[SpawnMonsterCount];
            for (int i = 0; i < SpawnMonsterCount; i++)
            {
                SpawnMonsterTypes[i] = stageInfoList[i]._type;
            }

            var Handle = MonsterSpawner.Instance.LoadMonsterAssets(stage, SpawnMonsterTypes);
            return Handle;
        }

        public List<StageInfo_v> GetStageMonsterInfo(eStage stage)
        {
            List<StageInfo_v> ret;
            bool IsCached;
            IsCached = _StageCache.TryGetValue((int)stage, out ret);
            if (!IsCached)
            {
                CustomLogger.LogWarning("Stage의 몬스터정보를 요청했지만, Cache되지 않았습니다.");
                return null;
            }
            return ret;
        }

        private int GenerateKey(int stage, int wave)
        {
            return ((stage << 16) | wave);
        }
    }
}

