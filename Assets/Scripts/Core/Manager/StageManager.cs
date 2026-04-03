using Newtonsoft.Json.Linq;
using PlayFab.CloudScriptModels;
using Scripts.Core.inteface;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using Scripts.Monster.SO;
using Scripts.Server.DTO;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Scripts.Core.SO.StageMetaDataSO;
using static UnityEngine.Networking.UnityWebRequest;

namespace Scripts.Core
{
    using Monster = Scripts.Monster.Monster;
    public class StageManager : MonoBehaviour
    {
        enum eStageResult
        { 
            _WaveChanged,
            _StageChanged
        }
        public static StageManager Instance;
        [SerializeField]
        private StageMetaDataSO _stageSO;
        [SerializeField]
        private MonsterSpawnLocationSO _locationSO;

		private eStage _currentStage;
		private int _totalCnt;
        private int _totalCharacterCnt;

		private bool _IsLoop;

        const int LAST_WAVE = 10;
        const float TICK_INTERVAL = 3f;
        private float _LastTick;
        //SendBuffer
        private Dictionary<eMonsterType, int> _huntResultList;
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
			_totalCnt = 0;
			_IsLoop = false;
            _huntResultList = new Dictionary<eMonsterType, int>();
			_LastTick = Time.time;
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

		public void StartStage(eStage stage)
		{
			_currentStage = stage;
            //_totalCnt = 0;
			_totalCharacterCnt = 3;

			List<StageInfo_v> stageInfos;
			bool flag = _stageSO.TryGetStageInfo(stage, out stageInfos);

			if (!flag)
			{
				CustomLogger.LogWarning("There is no StageInfo");
				return;
			}

            int spawnLocationCnt = _locationSO.GetLocationCount();
			//스테이지에 맞는 몬스터 정보를 긁어옴.
			foreach (StageInfo_v info in stageInfos)
			{
				eMonsterType type = info._type;
				int count = info._count;

				_totalCnt += count;

                for (int i = 0; i < count; i++)
                {
					int randomIndex = UnityEngine.Random.Range(0, spawnLocationCnt);
					Vector2 pos;
					_locationSO.TryGetPos(randomIndex, out pos);

					Monster mon;
					MonsterSpawner.Instance.SpawnMonster(type, pos, Quaternion.identity, out mon);
                    mon.OnDeath += DecrementMonCount;
				}
			}
            Debug.Log($"Stage진입 {_currentStage} 몬스터 마릿수 : {_totalCnt}");
		}

        //몬스터를 잡을 때 부르는 함수
        public void DecrementMonCount(IDamageable mon)
        {
            int count = 0;
            eMonsterType type = (eMonsterType)mon.GetTypeId();
            bool flag = _huntResultList.TryGetValue(type, out count);
            _huntResultList[type] = count + 1;

			--_totalCnt;
            CustomLogger.Log($"totalCount : {_totalCnt}");
            //몬스터를 다 잡은 경우
            if (_totalCnt <= 0)
            {
                //Loop가 켜져있는 경우
                if (_IsLoop)
                {
					StartStage(_currentStage);
                    return;
				}
                GoToNextStage();
			}
        }

        private void GoToNextStage()
        {
			eStage nxtStage;
			eStageResult res = CalculateNextStage(_currentStage, out nxtStage);

			//Stage를 바꿔야한다 -> 리소스 로딩이 필요함.
			if (res == eStageResult._StageChanged)
			{
				//리소스 로딩이 끝나면, 몬스터 스폰 요청
				CustomLogger.Log($"Go To Next Stage");

                //스테이지를 넘어갈 땐, 사냥결과를 한번 서버에 저장하고 가자.
				GameManager.Instance.LoadStage(_currentStage, nxtStage, StartStage);
			}
			else
			{
				//Wave를 바꿔야한다 -> 잠시 FadeOut/ 캐릭터들 HP회복
				CustomLogger.Log($"Go To Next Wave");
				StartStage(nxtStage);
				//Todo : 캐릭터 HP회복
			}
		}

        public void TurnOnLoopStage()
        {
            _IsLoop = true;
		}

        public void TurnOffLoopStage()
        {
			_IsLoop = false;
		}
        //캐릭터가 모두 죽은 경우, 이전 스테이지로 넘겨야함.
        public void DecrementCharacterCount()
        {
            --_totalCharacterCnt;

            if (_totalCharacterCnt <= 0)
            {
				_IsLoop = true;
				eStage prevStage;
                CalculatePrevStage(_currentStage, out prevStage);
				CustomLogger.Log($"GoTo Prev Wave");
                
                //Todo : 캐릭터 부활처리


				StartStage(prevStage);
			}
		}
        private eStageResult CalculateNextStage(eStage curstage, out eStage nxtstage)
        {
            //wave가 10이면 다음 스테이지로
            ulong waveMask = 0x000000000000FFFF;

            ulong wave = ((ulong)curstage & waveMask);

			if (wave >= LAST_WAVE)
			{
				ulong stageAdder = 0x0000000000010000;
				nxtstage = (eStage)((ulong)curstage + stageAdder);
                ++nxtstage;
                return eStageResult._StageChanged;
			}

			nxtstage = (eStage)((ulong)++curstage);
            return eStageResult._WaveChanged;
		}
        private eStageResult CalculatePrevStage(eStage curstage, out eStage prevstage)
        {
			//wave가 1이면 이전 스테이지로 못감.
			ulong waveMask = 0x000000000000FFFF;

			ulong wave = ((ulong)curstage & waveMask);

			if (wave <= 1)
			{
                prevstage = curstage;
				return eStageResult._StageChanged;
			}

			prevstage = (eStage)((ulong)--curstage);
			return eStageResult._WaveChanged;
		}

        private void SendHuntResult()
        {
            if (_huntResultList.Count <= 0)
            {
                return;
            }

            //직렬화
            List<RewardCode> sendmsg = new List<RewardCode>();
            RewardCode code;

			foreach (var mon in _huntResultList)
            {
                eMonsterType type = mon.Key;
                int count = mon.Value;

                code = new RewardCode
                {
                    Code = (ulong)type << 16 | (uint)count,
                };
                sendmsg.Add(code);
			}
            //
            NetworkManager.Instance.OnHuntReward(sendmsg, OnHuntRewardSuccess, OnError);
            _huntResultList.Clear();
		}

        private void OnHuntRewardSuccess(ExecuteFunctionResult result)
        {
			OnHuntResponseDTO response = JObject.FromObject(result.FunctionResult).ToObject<OnHuntResponseDTO>();
            //레벨업 보상이 있다면 레벨업 보상 UI처리 및 각종 UI처리
		}

        private void OnError(PlayFab.PlayFabError error)
        {
            Debug.Log(error.ErrorMessage);
        }

		private void Update()
		{
            float curTime = Time.time;
			if (_LastTick + TICK_INTERVAL > curTime)
            {
                SendHuntResult();
                _LastTick = Time.time;
			}
		}
	}
}

