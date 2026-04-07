using Newtonsoft.Json;
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
        private List<HuntResult> _sendmsg;
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
            _sendmsg = new List<HuntResult>();
			//PreLoadStageFile();
		}

        public void Clear()
        {
            MonsterSpawner.Instance.Clear();
		}

        /// <summary>
        /// ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½Ù²ï¿½, Stageï¿½ï¿½ ï¿½Ê¿ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ñµ¿±ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ Loadï¿½Ï´ï¿½ ï¿½Ô¼ï¿½ï¿½Ô´Ï´ï¿½.
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        public AsyncOperationHandle<IList<GameObject>> PreLoadAssets(eStage stage)
        {
            //ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½Ö´ï¿½ Monster Typeï¿½ï¿½ Load
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
                CustomLogger.LogWarning("Stageï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½Ã»ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½, Cacheï¿½ï¿½ï¿½ï¿½ ï¿½Ê¾Ò½ï¿½ï¿½Ï´ï¿½.");
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
				CustomLogger.LogWarning("Stageï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½Ã»ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½, Cacheï¿½ï¿½ï¿½ï¿½ ï¿½Ê¾Ò½ï¿½ï¿½Ï´ï¿½.");
				return null;
			}
			return ret;
		}

		public void StartStage(eStage stage)
		{
			Debug.Log("StageManager ÁøÀÔ");
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

            Debug.Log($"Stageï¿½ï¿½ï¿½ï¿½ {_currentStage} ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ : {_totalCnt}");

		}

        //ï¿½ï¿½ï¿½Í¸ï¿½ ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ ï¿½Î¸ï¿½ï¿½ï¿½ ï¿½Ô¼ï¿½
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
                // WaveManagerê°€ ì¡´ìž¬?˜ë©´ ?ë¦„???„ìž„
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

            /*Dummy °èÁ¤ÀÌ¶ó, ¾ÆÁ÷Àº Ãß°¡x. ÇÃ·¹ÀÌÇÏ´Â°É ºÁ¾ßÇÔ.*/

			//NetworkManager.Instance.OnStageClear(OnStageClearSuccess, OnError);
			//Stageï¿½ï¿½ ï¿½Ù²ï¿½ï¿½ï¿½Ñ´ï¿?-> ï¿½ï¿½ï¿½Ò½ï¿½ ï¿½Îµï¿½ï¿½ï¿½ ï¿½Ê¿ï¿½ï¿½ï¿½.
			if (res == eStageResult._StageChanged)
			{
				CustomLogger.Log($"Go To Next Stage");
				GameManager.Instance.LoadStage(_currentStage, nxtStage, StartStage);
			}
			else
			{
				CustomLogger.Log($"Go To Next Wave");
                //Ã¼·ÂÈ¸º¹
				StartStage(nxtStage);
				//Todo : Ä³ï¿½ï¿½ï¿½ï¿½ HPÈ¸ï¿½ï¿½
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
        //Ä³ï¿½ï¿½ï¿½Í°ï¿½ ï¿½ï¿½ï¿?ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿? ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½Ñ°Ü¾ï¿½ï¿½ï¿½.
        public void DecrementCharacterCount()
        {
            --_totalCharacterCnt;
            if (_totalCharacterCnt <= 0)
            {
				_IsLoop = true;
				eStage prevStage;
                CalculatePrevStage(_currentStage, out prevStage);
				CustomLogger.Log($"GoTo Prev Wave");
                
				StartStage(prevStage);
			}
		}
        private eStageResult CalculateNextStage(eStage curstage, out eStage nxtstage)
        {
            //waveï¿½ï¿½ 10ï¿½Ì¸ï¿½ ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½
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
			//waveï¿½ï¿½ 1ï¿½Ì¸ï¿½ ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ï¿½ ï¿½ï¿½ï¿½ï¿½.
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
				Debug.Log($"[StageManager] ÀâÀº ¸ó½ºÅÍ°¡ ¾ø½À´Ï´Ù.");
				return;
            }

            //ÀÌ°Å Ç®¸µÇÏÀÚ
            HuntResult code;

			foreach (var mon in _huntResultList)
            {
                eMonsterType type = mon.Key;
                int count = mon.Value;

                code = new HuntResult
				{
                    MonsterType = type,
                    Count = (short)count
                };
                Debug.Log($"[StageManager] ÀâÀº ¸ó½ºÅÍ : {type} | count : {count}");
                _sendmsg.Add(code);
			}

            //
            NetworkManager.Instance.OnHuntReward(_sendmsg, OnHuntRewardSuccess, OnError);
            _huntResultList.Clear();
            _sendmsg.Clear();
		}

        private void OnHuntRewardSuccess(ExecuteFunctionResult result)
        {
			string response = JsonConvert.SerializeObject(result.FunctionResult);
            OnHuntResponseDTO huntResult = JsonConvert.DeserializeObject<OnHuntResponseDTO>(response);

            //À¯ÀúÁ¤º¸ ¼ÂÆÃ (°ñµå, Å³½ºÄÚ¾î, °æÇèÄ¡, °ñµå, AncientGold)
            UserManager.Instance.SetHuntResult(huntResult);
		}

        private void OnStageClearSuccess(ExecuteFunctionResult result)
        {
            //For Debugging
			string response = JsonConvert.SerializeObject(result.FunctionResult);
		}

        private void OnError(PlayFab.PlayFabError error)
        {
            Debug.Log(error.ErrorMessage);
        }


		private void Update()
		{
            if (_LastTick + TICK_INTERVAL < Time.time)
            {
                SendHuntResult();
                _LastTick = Time.time;
			}
		}
	}
}

