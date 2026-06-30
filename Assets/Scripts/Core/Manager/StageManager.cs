using Newtonsoft.Json;
using PlayFab.CloudScriptModels;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using Scripts.Monster.SO;
using Scripts.Server.DTO;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Scripts.Users;
using UnityEngine;
using static Scripts.Core.SO.StageMetaDataSO;

namespace Scripts.Core
{
	using Monster = Scripts.Monster.Monster;
	public enum eStageResult
	{
		None, // 변경 없음
		WaveChanged,
		BossWaveEntered,
		StageChanged,
	}
	/// <summary>
	/// 스테이지 진입, 웨이브 진행, 클리어/패배 분기와 전환 흐름을 관리한다
	/// 
	/// <br/>BeginStage() → StartWave() 순서로 웨이브를 시작하고,
	/// StageSession의 모든 몬스터가 처치되면 ClearWave()에서 다음 흐름을 결정한다
	/// <br/>일반 웨이브는 AdvanceWave(), 보스 클리어 후 스테이지 전환은 AdvanceStage()로 진행한다
	/// <br/>플레이어 전멸 또는 보스 제한 시간 초과 시 DefeatWave()로 패배 상태에 진입하고,
	/// ChooseDefeatAction() 결과에 따라 RetryWave() 또는 이전 웨이브 복귀로 분기한다
	/// 
	/// <br/>정적 정보는 StageDefinition, 진행 중인 몬스터 상태는 StageSession,
	/// 스테이지/웨이브 계산은 StageRule이 담당한다
	/// </summary>
	public class StageManager : MonoBehaviour
	{
		#region 싱글톤 / 이벤트
		public static StageManager Instance;
        		public event Action<int, int, bool> OnWaveChanged;
        		public event Action<bool> OnLoopModeChanged;
        		public event Action<bool> OnBossAutoChallengeChanged;
        		public event Action OnDefeatPopupShow;
        		public event Action OnDefeatPopupHide;
        		public event Action<float> OnDeathPopupTick;
        		public event Action<float> OnBossTimerTick;
		#endregion
		#region 필드 / 상태

		[SerializeField]
        private StageMetaDataSO _stageSO;
        [SerializeField]
        private MonsterSpawnLocationSO _locationSO;
        [SerializeField]
        private float _bossTimeLimit = 30f;
        
        public eStage CurrentStage => _currentStage;
        public int StageNumber => _stageNumber;
        public int WaveNumber => _waveNumber;
        public bool IsBossWave => _isBossWave;
        public bool IsLoopMode => _isLoopMode;
        public bool BossAutoChallenge => _bossAutoChallenge;

        private const float DefeatPopupDuration = 15f;
        private const float TickInterval = 3f;
        
        private eStage _currentStage;
        private int _stageNumber;
        private int _waveNumber;
        private bool _isBossWave;
        private bool _isLoopMode;
        private int _totalCharacterCnt;
        private bool _bossAutoChallenge;
        private StageSession _currentSession;
        private StageDefinition _currentDefinition;
        private CancellationTokenSource _token;

        // 서버 전송용 사냥 결과 버퍼.
        private Dictionary<eMonsterType, int> _huntResultList;
        private List<HuntResult> _sendmsg;

        private float _bossTimer;
        private bool _bossTimerActive;

        private bool _defeatPopupActive;
        private bool _defeatPopupHandled;
        private float _defeatPopupTimer;

		#endregion

		#if UNITY_EDITOR
		[ContextMenu("Clear Wave")]
		private void DebugClearWave()
		{
			ClearWave();
		}
		#endif
		#region 유니티 생명주기
		/// <summary>싱글톤을 구성하고 스테이지 메타 데이터를 초기화한다.</summary>
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
		/// <summary>보스 제한 시간과 패배 팝업 선택 제한 시간을 갱신한다.</summary>
		private void Update()
        {
            if (_bossTimerActive)
            {
				_bossTimer -= Time.deltaTime;
				OnBossTimerTick?.Invoke(Mathf.Clamp01(_bossTimer / _bossTimeLimit));
				if (_bossTimer <= 0f)
				{
					_bossTimerActive = false;
					DefeatWave();
				}
            }

            if (_defeatPopupActive)
            {
				_defeatPopupTimer -= Time.unscaledDeltaTime;
				OnDeathPopupTick?.Invoke(Mathf.Clamp01(_defeatPopupTimer / DefeatPopupDuration));
				if (_defeatPopupTimer <= 0f)
				{
					_defeatPopupActive = false;
					ChooseDefeatAction(false);
				}
            }
        }
		/// <summary>StageManager가 사용하는 데이터 버퍼를 초기화한다</summary>
		private void Init()
		{
			_stageSO.Init();
			_huntResultList = new Dictionary<eMonsterType, int>();
			_sendmsg = new List<HuntResult>();
		}
		/// <summary>사냥 결과 자동 전송 루프를 중단하고 토큰을 정리한다</summary>
		private void OnDestroy()
		{
			_token?.Cancel();
			_token?.Dispose();
			_token = null;
		}
		#endregion

		#region 스테이지 시작

		/// <summary>
		/// 메인 씬 진입 후 호출되는 시작점이다. 스테이지 상태를 초기화하고 첫 웨이브를 시작한다
		/// </summary>
        public void BeginStage(eStage stage)
        {
	        _token?.Cancel();
	        _token?.Dispose();
			_token = new CancellationTokenSource();
			SendHuntResultLoop(_token.Token).Forget();
			ResetStage(stage);
			ReviveAllPlayers();
			StartWave(_currentStage);
        }		
		
		/// <summary>
		/// 현재 스테이지 값으로 웨이브를 시작한다. UI 갱신, 보스 타이머, 세션 생성, 몬스터 스폰을 수행한다
		/// </summary>
        private void StartWave(eStage stage)
        {
			_bossTimerActive = false;
			_defeatPopupActive = false;
			Time.timeScale = 1f;

			ReviveAllPlayers();
			OnWaveChanged?.Invoke(_stageNumber, _waveNumber, _isBossWave);

			if (_isBossWave)
			{
				_bossTimer = _bossTimeLimit;
				_bossTimerActive = true;
			}

			if (!BuildSession(stage))
			{
				CustomLogger.LogError($"[StageManager] CreateStageSession Failed : {stage}");
				return;
			}
			SpawnMonsters(stage);
        }

		#endregion

		#region 스테이지 진행
		
		/// <summary>
        /// 웨이브 클리어 후 다음 진행을 결정한다. 일반 웨이브는 AdvanceWave, 보스 클리어는 AdvanceStage로 분기한다
        /// </summary>
        public void ClearWave()
        {
        	eStageResult result = StageRule.GetNextWave(_currentStage, out var nextStage);
        	switch (result)
        	{
        		case eStageResult.StageChanged:
        			AdvanceStage();
        			break;
        		case eStageResult.BossWaveEntered:
        			if (!_bossAutoChallenge)
        			{
        				SetLoopMode(true);
        				OnLoopModeChanged?.Invoke(true);
        			}
        			AdvanceWave();
        			break;
        		case eStageResult.WaveChanged:
        			SetLoopMode(false);
        			OnLoopModeChanged?.Invoke(false);
        			AdvanceWave();
        			break;
        		default:
        			CustomLogger.LogError($"[StageManager]: 스테이지 이동 실패 {result}");
        			break;
        	}
        }
		/// <summary>
		/// 플레이어 전멸 또는 보스 제한 시간 초과 시 패배 상태로 전환한다.
		/// <br/>이후 선택 팝업에서 재도전/복귀 흐름으로 분기된다
		/// </summary>
        public void DefeatWave()
        {
			_bossTimerActive = false;
			Time.timeScale = 0f;
			_defeatPopupActive = true;
			_defeatPopupHandled = false;
			_defeatPopupTimer = DefeatPopupDuration;
			OnDefeatPopupShow?.Invoke();
        }
		/// <summary>패배 팝업 선택을 처리한다. 보스 패배는 이전 웨이브로 복귀하고, 일반 패배는 선택에 따라 분기한다</summary>
        public void ChooseDefeatAction(bool retryCurrentWave)
        {
			if (_defeatPopupHandled) return;
			_defeatPopupHandled = true;
			_defeatPopupActive = false;
			OnDefeatPopupHide?.Invoke();

			if (_isBossWave)
			{
				MovePrevWave();

				SetBossAutoChallenge(false);
				SetLoopMode(true);
				OnBossAutoChallengeChanged?.Invoke(false);
				OnLoopModeChanged?.Invoke(true);

				RetryWave();
				return;
			}

			if (retryCurrentWave)
			{
				SetLoopMode(false);
				OnLoopModeChanged?.Invoke(false);
			}
			else
			{
				MovePrevWave();

				SetLoopMode(true);
				OnLoopModeChanged?.Invoke(true);
			}
			RetryWave();
        }
        /// <summary>같은 맵 안에서 다음 웨이브로 진행한다. 반복 모드가 아니면 내부 웨이브 값을 먼저 이동한다.</summary>
        private void AdvanceWave()
        {
        	var fade = CameraFade.Instance;
        	if (!_isLoopMode)
                MoveNextWave();
        	if (fade != null)
        	{
        		fade.FadeOutIn(0.3f, 0.3f, onDark: () => StartWave(_currentStage));
        	}
        	else
        	{
        		StartWave(_currentStage);
        	}
        }
        /// <summary>보스 클리어 후 다음 큰 스테이지로 진행한다. LoadManager로 맵 리소스를 교체한 뒤 시작한다.</summary>
        private void AdvanceStage()
        {
        	_bossTimerActive = false;

        	eStage prevStage = _currentStage;
        	MoveNextWave();
        	var fade = CameraFade.Instance;
        	if (fade != null)
        	{
        		fade.FadeOut(0.4f, () =>
        		{
        			LoadManager.Instance.LoadStage(prevStage, _currentStage, (stage) =>
        			{
        				StartWave(_currentStage);
        				fade.FadeIn(0.4f);
        			});
        		});
        	}
        	else
        	{
        		StartWave(_currentStage);
        	}
        }
        /// <summary>패배 후 현재 상태값 기준으로 웨이브를 다시 시작한다. 재도전/복귀 결정은 호출 전에 끝난다</summary>
        private void RetryWave()
        {
        	var fade = CameraFade.Instance;
        	ReviveAllPlayers();

        	Time.timeScale = 1f;
        	StartWave(_currentStage);

        	if (fade != null)
        		fade.FadeIn(0.4f);
        }

		#endregion

		#region 스테이지 상태 변경
		
		/// <summary>스테이지 값을 지정하고 진행 상태를 기본값으로 초기화한다</summary>
		public void ResetStage(eStage stage)
		{
			_currentStage = stage;
			_stageNumber = StageRule.GetStageNumber(stage);
			_waveNumber = StageRule.GetWaveNumber(stage);
			_isBossWave = StageRule.IsBossWave(stage);

			_isLoopMode = false;
			_bossAutoChallenge = true;
		}

		/// <summary>내부 스테이지 값을 다음 웨이브로 이동한다. 보스 클리어 후에는 다음 스테이지의 1웨이브가 될 수 있다</summary>
		private eStageResult MoveNextWave()
		{
			eStageResult result = StageRule.GetNextWave(CurrentStage, out eStage nextStage);

			_currentStage = nextStage;
			_stageNumber = StageRule.GetStageNumber(CurrentStage);
			_waveNumber = StageRule.GetWaveNumber(CurrentStage);
			_isBossWave = result == eStageResult.BossWaveEntered;

			if (result == eStageResult.WaveChanged)
				_isLoopMode = false;

			return result;
		}
		
		/// <summary>내부 스테이지 값을 이전 웨이브로 이동하고 반복 모드로 전환한다</summary>
		private eStageResult MovePrevWave()
		{
			eStageResult result = StageRule.GetPreviousWave(CurrentStage, out eStage prevStage);

			if (result == eStageResult.None)
				return result;
			_currentStage = prevStage;
			_stageNumber = StageRule.GetStageNumber(CurrentStage);
			_waveNumber = StageRule.GetWaveNumber(CurrentStage);
			_isBossWave = false;
			_isLoopMode = true;

			return result;
		}
		
		/// <summary>현재 맵의 보스 웨이브 값으로 즉시 이동한다</summary>
		public void EnterBossWave()
        {
			_currentStage = StageRule.GetBossStage(CurrentStage);
			_stageNumber = StageRule.GetStageNumber(CurrentStage);
			_waveNumber = StageRule.GetWaveNumber(CurrentStage);
			_isBossWave = true;
        }
		
		/// <summary>반복 모드 상태값만 변경한다. UI 알림은 호출자가 처리한다</summary>
		public void SetLoopMode(bool value)
		{
			_isLoopMode = value;
		}
		
        /// <summary>반복 모드를 해제하고 UI에 변경을 알린다</summary>
        public void StopLoop()
        {
			SetLoopMode(false);
			OnLoopModeChanged?.Invoke(false);
        }
        
        /// <summary>보스 자동 도전 여부를 변경하고 UI에 알린다</summary>
        public void SetBossAutoChallenge(bool value)
        {
			CustomLogger.Log($"[StageManager] : BossAutoChallenge is Changed : {value}");
			_bossAutoChallenge = value;
			OnBossAutoChallengeChanged?.Invoke(value);
        }

		#endregion

		#region 스테이지 세션

		/// <summary>현재 웨이브용 StageSession을 생성한다. 기존 세션을 종료하고 처치/클리어 이벤트를 연결한다</summary>
		private bool BuildSession(eStage stage)
        {
			EndSession();

			_currentDefinition = BuildDefinition(stage);
			if (_currentDefinition == null)
			{
				CustomLogger.LogWarning($"[StageManager] : Create Session if Failed: {stage}");
				return false;
			}

			_currentSession = new StageSession(
				_currentDefinition,
				MonsterSpawner.Instance
				);

			_currentSession.MonsterKilled += CountKill;
			_currentSession.Cleared += ClearWave;
			_currentSession.Enter();
			return true;
        }
		/// <summary>현재 세션의 이벤트를 해제하고 남은 몬스터를 정리한다</summary>
		private void EndSession()
		{
			if (_currentSession == null)
			{
				return;
			}

			_currentSession.MonsterKilled -= CountKill;
			_currentSession.Cleared -= ClearWave;
			_currentSession.Exit();

			_currentSession = null;
			_currentDefinition = null;
		}
		/// <summary>스테이지 메타 데이터와 로드 그룹을 모아 StageDefinition을 만든다</summary>
		private StageDefinition BuildDefinition(eStage stage)
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
		/// <summary>현재 StageDefinition의 몬스터 목록을 실제 필드에 스폰하고 세션에 등록한다</summary>
		private void SpawnMonsters(eStage stage)
		{
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
		/// <summary>몬스터 처치 수를 서버 전송용 버퍼에 누적한다</summary>
		private void CountKill(Monster monster)
		{
			_huntResultList.TryGetValue(monster.Type, out int count);
			_huntResultList[monster.Type] = count + 1;
		}
		
		#endregion

		#region 플레이어

		/// <summary>현재 유저의 모든 플레이어를 부활시키고 생존 카운트를 갱신한다</summary>
		public void ReviveAllPlayers()
        {
			var um = UserManager.Instance;
			if (um == null) return;
			var userField = typeof(UserManager).GetField("_user",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			if (userField == null) return;
			var user = userField.GetValue(um) as User;
			if (user?._players == null) return;

			foreach (var p in user._players)
			{
				if (p != null) p.Revive();
			}

			_totalCharacterCnt = user._players.Count;
        }

		#endregion

		#region 스테이지 데이터

		/// <summary>스테이지 진입 전 필요한 몬스터 에셋을 미리 로드한다</summary>
        public UniTask PreLoadAssets(eStage stage)
        {
			_stageSO.TryGetMonsterList(stage, out List<eMonsterType> monsterTypes);
			var handle = MonsterSpawner.Instance.LoadMonsterAssets(stage, monsterTypes.ToArray());
			return handle;
        }
        /// <summary>스테이지에 설정된 몬스터 스폰 정보를 조회한다</summary>
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
        /// <summary>스테이지에 필요한 몬스터 타입 목록을 조회한다</summary>
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

		#endregion

		#region 서버 동기화

        /// <summary>일정 주기로 누적된 사냥 결과를 서버에 전송한다</summary>
        private async UniTaskVoid SendHuntResultLoop(CancellationToken token)
        {
			while(true)
			{
				await UniTask.WaitForSeconds(TickInterval, cancellationToken:token);
				SendHuntResult();
			}
        }
		/// <summary>사냥 결과 버퍼를 DTO로 변환해 서버에 전송한다</summary>
		private void SendHuntResult()
		{
			if (_huntResultList.Count <= 0)
			{
				Debug.Log($"[StageManager Send] Buffer Is Empty.");
				return;
			}

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
				Debug.Log($"[StageManager Send] Type : {type} | count : {count}");
				_sendmsg.Add(code);
			}

			NetworkManager.Instance.OnHuntReward(_sendmsg, OnHuntRewardSuccess, OnError);
			_huntResultList.Clear();
			_sendmsg.Clear();
		}
		/// <summary>서버 사냥 보상 응답을 유저 데이터에 반영한다</summary>
		private void OnHuntRewardSuccess(ExecuteFunctionResult result)
		{
			string response = JsonConvert.SerializeObject(result.FunctionResult);
			OnHuntResponseDTO huntResult = JsonConvert.DeserializeObject<OnHuntResponseDTO>(response);
			UserManager.Instance.SetHuntResult(huntResult);
		}
		/// <summary>스테이지 클리어 서버 응답을 처리한다</summary>
		private void OnStageClearSuccess(ExecuteFunctionResult result)
		{
			//For Debugging
			string response = JsonConvert.SerializeObject(result.FunctionResult);
		}
		/// <summary>서버 요청 실패 메시지를 로그로 출력한다</summary>
		private void OnError(PlayFab.PlayFabError error)
		{
			Debug.Log(error.ErrorMessage);
		}

		#endregion
	}
}
