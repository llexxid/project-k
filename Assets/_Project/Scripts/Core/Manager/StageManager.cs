using KingdomIdle.Balance;
using Newtonsoft.Json;
using PlayFab.CloudScriptModels;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using Scripts.Monster.SO;
using Protocol.Message.Dto;
using System;
using System.Collections.Generic;
using System.Threading;
using Core.Stage;
using Core.Stage.Action;
using Cysharp.Threading.Tasks;
using Scripts.Users;
using UnityEngine;
using static Scripts.Core.SO.StageMetaDataSO;

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
		Entering,
		Running,
		Resolving,
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
    /// <summary>HUD만 읽는 현재 실행 값. 표시 코드가 세션이나 보상을 변경하지 않는다.</summary>
    public readonly struct StageHudSnapshot
    {
        public readonly string RunId;
        public readonly StageDefinition Definition;
        public readonly int Kills, Target;
        public readonly float RemainingTime;
        public readonly long EarnedGold;
        public readonly Monster Boss;
        public StageHudSnapshot(StageSession session)
        {
            RunId = session.RunId; Definition = session.Definition;
            Kills = session.TotalKillCount; RemainingTime = session.RemainingTime; EarnedGold = session.EarnedGold;
            int target = 0;
            foreach (var entry in Definition.MonsterEntries) target += entry.Count;
            Target = Math.Max(1, target); Boss = null;
            foreach (var monster in session.Monsters)
                if (monster != null && monster.IsBalanceBoss && monster.GetHpRatio() > 0) { Boss = monster; break; }
        }
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

        [SerializeField]
        private StageDatabaseSO _stageDatabaseSO;
        [SerializeField]
        private MonsterSpawnLocationSO _locationSO;
        [SerializeField]
        private float _bossTimeLimit = 30f;
        [Header("Dungeon Progress (temporary)")]
        [SerializeField, Min(1)] private int _goldDungeonProgress = 2;
        [SerializeField, Min(1)] private int _rubyDungeonProgress = 3;
        
        public eStage CurrentStage => _currentStage;
        public StageDefinition CurrentDefinition => _currentSession?.Definition;
        public int CurrentStageNumber => _currentStageNumber;
        public int CurrentWaveNumber => _currentWaveNumber;
        public eStageRunState CurrentRunState => _currentState;
        public bool IsBossWave => _isBossWave;
        public bool IsLoopMode => _isLoopMode;
        public bool BossAutoChallenge => _bossAutoChallenge;
        public eStage MaxClearedStage => (eStage)LocalProgression.State.HighestMainClear;
        public eStage CycleFrontierStage => (eStage)Math.Max(LocalProgression.State.MainStage, LocalProgression.State.CycleFrontierStage);

        public bool IsCycleStageCleared(eStage stage) => StageParser.GetStageType(stage) == eStageType.Main && (long)stage < (long)CycleFrontierStage;

        public bool TryGetHudSnapshot(out StageHudSnapshot snapshot)
        {
            snapshot = _currentSession == null ? default : new StageHudSnapshot(_currentSession);
            return _currentSession != null;
        }

        /// <summary>선택 UI의 명시적 이동만 처리한다. 자동 도전 설정·클리어·보상에는 관여하지 않는다.</summary>
        public bool TryMoveToMainStage(eStage target)
        {
            if (_currentState != eStageRunState.Running || CurrentDefinition?.Type != eStageType.Main ||
                LocalProgression.State.PendingReincarnation || StageParser.GetStageType(target) != eStageType.Main ||
                (long)target > (long)CycleFrontierStage || !TryGetStageDefinition(target, out var definition) || !definition.Enabled) return false;
            if (target == CurrentStage) return true;
            bool previousLoop = _isLoopMode;
            _isLoopMode = IsCycleStageCleared(target) && !StageParser.IsBossWave(target);
            if (!TransitionStage(target)) { _isLoopMode = previousLoop; return false; }
            OnLoopModeChanged?.Invoke(_isLoopMode);
            return true;
        }

        private const float DefeatPopupDuration = 15f;
        public const float DungeonReturnDuration = 6f;
        public float ReturnCountdownDuration => _currentSession?.Definition.Type != eStageType.Main ? DungeonReturnDuration : DefeatPopupDuration;
        public float ReturnCountdownRemaining => _currentState == eStageRunState.ResultPending ? _resultPopupTimer :
            _currentState == eStageRunState.DefeatPending ? _defeatPopupTimer : 0;
        private float _resultPopupTimer;
        private const float TickInterval = 3f;
        
        [NonSerialized]
        private eStage _currentStage;
        private int _currentStageNumber;
        private int _currentWaveNumber;
        private bool _isBossWave;
        private bool _isLoopMode;
        private int _totalCharacterCnt;
        private bool _bossAutoChallenge = true;
        private StageSession _currentSession;
        private CancellationTokenSource _token;
        private MainStageSnapshot _mainStageSnapshot;
        private eStageRunState _currentState;
        [NonSerialized]
        private eStage maxStage; // 현재 최대 진행된 스테이지 확인(서버붙이기 전까지 사용)
        
        // 서버 전송용 사냥 결과 버퍼. 메인 스테이지 처치만 스테이지 ID별로 모은다(던전 처치는 보내지 않는다).
        private Dictionary<long, Dictionary<eMonsterType, int>> _huntBuffer;
        // 전송 대기열. 사냥 배치와 스테이지 클리어를 발생 순서대로 한 번에 하나씩 보낸다.
        private readonly Queue<HuntOutboxItem> _huntOutbox = new Queue<HuntOutboxItem>();
        private bool _huntInFlight;
        // 오프라인 정산 중에는 사냥 배치를 보류한다(서버는 정산 전 마지막 사냥 수락 시각을 기준으로 쓴다).
        private bool _huntsHeld;
        private float _lastHuntAcceptedAt = float.NegativeInfinity;
        // 서버 규칙: 같은 세션의 직전 수락 사냥으로부터 2초 초과(eConst.HUNT_MIN_INTERVAL_MS). 여유를 둔다.
        private const float HuntMinIntervalSec = 2.2f;
        private const int HuntMaxKillsPerBatch = 149, HuntMaxTypesPerBatch = 10, HuntMaxAttempts = 10;
        // 서버가 이미 다 정산한(1007) 스테이지별 몬스터 종류. 재접속 뒤 같은 웨이브를 처음부터 다시 싸우면 이전 세션에 정산된 처치만큼 넘치므로,
        // 1007이 오면 배치를 반으로 나눠 다시 보내고 1마리까지 거절되면 그 종류는 이 스테이지에서 더 보내지 않는다.
        private readonly Dictionary<long, HashSet<ulong>> _saturatedTypes = new Dictionary<long, HashSet<ulong>>();

        private sealed class HuntOutboxItem
        {
            public bool IsClear;
            public long StageId;
            public List<HuntResult> Loots;
            public int Attempts;
        }

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
				_bossTimer = _currentSession?.RemainingTime ?? 0;
				OnBossTimerTick?.Invoke(Mathf.Clamp01(_bossTimer / _bossTimeLimit));

            }
	        _currentSession?.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            if (_defeatPopupActive)
            {
				_defeatPopupTimer -= Time.unscaledDeltaTime;
				OnDeathPopupTick?.Invoke(Mathf.Clamp01(_defeatPopupTimer / ReturnCountdownDuration));
				if (_defeatPopupTimer <= 0f)
				{
					_defeatPopupActive = false;
					ChooseDefeatAction(false);
				}
            }
            if (_currentState == eStageRunState.ResultPending && _currentSession?.Definition.Type != eStageType.Main)
            {
                _resultPopupTimer -= Time.unscaledDeltaTime;
                if (_resultPopupTimer <= 0) ReturnToMainStage();
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

			_huntBuffer = new Dictionary<long, Dictionary<eMonsterType, int>>();
			_currentState = eStageRunState.None;
		}
		/// <summary>사냥 결과 자동 전송 루프를 중단하고 토큰을 정리한다</summary>
		private void OnDestroy()
		{
			EndSession();
			_token?.Cancel();
			_token?.Dispose();
			_token = null;
		}
		#endregion

		#region 외부 접근 메서드

		#if UNITY_EDITOR || LOBBY_DEVICE_QA
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
		/// <summary>스테이지를 시작하지 않고 정적 정의만 조회한다.</summary>
		public bool TryGetStageDefinition(
			eStage stage,
			out StageDefinition definition)
		{
			definition = BuildDefinition(stage);
			return definition != null;
		}

		public bool IsDungeonStageUnlocked(eStage stage)
		{
            var s = LocalProgression.State;
            int number = StageParser.GetStageNumber(stage); var type = StageParser.GetStageType(stage);
            if (number < 1 || number > 5) return false;
            // 메인 클리어는 서버 MainClear(HighestMainClear) 이하를 모두 클리어로 본다.
            return type == eStageType.GoldDungeon ? s.HighestMainClear >= 0x20001000B && number <= s.GoldDungeonClear + 1 :
                type == eStageType.RubyDungeon && s.HighestMainClear >= 0x200020005 && number <= s.RubyDungeonClear + 1;
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
			if ((type != eStageType.GoldDungeon &&
			     type != eStageType.RubyDungeon) ||
			    (!IsDungeonStageUnlocked(stage) || BattleEconomy.Tickets(type) <= 0))
			{
				return false;
			}

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
			SetBossAutoChallenge(_mainStageSnapshot.BossAutoChallenge);
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
			// 새 입장: 이전 세션의 전송 상태를 버리고, 메인 스테이지 처치를 3초마다 서버(OnHuntReward)에 보고한다.
			// 웨이브 클리어는 같은 대기열에서 OnStageClear로 저장한다.
			_huntInFlight = false;
			_huntOutbox.Clear();
			_huntBuffer.Clear();
			_saturatedTypes.Clear();
			SendHuntResultLoop(_token.Token).Forget();
			ResetStage((long)_currentStage == 0 ? stage : _currentStage);
            ReviveAllPlayers();
            TransitionStage(stage);
        }		
		
		/// <summary>
		/// 현재 스테이지 값으로 웨이브를 시작한다. UI 갱신, 보스 타이머, 세션 생성, 몬스터 스폰을 수행한다
		/// </summary>
        private void StartStage(StageDefinition definition)
        {
            PrepareStageBackground(definition).Forget(error => HandleStageLoadFailure(error, CameraFade.Instance));
        }

        private async UniTask PrepareStageBackground(StageDefinition definition)
        {
            var background = FindFirstObjectByType<StageBackgroundController>();
            if (background == null) throw new InvalidOperationException("StageBackgroundController is missing from the combat scene.");
            if (!await background.ApplyAsync(definition, _stageDatabaseSO)) return;
            if (this == null || _currentStage != definition.Id) return;
            StartPreparedStage(definition);
            CameraFade.Instance?.FadeIn(.3f);
        }

        private void StartPreparedStage(StageDefinition definition)
        {
			_bossTimerActive = false;
			_defeatPopupActive = false;
			Time.timeScale = 1f;

            foreach (var player in UserManager.Instance.GetPlayers()) player?.GetComponent<ChangeJob>()?.ApplySavedJob();
            ChangeJob.RefreshPartyAura();
            EquipmentManager.Instance?.RestoreEquipment();
            StatEnhanceManager.Instance?.ApplyToAllPlayers();
            ReviveAllPlayers();
            foreach (var player in UserManager.Instance.GetPlayers())
                if (player != null) player.transform.position = CombatViewport.Formation(player.PlayerIndex);
            OnWaveChanged?.Invoke(_currentStageNumber, _currentWaveNumber, _isBossWave);

			_currentState = eStageRunState.Entering;
			StageSession session = BuildSession(definition);
			// 기본 Task는 즉시 완료되므로 기존처럼 StartStage 호출 안에서 스폰과 전투 시작까지 진행한다.
			session.Tick(0f, 0f);
        }

		#endregion

		#region 스테이지 진행
		private void NotifyStageCleared(StageSession session)
		{
			StageDefinition definition =
				session.Definition;

			if (!BattleEconomy.Clear(session)) throw new InvalidOperationException("Stage reward could not be committed.");
            UpdateMaxClearedStage(definition);
            EnqueueStageClear(definition);
			OnStageCleared?.Invoke(definition);
		}
        private void HandleStageLoadFailure(Exception error, CameraFade fade)
        {
            _currentState=eStageRunState.None;_bossTimerActive=false;
            Debug.LogError("Stage resource load failed: " + error.Message);
            fade?.FadeIn(.2f);
            KingdomIdle.UGUI.UIManager.Instance?.ShowToast("전투 리소스를 불러오지 못했습니다. 입장권은 사용되지 않았습니다. 재접속해 주세요.");
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
							}).Forget(error => HandleStageLoadFailure(error, fade));
					});
				}
				else
				{
					LoadManager.Instance.LoadStage(
						previous,
						target,
						_ => StartStage(definition)).Forget(error => HandleStageLoadFailure(error, fade));
				}

				return true;
			}
			if (fade != null)
			{
				fade.FadeOut(0.3f, () => StartStage(definition));
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
			if (session != _currentSession)
				return;

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
			_currentSession?.NotifyPartyDefeated();
        }
		private void EnterDefeatPending()
		{
			_bossTimerActive = false;
			_currentState = eStageRunState.DefeatPending;

			Time.timeScale = 0f;

			_defeatPopupActive = true;
			_defeatPopupHandled = false;
			_defeatPopupTimer = ReturnCountdownDuration;

			OnDefeatPopupShow?.Invoke();
		}

		private void EnterResultPending()
		{
			_currentState = eStageRunState.ResultPending;
			_resultPopupTimer = DungeonReturnDuration;
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
var definition = _currentSession?.Definition;
            if (definition == null) return;
            if (definition.Type != eStageType.Main && BattleEconomy.Tickets(definition.Type) <= 0) { ReturnToMainStage(); return; }
            TransitionStage(definition.Id);
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

			if (BattleEconomy.Tickets(definition.Type) <= 0) return;
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
			SetBossAutoChallenge(_bossAutoChallenge);
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
			session.OnBattleStarted += HandleBattleStarted;
			session.OnResultAccepted += HandleResultAccepted;
			session.OnResultProduced += HandleRuleResult;
			session.OnActionSequenceFailed += HandleActionSequenceFailed;
			
			_currentSession = session;
			StageActionSequence sequence =
				StageActionSequenceFactory.Create(session, _locationSO);
			session.Enter(sequence);
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
            // 메인 스테이지만 대상이고, 서버 MainClear(HighestMainClear) 이하는 모두 클리어다.
            return StageParser.GetStageType(stage) == eStageType.Main && (long)stage > 0 && (long)stage <= LocalProgression.State.HighestMainClear;
    }

		private void UpdateMaxClearedStage(StageDefinition definition)
		{
			switch (definition.Type)
			{
				case eStageType.Main:
					if ((ulong)definition.Id > (ulong)maxStage)
						maxStage = definition.Id;
					break;
				case eStageType.GoldDungeon:
					AdvanceDungeonProgress(ref _goldDungeonProgress, definition);
					break;
				case eStageType.RubyDungeon:
					AdvanceDungeonProgress(ref _rubyDungeonProgress, definition);
					break;
			}
		}

		private static void AdvanceDungeonProgress(
			ref int progress,
			StageDefinition definition)
		{
			if (!definition.NextDifficultyId.HasValue)
				return;

			int nextStageNumber =
				StageParser.GetStageNumber(definition.NextDifficultyId.Value);
			if (nextStageNumber > progress)
				progress = nextStageNumber;
		}

		private void HandleMonsterKilled(StageSession session, Monster monster)
		{
			if (!BattleEconomy.Kill(session, monster)) throw new InvalidOperationException("Kill reward could not be committed.");
            CountKill(session.Definition, monster);
			OnMonsterKilled?.Invoke(session.Definition, monster);
		}

		private void HandleBattleStarted(StageSession session)
		{
			if (session != _currentSession)
				return;

			_currentState = eStageRunState.Running;
			if (session.Definition.TimeLimitSec > 0)
			{
                _bossTimeLimit = session.Definition.TimeLimitSec;
				_bossTimer = _bossTimeLimit;
				_bossTimerActive = true;
			}

			OnStageEnter?.Invoke(session.Definition);
		}

		private void HandleResultAccepted(StageSession session, StageRuleResult result)
		{
			if (session != _currentSession)
				return;

			_bossTimerActive = false;
			_currentState = eStageRunState.Resolving;
		}

		private void HandleActionSequenceFailed(StageSession session, Exception exception)
		{
			if (session != _currentSession)
				return;

			Debug.LogError(
				$"[StageManager] Stage action sequence failed. " +
				$"Stage: {session.Definition.Id}\n{exception}");
			_bossTimerActive = false;
			_currentState = eStageRunState.None;
			EndSession();
		}

		/// <summary>현재 세션의 이벤트를 해제하고 남은 몬스터를 정리한다</summary>
		private void EndSession()
		{
			if (_currentSession == null)
			{
				return;
			}

			StageSession session = _currentSession;
			_currentSession = null;

			session.OnMonsterKilled -= HandleMonsterKilled;
			session.OnBattleStarted -= HandleBattleStarted;
			session.OnResultAccepted -= HandleResultAccepted;
			session.OnResultProduced -= HandleRuleResult;
			session.OnActionSequenceFailed -= HandleActionSequenceFailed;
			session.Exit();
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
		/// <summary>메인 스테이지의 몬스터 처치 수를 스테이지별 서버 전송 버퍼에 누적한다. 던전 처치는 서버에 보내지 않는다.</summary>
		private void CountKill(StageDefinition definition, Monster monster)
		{
			if (definition == null || definition.Type != eStageType.Main) return;
			long stageId = (long)definition.Id;
			if (!_huntBuffer.TryGetValue(stageId, out var kills))
			{
				kills = new Dictionary<eMonsterType, int>();
				_huntBuffer[stageId] = kills;
			}
			kills.TryGetValue(monster.Type, out int count);
			kills[monster.Type] = count + 1;
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

        /// <summary>3초마다 현재 스테이지의 처치 버퍼를 배치로 옮기고 대기열의 머리 항목을 보낸다. 응답이 오기 전에는 겹쳐 보내지 않는다.</summary>
        private async UniTaskVoid SendHuntResultLoop(CancellationToken token)
        {
			while (!token.IsCancellationRequested)
			{
				await UniTask.WaitForSeconds(TickInterval, cancellationToken: token);
				if (_huntOutbox.Count == 0 && StageParser.GetStageType(_currentStage) == eStageType.Main) FlushHuntBuffer((long)_currentStage);
				TrySendNextHunt();
			}
        }

		/// <summary>웨이브 클리어: 그 스테이지에 남은 처치를 먼저 배치로 보내고, 그 뒤에 OnStageClear를 줄 세운다(서버는 정산 전 클리어를 1026으로 거절).</summary>
		private void EnqueueStageClear(StageDefinition definition)
		{
			if (definition == null || definition.Type != eStageType.Main) return;
			long stageId = (long)definition.Id;
			FlushHuntBuffer(stageId);
			_huntOutbox.Enqueue(new HuntOutboxItem { IsClear = true, StageId = stageId });
			TrySendNextHunt();
		}

		/// <summary>스테이지 버퍼를 서버 상한(149마리·10종류)에 맞춘 배치로 나눠 대기열에 넣는다.</summary>
		private void FlushHuntBuffer(long stageId)
		{
			if (!_huntBuffer.TryGetValue(stageId, out var kills)) return;
			_huntBuffer.Remove(stageId);
			var loots = new List<HuntResult>();
			int total = 0;
			void Enqueue()
			{
				if (loots.Count > 0) _huntOutbox.Enqueue(new HuntOutboxItem { StageId = stageId, Loots = loots });
				loots = new List<HuntResult>();
				total = 0;
			}
			_saturatedTypes.TryGetValue(stageId, out var saturated);
			foreach (var pair in kills)
			{
				if (saturated != null && saturated.Contains((ulong)pair.Key)) continue;
				int remaining = pair.Value;
				while (remaining > 0)
				{
					if (loots.Count >= HuntMaxTypesPerBatch || total >= HuntMaxKillsPerBatch) Enqueue();
					int take = Math.Min(remaining, HuntMaxKillsPerBatch - total);
					loots.Add(new HuntResult { MonsterType = (Protocol.Message.Data.eMonsterType)(ulong)pair.Key, Count = (short)take });
					total += take;
					remaining -= take;
				}
			}
			Enqueue();
		}

		/// <summary>오프라인 보상 정산 동안 사냥 배치 전송을 보류한다. 처치는 버퍼에 계속 쌓이고 해제 뒤 보낸다.</summary>
		public void HoldHunts() { _huntsHeld = true; }
		public void ReleaseHunts() { if (!_huntsHeld) return; _huntsHeld = false; TrySendNextHunt(); }

		/// <summary>환생 요청 전에 아직 보내지 않은 처치·클리어를 버린다. 서버가 1-1로 되돌리면 이전 스테이지 배치는 1025로 거절되기 때문이다. 전송 중인 배치의 응답은 그대로 처리된다.</summary>
		public void DiscardPendingHunts()
		{
			_huntBuffer?.Clear();
			HuntOutboxItem inFlight = _huntInFlight && _huntOutbox.Count > 0 ? _huntOutbox.Peek() : null;
			_huntOutbox.Clear();
			if (inFlight != null) _huntOutbox.Enqueue(inFlight);
			_saturatedTypes.Clear();
		}

		/// <summary>대기열 머리 항목을 보낸다. 사냥 배치는 직전 수락으로부터 HuntMinIntervalSec 뒤에만 보낸다.</summary>
		private void TrySendNextHunt()
		{
			if (_huntInFlight || _huntsHeld) return;
			// 서버가 이미 정산한 종류는 빼고, 남는 처치가 없는 배치는 보내지 않는다.
			while (_huntOutbox.Count > 0 && !_huntOutbox.Peek().IsClear && PruneSaturated(_huntOutbox.Peek())) _huntOutbox.Dequeue();
			if (_huntOutbox.Count == 0) return;
			var network = NetworkManager.Instance;
			if (network == null || !network.IsGameDataReady) return;
			var item = _huntOutbox.Peek();
			if (!item.IsClear && Time.realtimeSinceStartup - _lastHuntAcceptedAt < HuntMinIntervalSec) return;
			_huntInFlight = true;
			item.Attempts++;
			if (item.IsClear)
			{
				network.Call<OnStageClearRequestDTO, OnStageClearResponseDTO>(
					new OnStageClearRequestDTO { ClearedStage = (Protocol.Message.Data.eStage)item.StageId },
					result => OnStageClearSuccess(item, result), error => OnHuntRewardError(item, error));
			}
			else
			{
				// 메인 스테이지는 BattleId를 비운다(값이 있으면 1025).
				network.Call<OnRewardRequestDTO, OnHuntResponseDTO>(
					new OnRewardRequestDTO { Stage = (Protocol.Message.Data.eStage)item.StageId, Loots = item.Loots, BattleId = null },
					result => OnHuntRewardSuccess(item, result), error => OnHuntRewardError(item, error));
			}
		}

		private void Dequeue(HuntOutboxItem item)
		{
			if (_huntOutbox.Count > 0 && ReferenceEquals(_huntOutbox.Peek(), item)) _huntOutbox.Dequeue();
		}

		/// <summary>서버 사냥 보상 응답의 플레이어 데이터(레벨·경험치·처치·재화·장비 인벤토리)를 그대로 저장한다. 로컬 처치 보상·드롭은 더하지 않는다.</summary>
		private void OnHuntRewardSuccess(HuntOutboxItem item, OnHuntResponseDTO result)
		{
			_huntInFlight = false;
			_lastHuntAcceptedAt = Time.realtimeSinceStartup;
			Dequeue(item);
			bool saved = LocalProgression.Execute("server-hunt", s =>
			{
				s.AccountLevel = BalanceMath.Clamp(result.Level, 1, BalanceMath.AccountCap);
				s.Experience = Math.Max(0, result.Exp);
				BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, 0);
				s.Kills = Math.Max(0, result.KillScore);
				UserManager.ApplyCurrency(s, result.Currency);
				s.Wallet[eCurrency.Gold] = Math.Max(0, result.Gold);
				s.Wallet[eCurrency.AncientCoin] = Math.Max(0, result.AncientCoin);
				UserManager.ApplyEquipment(s, result.Equipment);
				return true;
			});
			Debug.Log($"[Hunt] stage=0x{item.StageId:X} loots={DescribeLoots(item.Loots)} -> level={result.Level} exp={result.Exp} kills={result.KillScore} gold={result.Gold} drops={result.Drops?.Count ?? 0} inventory={UserManager.DescribeInventory(result.Equipment)} saved={saved}");
			if (!saved) Debug.LogError("[Hunt] 서버 결과 저장 실패: " + LocalProgression.LastError);
			if (saved && result.Equipment != null) EquipmentManager.Instance?.RestoreEquipment();
			if (saved && result.Drops != null)
			{
				// 서버가 굴린 전장 드롭을 HUD에 알린다(예전 로컬 드롭 연출과 같은 경로).
				foreach (var drop in result.Drops)
				{
					if (drop != null && drop.Kind == RewardKind.Equipment && drop.ItemCode.HasValue)
						KingdomIdle.UGUI.UIManager.Instance?.NotifyFieldEquipment((int)drop.ItemCode.Value.GetItemCode());
				}
			}
			TrySendNextHunt();
		}

		/// <summary>스테이지 클리어 응답의 플레이어 데이터(재화·클리어 기록·장비 인벤토리·마탑 상태)를 그대로 저장한다. 최초 클리어 보상도 서버가 지급한 결과다.</summary>
		private void OnStageClearSuccess(HuntOutboxItem item, OnStageClearResponseDTO result)
		{
			_huntInFlight = false;
			Dequeue(item);
			_saturatedTypes.Remove(item.StageId);
			bool saved = LocalProgression.Execute("server-stage-clear", s =>
			{
				UserManager.ApplyCurrency(s, result.Currency);
				UserManager.ApplyEquipment(s, result.Equipment);
				UserManager.ApplyMageTower(s, result.MageTowerState);
				if (result.StageState != null)
				{
					// 영구 최고 클리어는 서버 MainClear다. 이번 주기 최고 도달도 서버 값보다 뒤로 가지 않게 맞춘다.
					s.HighestMainClear = Math.Max(0, (long)result.StageState.MainClear);
					s.CycleFrontierStage = Math.Max(s.CycleFrontierStage, (long)result.StageState.HighestStage);
				}
				return true;
			});
			long next = result.StageState != null ? (long)result.StageState.CurrentStage : 0;
			Debug.Log($"[StageClear] stage=0x{item.StageId:X} first={result.IsFirstClear} rewards={result.GrantedRewards?.Count ?? 0} next=0x{next:X} client=0x{(long)_currentStage:X} inventory={UserManager.DescribeInventory(result.Equipment)} skills={result.MageTowerState?.Skills?.Count ?? -1} saved={saved}");
			if (!saved) Debug.LogError("[StageClear] 서버 결과 저장 실패: " + LocalProgression.LastError);
			if (saved) QuestManager.Instance?.RequestRefresh("clear");
			if (saved)
			{
				if (result.Equipment != null) EquipmentManager.Instance?.RestoreEquipment();
				if (result.MageTowerState != null) KingdomIdle.MageTower.MageTowerManager.Instance?.NotifyCommitted();
			}
			// 웨이브가 빨리 끝나면 클라이언트가 서버보다 앞서 있는 것이 정상이다(뒤따르는 클리어가 대기열에 있다). 뒤에 있을 때만 경고한다.
			if (StageParser.GetStageType(_currentStage) == eStageType.Main && (long)_currentStage < next)
				Debug.LogWarning($"[StageClear] 서버 다음 스테이지 0x{next:X}보다 클라이언트 현재 스테이지 0x{(long)_currentStage:X}가 뒤에 있습니다. 같은 웨이브 반복은 아직 서버가 지원하지 않아 이후 배치가 1025로 거절될 수 있습니다.");
			TrySendNextHunt();
		}

		/// <summary>간격 위반(20)·처리 중(1017)·전송 장애는 다음 틱에 같은 항목을 다시 보내고, 그 외 거절은 항목을 폐기한다.</summary>
		private void OnHuntRewardError(HuntOutboxItem item, PlayFab.PlayFabError error)
		{
			_huntInFlight = false;
			int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
			string kind = error?.ErrorDetails != null && error.ErrorDetails.TryGetValue("Kind", out var kinds) && kinds.Count > 0 ? kinds[0] : "";
			// Kind가 없는 오류는 PlayFab SDK 원시 오류(실행 시간 한도·네트워크 등)라 재시도한다. 요청 자체가 틀린 종류만 폐기한다.
			bool definite = kind == "InvalidRequest" || kind == "NotAuthenticated" || kind == "Protocol" || kind == "InvalidResponse" || kind == "ApplyFailed";
			bool retry = (code == 20 || code == 1017 || (code == 0 && !definite)) && item.Attempts < HuntMaxAttempts;
			string what = item.IsClear ? "StageClear" : "Hunt";
			if (retry)
			{
				Debug.LogWarning($"[{what}] 재시도 대기 stage=0x{item.StageId:X} kind={kind} code={code} attempt={item.Attempts}: {error?.ErrorMessage}");
				return;
			}
			if (code == 1007 && !item.IsClear)
			{
				// 웨이브 상한 초과: 재접속 전 세션에서 이미 정산된 처치만큼 넘친 것이다. 반으로 나눠 다시 보내 서버가 받는 만큼만 정산하고,
				// 1마리짜리 배치도 거절되면 그 종류는 포화로 표시해 이 스테이지에서는 더 보내지 않는다. 그 뒤의 클리어가 1026 없이 통과한다.
				Dequeue(item);
				var halves = SplitHuntItem(item);
				if (halves == null)
				{
					MarkSaturated(item.StageId, item.Loots);
					Debug.LogWarning($"[Hunt] 포화 stage=0x{item.StageId:X} {DescribeLoots(item.Loots)}: 서버가 이미 정산한 처치라 버린다.");
				}
				else
				{
					PushFront(halves);
					Debug.LogWarning($"[Hunt] 분할 재전송 stage=0x{item.StageId:X} {DescribeLoots(item.Loots)} -> {DescribeLoots(halves[0].Loots)} + {DescribeLoots(halves[1].Loots)} (1007)");
				}
				TrySendNextHunt();
				return;
			}
			Dequeue(item);
			Debug.LogError($"[{what}] 폐기 stage=0x{item.StageId:X} kind={kind} code={code}: {error?.ErrorMessage}");
			TrySendNextHunt();
		}

		/// <summary>배치를 둘로 나눈다. 종류가 여럿이면 종류로, 한 종류면 수량으로 나누고, 1마리면 null.</summary>
		private static List<HuntOutboxItem> SplitHuntItem(HuntOutboxItem item)
		{
			if (item.Loots == null || item.Loots.Count == 0) return null;
			var first = new List<HuntResult>();
			var second = new List<HuntResult>();
			if (item.Loots.Count > 1)
			{
				int half = item.Loots.Count / 2;
				for (int i = 0; i < item.Loots.Count; i++) (i < half ? first : second).Add(item.Loots[i]);
			}
			else
			{
				var loot = item.Loots[0];
				if (loot.Count <= 1) return null;
				int half = loot.Count / 2;
				first.Add(new HuntResult { MonsterType = loot.MonsterType, Count = (short)half });
				second.Add(new HuntResult { MonsterType = loot.MonsterType, Count = (short)(loot.Count - half) });
			}
			return new List<HuntOutboxItem>
			{
				new HuntOutboxItem { StageId = item.StageId, Loots = first },
				new HuntOutboxItem { StageId = item.StageId, Loots = second }
			};
		}

		private void MarkSaturated(long stageId, List<HuntResult> loots)
		{
			if (loots == null) return;
			if (!_saturatedTypes.TryGetValue(stageId, out var set)) _saturatedTypes[stageId] = set = new HashSet<ulong>();
			foreach (var loot in loots) set.Add((ulong)loot.MonsterType);
		}

		/// <summary>포화 종류를 배치에서 뺀다. 남는 처치가 없으면 true.</summary>
		private bool PruneSaturated(HuntOutboxItem item)
		{
			if (item.Loots == null) return true;
			if (_saturatedTypes.TryGetValue(item.StageId, out var set) && set.Count > 0)
				item.Loots.RemoveAll(loot => set.Contains((ulong)loot.MonsterType));
			return item.Loots.Count == 0;
		}

		/// <summary>대기열 앞에 항목들을 넣는다(Queue라 다시 쌓는다).</summary>
		private void PushFront(List<HuntOutboxItem> items)
		{
			var rest = new List<HuntOutboxItem>(_huntOutbox);
			_huntOutbox.Clear();
			foreach (var entry in items) _huntOutbox.Enqueue(entry);
			foreach (var entry in rest) _huntOutbox.Enqueue(entry);
		}

		private static string DescribeLoots(List<HuntResult> loots)
		{
			if (loots == null) return "";
			var parts = new List<string>(loots.Count);
			foreach (var loot in loots) parts.Add($"{(ulong)loot.MonsterType}x{loot.Count}");
			return string.Join(",", parts);
		}

		#endregion
	}
}
