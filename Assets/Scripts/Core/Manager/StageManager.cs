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
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Scripts.Core.SO.StageMetaDataSO;
using static UnityEngine.Networking.UnityWebRequest;

namespace Scripts.Core
{
	using Monster = Scripts.Monster.Monster;
	public enum eStageResult
	{
		None, //변경사항 없음
		WaveChanged, 
		BossWaveEntered,
		StageChanged,
	}
	public class StageManager : MonoBehaviour
	{
		public static StageManager Instance;
		[SerializeField]
		private StageMetaDataSO _stageSO;
		[SerializeField]
		private MonsterSpawnLocationSO _locationSO;

		private eStage _currentStage;
		private int _totalCnt;
		private int _totalCharacterCnt;

		private bool _IsLoop;

		// ── [WaveManager] 같은 wave 내 OnWaveCleared 중복 호출 방지 ──
		// stale 비동기/이벤트로 _totalCnt가 음수로 떨어져 OnWaveCleared가
		// 여러 번 발사되는 것을 막는다. StartStage/ResetWaveCount에서 false로 초기화.
		private bool _waveCleared;
		// ── [WaveManager 끝] ──

		const int LAST_WAVE = 11;
		const float TICK_INTERVAL = 3f;
		private float _LastTick;

		//SendBuffer
		private Dictionary<eMonsterType, int> _huntResultList;
		private List<HuntResult> _sendmsg;

		public eStage CurrentStage => _currentStage;
		public int StageNumber { get; private set; }
		public int WaveNumber { get; private set; }
		public bool IsBossWave { get; private set; }
		public bool IsLoopMode { get; private set; }
		public void SetLoopMode(bool value) => IsLoopMode = value;
		public bool BossAutoChallenge { get; private set; }
		public void SetBossAutoChallenge(bool value) => BossAutoChallenge = value;

		
		private StageDefinition _currentDefinition;
		private StageSession _currentSession;
		
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
		public void InitStage(eStage stage)
		{
			_currentStage = stage;
			StageNumber = StageRule.GetStageNumber(stage);
			WaveNumber = StageRule.GetWaveNumber(stage);
			IsBossWave = StageRule.IsBossWave(stage);

			IsLoopMode = false;
			BossAutoChallenge = true;
		}
		public void Clear()
		{
			MonsterSpawner.Instance.Clear();
		}

		/// <summary>
		/// 스테이지 전환 전 필요한 몬스터 리소스를 비동기로 로드한다.
		/// </summary>
		/// <param name="stage"></param>
		/// <returns></returns>
		public UniTask PreLoadAssets(eStage stage)
		{
			// 스테이지 정보에 포함된 몬스터 타입을 로드
			_stageSO.TryGetMonsterList(stage, out List<eMonsterType> monsterTypes);
			var handle = MonsterSpawner.Instance.LoadMonsterAssets(stage, monsterTypes.ToArray());
			return handle;
		}

		public List<StageInfo_v> GetStageMonsterInfo(eStage stage)
		{
			List<StageInfo_v> ret;
			bool IsValid;
			IsValid = _stageSO.TryGetStageInfo(stage, out ret);
			if (!IsValid)
			{
				CustomLogger.LogWarning("StageInfo was requested, but it was not found in cache.");
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
				CustomLogger.LogWarning("Stage monster types were requested, but they were not found in cache.");
				return null;
			}
			return ret;
		}
		
		private bool CreateStageSession(eStage stage)
		{
			ExitCurrentSession();

			_currentDefinition = CreateStageDefinition(stage);
			if (_currentDefinition == null)
			{
				CustomLogger.LogWarning($"[StageManager] : Create Session if Failed: {stage}");
				return false;
			}

			_currentSession = new StageSession(
				_currentDefinition,
				MonsterSpawner.Instance
				);

			_currentSession.MonsterKilled += HandleMonsterKilled;
			_currentSession.Cleared += HandleWaveCleared;
			_currentSession.Enter();
			return true;
		}

		private void ExitCurrentSession()
		{
			if (_currentSession == null)
			{
				return;
			}
			
			_currentSession.MonsterKilled -= HandleMonsterKilled;
			_currentSession.Cleared -= HandleWaveCleared;
			_currentSession.Exit();

			_currentSession = null;
			_currentDefinition = null;
		}
		private void HandleMonsterKilled(Monster monster)
		{
			_huntResultList.TryGetValue(monster.Type, out int count);
			_huntResultList[monster.Type] = count + 1;
		}

		private void HandleWaveCleared()
		{
			WaveManager.Instance.OnWaveCleared();
		}
		public void SpawnStageMonster(eStage stage)
		{
			if (!CreateStageSession(stage))
				return;

			_waveCleared = false; // 새 wave 시작 — 클리어 가드 해제
			
			int locationCount = _locationSO.GetLocationCount();
			foreach (StageInfo_v entry in _currentDefinition.MonsterEntries)
			{
				for (int i = 0; i < entry._count; i++)
				{
					int index = UnityEngine.Random.Range(0, locationCount);
					_locationSO.TryGetPos(index, out Vector2 position);
					MonsterSpawner.Instance.SpawnMonster(
						entry._type,
						_currentDefinition.SpawnRatio,
						position,
						Quaternion.identity,
						out Monster monster);
					if (monster != null)
					{
						_currentSession.RegisterMonster(monster);
					}
				}
			}
			
			_currentSession.CompleteSpawning();
		}

		//신규작성
		private StageDefinition CreateStageDefinition(eStage stage)
		{
			eStage stageKey = StageParser.GetFixedStageKey(stage);
			double spawnRatio = StageParser.GetRatio(stage);
			Debug.Log(stageKey);
			if (!_stageSO.TryGetStageInfo(stageKey, out List<StageInfo_v> entries))
			{
				CustomLogger.LogWarning($"[StageManager] : Stage definition not found: {stage}");
				return null;
			}

			ulong resourceGroupId = LoadManager.Instance.GetResourceGroupId(stage);

			return new StageDefinition(
				stage,
				resourceGroupId,
				spawnRatio,
				entries
			);
		}

		public eStageResult MoveNext()
		{
			eStageResult result = StageRule.GetNextWave(CurrentStage, out eStage nextStage);
    
			_currentStage = nextStage;
			StageNumber = StageRule.GetStageNumber(CurrentStage);
			WaveNumber = StageRule.GetWaveNumber(CurrentStage);
			IsBossWave = result == eStageResult.BossWaveEntered;
    
			if (result == eStageResult.WaveChanged)
				IsLoopMode = false;
    
			return result;
		}
    
		public eStageResult MovePrev()
		{
			eStageResult result = StageRule.GetPreviousWave(CurrentStage, out eStage prevStage);
    
			//스테이지의 이동이 없었을 때
			if (result == eStageResult.None) 
				return result;
			_currentStage = prevStage;
			StageNumber = StageRule.GetStageNumber(CurrentStage);
			WaveNumber = StageRule.GetWaveNumber(CurrentStage);
			IsBossWave = false;
			IsLoopMode = true;
            
			return result;
		}
    
		public void EnterBossWave()
		{
			_currentStage = StageRule.GetBossStage(CurrentStage);
			StageNumber = StageRule.GetStageNumber(CurrentStage);
			WaveNumber = StageRule.GetWaveNumber(CurrentStage);
			IsBossWave = true;
		}

		
		private void GoToNextStage()
		{
			eStage nxtStage;
			eStageResult res = StageRule.GetNextWave(_currentStage, out nxtStage);

			/* Dummy 로직. 서버 스테이지 클리어 처리는 추후 연결. */

			//NetworkManager.Instance.OnStageClear(OnStageClearSuccess, OnError);
			// Stage가 바뀌는 경우 리소스 로드가 필요하다.
			if (res == eStageResult.StageChanged)
			{
				CustomLogger.Log($"Go To Next Stage");
				LoadManager.Instance.LoadStage(_currentStage, nxtStage, SpawnStageMonster);
			}
			else
			{
				CustomLogger.Log($"Go To Next Wave");
				// 체력 회복
				SpawnStageMonster(nxtStage);
				// Todo: 캐릭터 HP 회복
			}
		}

		// ── [WaveManager 추가] 웨이브 재시작 시 몬스터 카운트 리셋 ──
		// WaveManager가 이전 웨이브로 복귀하거나 같은 웨이브 재시작 전 호출.
		// StartStage 내부의 _totalCnt += count가 누적되는 문제를 막는다.
		public void ResetWaveCount()
		{
			_totalCnt = 0;
			_totalCharacterCnt = 3;
			_waveCleared = false;
		}
		// ── [WaveManager 추가 끝] ──

		// 캐릭터가 모두 사망하면 이전 웨이브로 되돌린다.
		public void DecrementCharacterCount()
		{
			--_totalCharacterCnt;
			if (_totalCharacterCnt <= 0)
			{
				_IsLoop = true;
				eStage prevStage;
				StageRule.GetPreviousWave(_currentStage, out prevStage);
				CustomLogger.Log($"GoTo Prev Wave");

				SpawnStageMonster(prevStage);
			}
		}
	
		private void SendHuntResult()
		{
			if (_huntResultList.Count <= 0)
			{
				//Debug.Log($"[StageManager Send] Buffer Is Empty.");
				return;
			}

			// 사냥 결과를 전송용 DTO로 변환
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
				//Debug.Log($"[StageManager Send] Type : {type} | count : {count}");
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