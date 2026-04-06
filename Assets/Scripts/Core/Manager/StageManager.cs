using Newtonsoft.Json.Linq;
using PlayFab.CloudScriptModels;
using Scripts.Core;
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
        /// ���������� �ٲ�, Stage�� �ʿ��� �������� �񵿱������� Load�ϴ� �Լ��Դϴ�.
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        public AsyncOperationHandle<IList<GameObject>> PreLoadAssets(eStage stage)
        {
            //�������� ������ �ִ� Monster Type�� Load
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
                CustomLogger.LogWarning("Stage�� ������ ��û������, Cache���� �ʾҽ��ϴ�.");
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
				CustomLogger.LogWarning("Stage�� ���������� ��û������, Cache���� �ʾҽ��ϴ�.");
				return null;
			}
			return ret;
		}

		public void StartStage(eStage stage)
		{
			Debug.Log("StageManager ����");
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

            Debug.Log($"Stage���� {_currentStage} ���� ������ : {_totalCnt}");

		}

        //���͸� ���� �� �θ��� �Լ�
        public void DecrementMonCount(IDamageable mon)
        {
            int count = 0;
            eMonsterType type = (eMonsterType)mon.GetTypeId();
            bool flag = _huntResultList.TryGetValue(type, out count);
            _huntResultList[type] = count + 1;

			--_totalCnt;
            CustomLogger.Log($"totalCount : {_totalCnt}");
            if (_totalCnt <= 0)
            {
                // WaveManager가 존재?�면 ?�름???�임
                if (WaveManager.Instance != null)
                {
                    WaveManager.Instance.OnWaveCleared();
                    return;
                }

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

			//Stage�� �ٲ���Ѵ�?-> ���ҽ� �ε��� �ʿ���.
			if (res == eStageResult._StageChanged)
			{
				//���ҽ� �ε��� ������, ���� ���� ��û
				CustomLogger.Log($"Go To Next Stage");

                //���������� �Ѿ ��, ��ɰ���� �ѹ� ������ �����ϰ� ����.
				GameManager.Instance.LoadStage(_currentStage, nxtStage, StartStage);
			}
			else
			{
				//Wave�� �ٲ���Ѵ�?-> ���?FadeOut/ ĳ���͵� HPȸ��
				CustomLogger.Log($"Go To Next Wave");
				StartStage(nxtStage);
				//Todo : ĳ���� HPȸ��
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
        //ĳ���Ͱ� ���?���� ���? ���� ���������� �Ѱܾ���.
        public void DecrementCharacterCount()
        {
            --_totalCharacterCnt;

            if (_totalCharacterCnt <= 0)
            {
				_IsLoop = true;
				eStage prevStage;
                CalculatePrevStage(_currentStage, out prevStage);
				CustomLogger.Log($"GoTo Prev Wave");
                
                //Todo : ĳ���� ��Ȱó��


				StartStage(prevStage);
			}
		}
        private eStageResult CalculateNextStage(eStage curstage, out eStage nxtstage)
        {
            //wave�� 10�̸� ���� ����������
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
			//wave�� 1�̸� ���� ���������� ����.
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

            //����ȭ
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
            //������ ������ �ִٸ� ������ ���� UIó�� �� ���� UIó��
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
                //SendHuntResult();
                _LastTick = Time.time;
			}
		}
	}
}

