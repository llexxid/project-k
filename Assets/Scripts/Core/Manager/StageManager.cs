using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using static Scripts.Core.SO.StageMetaDataSO;
using Scripts.Core.SO;
using Scripts.Core.Utils;

namespace Scripts.Core
{
    using Monster = Scripts.Monster.Monster;
    public class StageManager : MonoBehaviour
    {

        public static StageManager Instance;
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
            _stageSO.Init();
            //PreLoadStageFile();
        }

        public void Clear()
        {
            MonsterSpawner.Instance.Clear();
		}

        /// <summary>
        /// 스테이지가 바뀔때, Stage에 필요한 정보들을 비동기적으로 Load하는 함수입니다.
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        public AsyncOperationHandle<IList<GameObject>> PreLoadAssets(eStage stage)
        {
            //스테이지 정보에 있는 Monster Type들 Load
            List<eMonsterType> monsterTypes;

            _stageSO.TryGetMonsterList(stage, out monsterTypes);
            var Handle = MonsterSpawner.Instance.LoadMonsterAssets(stage, monsterTypes.ToArray());
            return Handle;
        }

        public List<StageInfo_v> GetStageMonsterInfo(eStage stage)
        {
            List<StageInfo_v> ret;
            bool IsValid;
			IsValid = _stageSO.TryGetStageInfo(stage, out ret);
            if (!IsValid)
            {
                CustomLogger.LogWarning("Stage의 정보를 요청했지만, Cache되지 않았습니다.");
                return null;
            }
            return ret;
        }

        public List<eMonsterType> GetStageMonsterTypes(eStage stage)
        {
            List<eMonsterType> ret;
            bool IsValid;
            IsValid = _stageSO.TryGetMonsterList(stage, out ret);
            if (!IsValid)
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

