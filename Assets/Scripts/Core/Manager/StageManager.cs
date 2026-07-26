using Newtonsoft.Json;
using PlayFab.CloudScriptModels;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using Scripts.Monster.SO;
using Scripts.Server.DTO;
using System;
using System.Collections.Generic;
using System.Threading;
using Core.Stage;
using Cysharp.Threading.Tasks;
using Scripts.Users;
using UnityEngine;

namespace Scripts.Core.Manager
{
	using Monster = Scripts.Monster.Monster;
	public enum eStageResult
	{
		None, // 변경 없음
		WaveChanged,
		BossWaveEntered,
		StageChanged,
	}

	public enum eStageRunState
	{
		None,
		Transitioning,
		Running,
		DefeatPending,
		ResultPending,
		Exiting
	}
	//현재 진행중인 스테이지(메인스테이지 + 던전 총체)의 정보
	public sealed class StageRunContext
	{
		public StageDefinition Definition { get; }
		public StageSession Session { get; }
		public IStageRule Rule { get; }
		public eStageRunState State { get; }
	}
	//다른 던전에 입장했다가 복귀 시 재시작하기 위한 값
	public sealed class MainStageSnapshot
	{
		public eStage StageId;
		public bool LoopMode;
		public bool BossAutoChallenge;
	}
	/// <summary>
	/// 스테이지 진입, 웨이브 진행, 클리어/패배 분기와 전환 흐름을 관리한다
	/// 
	/// <br/>BeginStage() → StartWave() 순서로 웨이브를 시작하고,
	/// StageSession의 모든 몬스터가 처치되면 ClearWave()에서 다음 흐름을 결정한다
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
        public event Action OnRewardPopupShow;
        public event Action<float> OnDeathPopupTick;
        public event Action<float> OnBossTimerTick;
        public event Action<StageDefinition> OnStageCleared;
        public event Action<StageDefinition> OnStageEnter;
        public event Action<StageDefinition, Monster> OnMonsterKilled;
		#endregion
		#region 필드 / 상태

		public eStage GoldDungeonProgress = eStage.GoldDungeon2_1;
		public eStage RubyDungeonProgress = eStage.RubyDungeon3_1;

        [SerializeField]
        private StageDatabaseSO _stageDatabaseSO;
        [SerializeField]
        private MonsterSpawnLocationSO _locationSO;
        [SerializeField]
        private float _bossTimeLimit = 30f;
        
        public eStage CurrentStage => _currentStage;
        public StageDefinition CurrentDefinition => _currentSession?.Definition;
        public int CurrentStageNumber => _currentStageNumber;
        public int CurrentWaveNumber => _currentWaveNumber;
        public eStageRunState CurrentRunState => _currentState;
        public bool IsBossWave => _isBossWave;
        public bool IsLoopMode => _isLoopMode;
        public bool BossAutoChallenge => _bossAutoChallenge;
        public eStage MaxClearedStage => maxStage;

        private const float DefeatPopupDuration = 15f;
        private const float TickInterval = 3f;
        
        private eStage _currentStage;
        private int _currentStageNumber;
        private int _currentWaveNumber;
        private bool _isBossWave;
        private bool _isLoopMode;
        private int _totalCharacterCnt;
        private bool _bossAutoChallenge;
        private StageSession _currentSession;
        private CancellationTokenSource _token;
        private MainStageSnapshot _mainStageSnapshot;
        private eStageRunState _currentState;
        private StageSpawnController _currentSpawnController;
        private eStage maxStage; // 현재 최대 진행된 스테이지 확인(서버붙이기 전까지 사용)
        
        // 서버 전송용 사냥 결과 버퍼.
        private Dictionary<eMonsterType, int> _huntResultList;
        private List<HuntResult> _sendmsg;

        private float _bossTimer;
        private bool _bossTimerActive;

        private bool _defeatPopupActive;
        private bool _defeatPopupHandled;
        private float _defeatPopupTimer;

		#endregion

		private IStageDefinitionProvider _provider;

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
	        _currentSession?.Tick(Time.deltaTime);
	        _currentSpawnController?.Tick(Time.deltaTime);
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
			if (_stageDatabaseSO != null)
			{
				// 새 엑셀 생성 경로에서는 StageDatabaseSO가 모든 콘텐츠의 Definition 원본이 된다.
				_stageDatabaseSO.Init();
				_provider = new StageDefinitionProvider(_stageDatabaseSO);
			}
			else
			{
				Debug.LogError("[StageManager] StageDatabaseSO가 연결되지 않았습니다.");
			}

			_huntResultList = new Dictionary<eMonsterType, int>();
			_sendmsg = new List<HuntResult>();
			_currentState = eStageRunState.None;
		}
		/// <summary>사냥 결과 자동 전송 루프를 중단하고 토큰을 정리한다</summary>
		private void OnDestroy()
		{
			_token?.Cancel();
			_token?.Dispose();
			_token = null;
		}
		#endregion

		#region 외부 접근 메서드

		#if UNITY_EDITOR
		public bool TestClearStage()
		{
			if (_currentState != eStageRunState.Running ||
			    _currentSession == null)
			{
				return false;
			}

			StageDefinition definition = _currentSession.Definition;
			StageRuleResult result;

			if (definition.FlowType == eStageFlowType.MainProgression)
			{
				MainStageRule.GetNextWave(
					definition.Id,
					out eStage nextStage);

				result = StageRuleResult.MoveTo(nextStage);
			}
			else
			{
				result = StageRuleResult.ShowResult;
			}

			return _currentSession.TestPublishResult(result);
		}

		#endif
		public bool IsDungeonStageUnlocked(eStage stage)
		{
			eStage progress = StageParser.GetStageType(stage) switch
			{
				eStageType.GoldDungeon => GoldDungeonProgress,
				eStageType.RubyDungeon => RubyDungeonProgress,
				_ => throw new ArgumentException("던전 스테이지가 아닙니다.")
			};

			return StageParser.GetStageNumber(stage)
			       <= StageParser.GetStageNumber(progress);
		}

		public void EnterGoldDungeon()
		{
			TryEnterDungeon(eStage.GoldDungeon1_1);
		}

		public void EnterRubyDungeon()
		{
			TryEnterDungeon(eStage.RubyDungeon1_1);
		}

		public bool TryEnterDungeon(eStage stage)
		{
			if (_currentState != eStageRunState.Running ||
			    _currentSession?.Definition.Type != eStageType.Main)
			{
				return false;
			}

			eStageType type = StageParser.GetStageType(stage);
			if (type != eStageType.GoldDungeon &&
			    type != eStageType.RubyDungeon)
			{
				return false;
			}

			if (!IsDungeonStageUnlocked(stage))
				return false;

			CaptureMainStageSnapshot();
			return TransitionStage(stage);
		}

		public void ReturnToMainStage()
		{
			if (StageParser.GetStageType(_currentStage) == eStageType.Main || _currentState == eStageRunState.Transitioning) return;
			Time.timeScale = 1f;
			if (_mainStageSnapshot == null)
			{
				Debug.LogError("[Stage] MainStageSnapshot이 없습니다.");
				return;
			}

			_isLoopMode = _mainStageSnapshot.LoopMode;
			_bossAutoChallenge = _mainStageSnapshot.BossAutoChallenge;
			TransitionStage(_mainStageSnapshot.StageId);
		}

		public void RestartDungeon()
		{
			if (_currentState != eStageRunState.ResultPending || _currentSession.Definition.Type == eStageType.Main) 
				return;
			
			RestartStage();
		}

		public bool TryResetMainProgress()
		{
			if (_currentState != eStageRunState.Running ||
			    _currentSession?.Definition.Type != eStageType.Main)
			{
				return false;
			}

			// TODO(서버 연동):
			// TransitionStage의 true는 전환 요청 수락을 의미하며 비동기 리소스 로딩 완료를 보장하지 않는다.
			// 추후 StartStage 완료를 확인한 뒤 서버 환생 트랜잭션을 확정해야 한다.
			if (!TransitionStage(eStage.Stage1_1))
				return false;

			_mainStageSnapshot = null;
			StopLoop();
			SetBossAutoChallenge(false);

			return true;
		}
		#endregion
		#region 스테이지 시작

		private void CaptureMainStageSnapshot()
		{
			_mainStageSnapshot = new MainStageSnapshot()
			{
				BossAutoChallenge = _bossAutoChallenge,
				LoopMode = _isLoopMode,
				StageId = _currentStage
			};
		}
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
			TransitionStage(_currentStage);
        }		
		
		/// <summary>
		/// 현재 스테이지 값으로 웨이브를 시작한다. UI 갱신, 보스 타이머, 세션 생성, 몬스터 스폰을 수행한다
		/// </summary>
        private void StartStage(StageDefinition definition)
        {
			_bossTimerActive = false;
			_defeatPopupActive = false;
			Time.timeScale = 1f;

			ReviveAllPlayers();
			OnWaveChanged?.Invoke(_currentStageNumber, _currentWaveNumber, _isBossWave);

			if (_isBossWave)
			{
				_bossTimer = _bossTimeLimit;
				_bossTimerActive = true;
			}

			StageSession session = BuildSession(definition);
			_currentSpawnController = new StageSpawnController();
			_currentSpawnController.Begin(_currentSession, _locationSO);
			_currentState = eStageRunState.Running;
			OnStageEnter?.Invoke(definition);
        }

		#endregion

		#region 스테이지 진행
		private void NotifyStageCleared(StageSession session)
		{
			StageDefinition definition =
				session.Definition;

			UpdateMaxClearedStage(definition);
			OnStageCleared?.Invoke(definition);
		}
		private bool TransitionStage(eStage target)
		{
			Debug.Log($"{target}");
			if (_currentState == eStageRunState.Transitioning) return false;
			
			if (!_provider.TryGet(target, out StageDefinition definition))
			{ 
				Debug.LogError($"StageDefinition을 찾을 수 없습니다. Stage: {target}");
				return false;
			}
			_currentState = eStageRunState.Transitioning;
			eStage previous = _currentStage;
			bool requiresLoad =
				StageParser.GetResourceGroupId(previous) !=
				StageParser.GetResourceGroupId(target);

			EndSession();
			SetCurrentStage(definition.Id);
			CameraFade fade = CameraFade.Instance;

			if (requiresLoad)
			{
				if (fade != null)
				{
					fade.FadeOut(0.4f, () =>
					{
						LoadManager.Instance.LoadStage(
							previous,
							target,
							_ =>
							{
								StartStage(definition);
								fade.FadeIn(0.4f);
							});
					});
				}
				else
				{
					LoadManager.Instance.LoadStage(
						previous,
						target,
						_ => StartStage(definition));
				}

				return true;
			}
			if (fade != null)
			{
				fade.FadeOutIn(
					0.3f,
					0.3f,
					onDark: () => StartStage(definition));
			}
			else
			{
				StartStage(definition);
			}
			return true;
		}

		private bool ShouldRestart(eStage target)
		{
			if (StageParser.GetStageType(_currentStage) != eStageType.Main) return false;
			
			if (!_bossAutoChallenge &&StageParser.IsBossWave(target)) return true;
			if (_isLoopMode && !StageParser.IsBossWave(_currentStage)) return true;

			return false;
		}
		public void HandleRuleResult(StageSession session, StageRuleResult result)
		{
			switch (result.Action)
			{
				case eStageFlowAction.MoveToStage:
					// StageRuleResult의 생성 규칙상 MoveToStage는 항상 TargetStage를 가진다.
					eStage target = result.TargetStage.Value;
					NotifyStageCleared(session);
					if (session.Definition.Type == eStageType.Main && ShouldRestart(target))
					{
						RestartStage();
						break;
					}
					
					Debug.Log($"TransitionStage {target}");
					TransitionStage(target);					
					break;
				case eStageFlowAction.RestartStage:
					Debug.Log("스테이지를 재시작 합니다");
					RestartStage();
					break;
				case eStageFlowAction.ShowResult:
					Debug.Log("스테이지 클리어 후 결과를 보고 있습니다");
					NotifyStageCleared(session);
					EnterResultPending();
					break;
				case eStageFlowAction.AwaitDefeatChoice:
					Debug.Log("스테이지 패배 후 작업을 선택중입니다");
					EnterDefeatPending();
					break;
				case eStageFlowAction.ReturnToMainStage:
					Debug.Log("메인 스테이지로 복귀합니다");
					ReturnToMainStage();
					break;
			}
		}

		/// <summary>
		/// 플레이어 전멸 또는 보스 제한 시간 초과 시 패배 상태로 전환한다.
		/// <br/>이후 선택 팝업에서 재도전/복귀 흐름으로 분기된다
		/// </summary>
        public void DefeatWave()
        {
			_currentSession.NotifyPartyDefeated();
        }
		private void EnterDefeatPending()
		{
			_bossTimerActive = false;
			_currentState = eStageRunState.DefeatPending;

			Time.timeScale = 0f;

			_defeatPopupActive = true;
			_defeatPopupHandled = false;
			_defeatPopupTimer = DefeatPopupDuration;

			OnDefeatPopupShow?.Invoke();
		}

		private void EnterResultPending()
		{
			_currentState = eStageRunState.ResultPending;
			Time.timeScale = 0f;
			
			OnRewardPopupShow?.Invoke();
		}

		/// <summary>패배 팝업 선택을 처리한다. 보스 패배는 이전 웨이브로 복귀하고, 일반 패배는 선택에 따라 분기한다</summary>
        public void ChooseDefeatAction(bool retryCurrentWave)
        {
			if (_defeatPopupHandled) return;
			_defeatPopupHandled = true;
			_defeatPopupActive = false;
			OnDefeatPopupHide?.Invoke();
			if (_currentSession.Definition.Type != eStageType.Main)
			{
				if (retryCurrentWave)
					RestartStage();
				else
					ReturnToMainStage();
				return;
			}
			if (_isBossWave)
			{
				MovePrevWave();
				SetBossAutoChallenge(false);
				SetLoopMode(true);
				OnLoopModeChanged?.Invoke(true);

				TransitionStage(_currentStage);
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
				
				TransitionStage(_currentStage);
				SetLoopMode(true);
				OnLoopModeChanged?.Invoke(true);
				return;
			}
			RestartStage();
        }
		
        /// <summary>패배 후 현재 상태값 기준으로 웨이브를 다시 시작한다. 재도전/복귀 결정은 호출 전에 끝난다</summary>
        private void RestartStage()
        {
        	var fade = CameraFade.Instance;
	        _currentState = eStageRunState.Transitioning;
        	Time.timeScale = 1f;
        	StartStage(_currentSession.Definition);

        	if (fade != null)
        		fade.FadeIn(0.4f);
        }

		#endregion

		#region 던전 조작

		public void ContinueDungeon()
		{
			if (_currentState != eStageRunState.ResultPending)
				return;

			StageDefinition definition = _currentSession?.Definition;

			if (definition == null || definition.Type == eStageType.Main || !definition.NextDifficultyId.HasValue)
				return;

			Time.timeScale = 1f;
			TransitionStage(definition.NextDifficultyId.Value);
		}
		#endregion
		#region 스테이지 상태 변경
		
		/// <summary>스테이지 값을 지정하고 진행 상태를 기본값으로 초기화한다</summary>
		public void ResetStage(eStage stage)
		{
			_currentStage = stage;
			_currentStageNumber = StageParser.GetStageNumber(stage);
			_currentWaveNumber = StageParser.GetWaveNumber(stage);
			_isBossWave = StageRule.IsBossWave(stage);

			_isLoopMode = false;
			_bossAutoChallenge = true;
		}

		/// <summary>내부 스테이지 값을 다음 웨이브로 이동한다. 보스 클리어 후에는 다음 스테이지의 1웨이브가 될 수 있다</summary>
		private eStageResult MoveNextWave()
		{
			eStageResult result = StageRule.GetNextWave(CurrentStage, out eStage nextStage);

			_currentStage = nextStage;
			_currentStageNumber = StageParser.GetStageNumber(CurrentStage);
			_currentWaveNumber = StageParser.GetWaveNumber(CurrentStage);
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
			_currentStageNumber = StageParser.GetStageNumber(CurrentStage);
			_currentWaveNumber = StageParser.GetWaveNumber(CurrentStage);
			_isBossWave = false;
			_isLoopMode = true;

			return result;
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
		private StageSession BuildSession(StageDefinition definition)
        {
			EndSession();
			
			IStageRule rule = StageRuleFactory.Create(definition);
			StageSession session = new StageSession(
				definition,
				rule,
				MonsterSpawner.Instance
				);
			
			session.OnMonsterKilled += HandleMonsterKilled;
			session.OnResultProduced += HandleRuleResult;
			
			_currentSession = session;
			session.Enter();
			return session;
        }
		private void SetCurrentStage(eStage stage)
		{
			_currentStage = stage;

			_currentStageNumber = StageParser.GetStageNumber(stage);
			_currentWaveNumber = StageParser.GetWaveNumber(stage);

			_isBossWave = StageParser.IsBossWave(stage);
		}


		public bool IsStageCleared(eStage stage)
		{
			return (ulong)stage <= (ulong)maxStage;
		}

		private void UpdateMaxClearedStage(StageDefinition definition)
		{
			switch (definition.Type)
			{
				case eStageType.Main:
					var clearedStage = definition.Id;
					if ((ulong)clearedStage > (ulong)maxStage)
						maxStage = clearedStage;
					break;
				case eStageType.GoldDungeon:
					AdvanceDungeonProgress(ref GoldDungeonProgress, definition);
					break;
				case eStageType.RubyDungeon:
					AdvanceDungeonProgress(ref RubyDungeonProgress, definition);
					break;
			}
		}

		private static void AdvanceDungeonProgress(
			ref eStage progress,
			StageDefinition definition)
		{
			if (!definition.NextDifficultyId.HasValue)
				return;

			eStage nextStage = definition.NextDifficultyId.Value;
			if (StageParser.GetStageNumber(nextStage) >
			    StageParser.GetStageNumber(progress))
			{
				progress = nextStage;
			}
		}

		private void HandleMonsterKilled(StageSession session, Monster monster)
		{
			CountKill(monster);
			OnMonsterKilled?.Invoke(session.Definition, monster);
		}
		/// <summary>현재 세션의 이벤트를 해제하고 남은 몬스터를 정리한다</summary>
		private void EndSession()
		{
			if (_currentSession == null)
			{
				return;
			}

			_currentSession.OnMonsterKilled -= HandleMonsterKilled;
			_currentSession.OnResultProduced -= HandleRuleResult;
			_currentSession.Exit();
			_currentSpawnController.Stop();
		}
		/// <summary>스테이지 메타 데이터와 로드 그룹을 모아 StageDefinition을 만든다</summary>
		private StageDefinition BuildDefinition(eStage stage)
		{
			return _provider != null && _provider.TryGet(stage, out StageDefinition definition)
				? definition : null;		
		}
		/// <summary>현재 StageDefinition의 몬스터 목록을 실제 필드에 스폰하고 세션에 등록한다</summary>
		private void SpawnMonsters(StageSession session)
		{
			int locationCount = _locationSO.GetLocationCount();
			StageDefinition definition = session.Definition;
			foreach (StageMonsterEntry entry in definition.MonsterEntries)
			{
				for (int i = 0; i < entry.Count; i++)
				{
					// SpawnPointSetId/SpawnPointGroupId 프리셋 연결 전까지는 기존 위치 후보군을 사용한다.
					// 위치 데이터가 연결되면 이 좌표 선택 부분만 전용 Resolver로 교체하면 된다.
					int index = UnityEngine.Random.Range(0, locationCount);
					_locationSO.TryGetPos(index, out Vector2 position);
					MonsterSpawner.Instance.SpawnMonster(
						entry.MonsterType,
						definition.MonsterStatMultiplier,
						position,
						Quaternion.identity,
						out Monster monster);
					if (monster != null)
					{
						session.RegisterMonster(monster);
					}
				}
			}
			session.CompleteSpawning();
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
	        if (_stageDatabaseSO != null &&
	            _stageDatabaseSO.TryGetMonsterTypes(stage, out IReadOnlyList<eMonsterType> databaseTypes))
	        {
		        var types = new eMonsterType[databaseTypes.Count];
		        for (int i = 0; i < databaseTypes.Count; i++)
			        types[i] = databaseTypes[i];

		        return MonsterSpawner.Instance.LoadMonsterAssets(stage, types);
	        }
	        
			return default;
        }
		
        /// <summary>스테이지에 필요한 몬스터 타입 목록을 조회한다</summary>
        public List<eMonsterType> GetStageMonsterTypes(eStage stage)
        {
			if (_stageDatabaseSO != null &&
			    _stageDatabaseSO.TryGetMonsterTypes(stage, out IReadOnlyList<eMonsterType> databaseTypes))
			{
				return new List<eMonsterType>(databaseTypes);
			}
			CustomLogger.LogWarning(
				$"Stage monster types were requested, but they were not found in cache. Stage: {stage}");
			return null;
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
