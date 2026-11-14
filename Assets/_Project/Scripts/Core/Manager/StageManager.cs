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
using System.Linq;
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
	public partial class StageManager : MonoBehaviour
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
        internal bool IsMainStageMovePending => _mainStageRequestPending;
        internal bool IsHuntSyncFailed => _huntSyncFailed;
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
            return RequestMainStageChange(target, IsCycleStageCleared(target) && !StageParser.IsBossWave(target));
        }

        private const float DefeatPopupDuration = 15f;
        public const float DungeonReturnDuration = 6f;
        public float ReturnCountdownDuration => _currentSession?.Definition.Type != eStageType.Main ? DungeonReturnDuration : DefeatPopupDuration;
        public float ReturnCountdownRemaining => _currentState == eStageRunState.ResultPending ? _resultPopupTimer :
            _currentState == eStageRunState.DefeatPending ? _defeatPopupTimer : 0;
        private float _resultPopupTimer;
        private const float TickInterval = 3f;
        private const float MainWaveClearHoldSeconds = .9f;
        
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
        
        // 던전 배치는 입장 때 받은 BattleId를 유지한다.
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
            public Protocol.Message.Data.eStageClearAction NextAction;
            public bool ShowResult;
            public float PresentNextAfter;
            public long StageId;
            public string BattleId;
            public List<HuntResult> Loots;
            public int Attempts;
            public string MainRunId, BatchId;
            public MainWaveCompletionSave Completion;
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
            PumpHuntRetry();
            RetryDungeonRequest();
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
			if (RubyProgression.IsBusy || _mainStageRequestPending || _currentState != eStageRunState.Running ||
			    _currentSession?.Definition.Type != eStageType.Main)
			{
				return false;
			}

			eStageType type = StageParser.GetStageType(stage);
			if ((type != eStageType.GoldDungeon &&
			     type != eStageType.RubyDungeon) ||
			    !IsDungeonStageUnlocked(stage))
			{
				return false;
			}

			CaptureMainStageSnapshot();
			return BeginServerDungeon(stage);
		}
		public void ReturnToMainStage()
		{
			if (RubyProgression.IsBusy || _dungeonRequestPending || StageParser.GetStageType(_currentStage) == eStageType.Main || _currentState == eStageRunState.Transitioning) return;
            if (!string.IsNullOrEmpty(LocalProgression.State.ActiveDungeon))
            {
                ExitServerDungeon(ReturnToMainStage);
                return;
            }
			Time.timeScale = 1f;
			if (_mainStageSnapshot == null)
			{
				Debug.LogError("[Stage] MainStageSnapshot이 없습니다.");
				return;
			}

			_isLoopMode = _mainStageSnapshot.LoopMode;
			SetBossAutoChallenge(_mainStageSnapshot.BossAutoChallenge);
			TransitionStage((eStage)LocalProgression.State.MainStage);
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

			// 환생 성공 응답을 저장한 뒤에만 호출한다.
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
			_huntSyncFailed = false;
			_mainStageRequestPending = false;
            _onHuntsSettled = null;
            _dungeonRequestPending = false;
            _retryDungeon = null;
            _nextHuntSendAt = 0f;
            _mainRunForSession = null;
            _recoveringMainCompletion = false;
            _mainTailWaiting = false;
			_huntOutbox.Clear();
			_huntBuffer.Clear();
			_saturatedTypes.Clear();
			SendHuntResultLoop(_token.Token).Forget();
			if (ResumePendingMainCompletion()) return;
			ResetStage((long)_currentStage == 0 ? stage : _currentStage);
            ReviveAllPlayers();
            if (!string.IsNullOrEmpty(LocalProgression.State.ActiveDungeon))
            {
                _mainStageSnapshot = new MainStageSnapshot { StageId = stage, LoopMode = _isLoopMode, BossAutoChallenge = _bossAutoChallenge };
                _currentState = eStageRunState.Entering;
                ExitServerDungeon(() => TransitionStage(stage));
            }
            else TransitionStage(stage);
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
			if (!BindMainWaveRun(definition)) return;
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
		private void NotifyStageCleared(StageSession session, bool showResult = false)
		{
			EnqueueStageClear(session.Definition, showResult);
		}
        private void HandleStageLoadFailure(Exception error, CameraFade fade)
        {
            _currentState=eStageRunState.None;_bossTimerActive=false;
            Debug.LogError("Stage resource load failed: " + error.Message);
            fade?.FadeIn(.2f);
            KingdomIdle.UGUI.UIManager.Instance?.ShowToast("전투 리소스를 불러오지 못했습니다. 던전 입장권 상태는 서버에 저장되어 있습니다. 재접속해 주세요.");
        }
        private bool TransitionStage(eStage target, float presentAfter = 0f)
		{
			Debug.Log($"{target}");
			if (RubyProgression.IsBusy || _huntSyncFailed || _mainStageRequestPending || _onHuntsSettled != null || _dungeonRequestPending || _currentState == eStageRunState.Transitioning) return false;
			
			if (!_provider.TryGet(target, out StageDefinition definition))
			{ 
				Debug.LogError($"StageDefinition을 찾을 수 없습니다. Stage: {target}");
				return false;
			}
			_currentState = eStageRunState.Transitioning;
            bool automaticMainClear = IsAutomaticMainClearTransition(_currentSession?.Definition, definition, presentAfter);
            float hold = presentAfter - Time.realtimeSinceStartup;
            if (hold > 0f)
                PresentStageAfterHold(definition, hold, automaticMainClear, _token.Token).Forget();
            else
                PresentStage(definition, automaticMainClear);
            return true;
        }

        private async UniTask PresentStageAfterHold(StageDefinition definition, float hold, bool automaticMainClear, CancellationToken token)
        {
            long generation = LocalProgression.AccountGeneration;
            var network = NetworkManager.Instance;
            long networkGeneration = network != null ? network.AccountGeneration : -1;
            bool canceled = await UniTask.WaitForSeconds(hold, ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow();
            if (!canceled && CanPresentHeldStage(generation, token, network, networkGeneration))
                PresentStage(definition, automaticMainClear);
        }

        private bool CanPresentHeldStage(long generation, CancellationToken token, NetworkManager network, long networkGeneration) => this != null &&
            generation == LocalProgression.AccountGeneration && !token.IsCancellationRequested &&
            _token != null && _token.Token == token && ReferenceEquals(NetworkManager.Instance, network) &&
            (network == null || network.AccountGeneration == networkGeneration);

        private static bool IsAutomaticMainClearTransition(StageDefinition current, StageDefinition target, float presentAfter) =>
            presentAfter > 0f && current?.Type == eStageType.Main && target.Type == eStageType.Main;

        private static float StageFadeOutSeconds(eStage previous, StageDefinition target, bool automaticMainClear)
        {
            if (StageParser.GetResourceGroupId(previous) != target.ResourceGroupId) return .4f;
            return automaticMainClear && StageParser.GetStageType(previous) == eStageType.Main && target.Type == eStageType.Main ? .1f : .3f;
        }

        private void PresentStage(StageDefinition definition, bool automaticMainClear)
        {
            eStage target = definition.Id;
			eStage previous = _currentStage;
            float fadeOutSeconds = StageFadeOutSeconds(previous, definition, automaticMainClear);
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
					fade.FadeOut(fadeOutSeconds, () =>
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

				return;
			}
			if (fade != null)
			{
				fade.FadeOut(fadeOutSeconds, () => StartStage(definition));
			}
			else
			{
				StartStage(definition);
			}
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
					NotifyStageCleared(session);
					break;
				case eStageFlowAction.RestartStage:
					Debug.Log("스테이지를 재시작 합니다");
					RestartStage();
					break;
				case eStageFlowAction.ShowResult:
					Debug.Log("스테이지 클리어 후 결과를 보고 있습니다");
                    if (session.Definition.Type == eStageType.Main)
                    {
                        NotifyStageCleared(session, true);
                    }
                    else CompleteServerDungeon(session);
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
			bool retreat = _isBossWave || !retryCurrentWave;
			eStage target = _currentStage;
			if (retreat) StageRule.GetPreviousWave(_currentStage, out target);
			if (_isBossWave) SetBossAutoChallenge(false);
			if (!RequestMainStageChange(target, retreat))
			{
				_defeatPopupHandled = false;
				_defeatPopupActive = true;
				_defeatPopupTimer = ReturnCountdownDuration;
				OnDefeatPopupShow?.Invoke();
			}
        }
		
        /// <summary>패배 후 현재 상태값 기준으로 웨이브를 다시 시작한다. 재도전/복귀 결정은 호출 전에 끝난다</summary>
        private void RestartStage()
        {
var definition = _currentSession?.Definition;
            if (definition == null) return;
            if (definition.Type != eStageType.Main)
            {
                if (_dungeonRequestPending) return;
                if (!string.IsNullOrEmpty(LocalProgression.State.ActiveDungeon)) ExitServerDungeon(() => BeginServerDungeon(definition.Id));
                else BeginServerDungeon(definition.Id);
                return;
            }
            RequestMainStageChange(definition.Id, _isLoopMode);
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

            BeginServerDungeon(definition.NextDifficultyId.Value);
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
			if (session.Definition.Type == eStageType.Main)
				Debug.Log($"[MainWave] start stage=0x{(long)session.Definition.Id:X} realtime={Time.realtimeSinceStartupAsDouble:F3}");
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
		/// <summary>현재 전투의 처치 수를 서버 전송 버퍼에 누적한다.</summary>
		private void CountKill(StageDefinition definition, Monster monster)
		{
			if (definition == null) return;
			if (definition.Type == eStageType.Main && _mainRunForSession != null) return;
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
				await UniTask.WaitForSeconds(TickInterval, ignoreTimeScale: true, cancellationToken: token);
				if (_huntOutbox.Count == 0)
				{
					FlushMainWaveHunts();
					FlushHuntBuffer((long)_currentStage);
				}
				TrySendNextHunt();
			}
        }

		/// <summary>웨이브 클리어: 그 스테이지에 남은 처치를 먼저 배치로 보내고, 그 뒤에 OnStageClear를 줄 세운다(서버는 정산 전 클리어를 1026으로 거절).</summary>
		private void EnqueueStageClear(StageDefinition definition, bool showResult)
		{
			if (definition == null || definition.Type != eStageType.Main) return;
			if (_mainRunForSession != null)
			{
				EnqueueMainCompletion(definition, showResult);
				return;
			}
			long stageId = (long)definition.Id;
			MainStageRule.GetNextWave(definition.Id, out eStage next);
			FlushHuntBuffer(stageId);
			_huntOutbox.Enqueue(new HuntOutboxItem { IsClear = true, StageId = stageId, ShowResult = showResult,
                PresentNextAfter = Time.realtimeSinceStartup + MainWaveClearHoldSeconds,
                NextAction = ShouldRestart(next) ? Protocol.Message.Data.eStageClearAction.Repeat : Protocol.Message.Data.eStageClearAction.Advance });
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
				if (loots.Count > 0) _huntOutbox.Enqueue(new HuntOutboxItem { StageId = stageId, Loots = loots,
                    BattleId = StageParser.GetStageType((eStage)stageId) == eStageType.Main ? null : LocalProgression.State.ActiveDungeon });
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

		/// <summary>대기열 머리 항목을 보낸다. 사냥 배치는 직전 수락으로부터 HuntMinIntervalSec 뒤에만 보낸다.</summary>
		private void TrySendNextHunt()
		{
			if (_huntInFlight || _huntsHeld || _huntSyncFailed) return;
			if (_nextHuntSendAt > Time.realtimeSinceStartup) return;
			_nextHuntSendAt = 0f;
			// 서버가 이미 정산한 종류는 빼고, 남는 처치가 없는 배치는 보내지 않는다.
			while (_huntOutbox.Count > 0 && !_huntOutbox.Peek().IsClear && _huntOutbox.Peek().MainRunId == null && PruneSaturated(_huntOutbox.Peek())) _huntOutbox.Dequeue();
			if (_huntOutbox.Count == 0 && _onHuntsSettled != null) FlushMainWaveHunts(true);
			if (_huntOutbox.Count == 0) { FinishHuntSettlement(true); return; }
			var network = NetworkManager.Instance;
			if (network == null || !network.IsGameDataReady) return;
			var item = _huntOutbox.Peek();
            if (item.MainRunId != null && !item.IsClear && _currentSession?.IsCleared == true &&
                !(_onHuntsSettled != null && IsMainDefeatSettlement)) return;
			if (!item.IsClear && Time.realtimeSinceStartup - _lastHuntAcceptedAt < HuntMinIntervalSec)
			{
				_nextHuntSendAt = _lastHuntAcceptedAt + HuntMinIntervalSec;
				return;
			}
			if (DeferMainTail(item, Time.realtimeSinceStartup)) return;
			_huntInFlight = true;
			item.Attempts++;
			long generation = LocalProgression.AccountGeneration, networkGeneration = network.AccountGeneration;
			var token = _token;
            bool Current() => OwnsHuntResponse(item, network, generation, networkGeneration, token);
			if (item.Completion != null)
			{
				network.Call<OnCompleteMainWaveRequestDTO, OnCompleteMainWaveResponseDTO>(MainWaveProgression.Request(item.Completion),
					result => { if (Current()) OnMainCompletionSuccess(item, result); },
					error => { if (Current()) OnMainWaveError(item, error); });
				return;
			}
			if (item.IsClear)
			{
				network.Call<OnStageClearRequestDTO, OnStageClearResponseDTO>(
					new OnStageClearRequestDTO { ClearedStage = (Protocol.Message.Data.eStage)item.StageId, NextAction = item.NextAction },
					result => { if (Current()) OnStageClearSuccess(item, result); }, error => { if (Current()) OnHuntRewardError(item, error); });
			}
			else
			{
				// 메인은 null, 던전은 입장 때 받은 서버 BattleId를 보낸다.
				network.Call<OnRewardRequestDTO, OnHuntResponseDTO>(
					new OnRewardRequestDTO { Stage = (Protocol.Message.Data.eStage)item.StageId, Loots = item.Loots, BattleId = item.BattleId,
						MainRunId = item.MainRunId, BatchId = item.BatchId },
					result => { if (Current()) OnHuntRewardSuccess(item, result); },
					error => { if (Current()) { if (item.MainRunId != null) OnMainWaveError(item, error); else OnHuntRewardError(item, error); } });
			}
		}

		private void Dequeue(HuntOutboxItem item)
		{
			if (_huntOutbox.Count > 0 && ReferenceEquals(_huntOutbox.Peek(), item)) _huntOutbox.Dequeue();
		}

		/// <summary>서버 사냥 보상 응답의 플레이어 데이터(레벨·경험치·처치·재화·장비 인벤토리)를 그대로 저장한다. 로컬 처치 보상·드롭은 더하지 않는다.</summary>
		private void OnHuntRewardSuccess(HuntOutboxItem item, OnHuntResponseDTO result)
		{
			if (item.MainRunId != null && (result.MainBattle == null || result.MainBattle.RunId != item.MainRunId ||
				(long)result.MainBattle.Stage != item.StageId))
			{
				OnMainWaveError(item, NetworkManager.CreateError("InvalidResponse", "부분 정산의 메인 회차를 확인하지 못했습니다."));
				return;
			}
			_huntInFlight = false;
			_lastHuntAcceptedAt = Time.realtimeSinceStartup;
			Dequeue(item);
			bool saved = LocalProgression.Execute("server-hunt", s =>
			{
				if (item.MainRunId != null && !MainWaveProgression.ApplyServerState(s, result.MainBattle)) return false;
				s.AccountLevel = BalanceMath.Clamp(result.Level, 1, BalanceMath.AccountCap);
				s.Experience = Math.Max(0, result.Exp);
				BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, 0);
				s.Kills = Math.Max(0, result.KillScore);
				UserManager.ApplyCurrency(s, result.Currency);
				s.Wallet[eCurrency.Gold] = Math.Max(0, result.Gold);
				s.Wallet[eCurrency.AncientCoin] = Math.Max(0, result.AncientCoin);
				if (result.Equipment != null) UserManager.ApplyEquipment(s, result.Equipment);
				return true;
			});
			Debug.Log($"[Hunt] stage=0x{item.StageId:X} loots={DescribeLoots(item.Loots)} -> level={result.Level} exp={result.Exp} kills={result.KillScore} gold={result.Gold} drops={result.Drops?.Count ?? 0} inventory={UserManager.DescribeInventory(result.Equipment)} saved={saved}");
            if (!saved)
            {
                _huntSyncFailed = true;
                FinishHuntSettlement(false);
                Time.timeScale = 0;
                Debug.LogError("[Hunt] 서버 결과 저장 실패: " + LocalProgression.LastError);
            }
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
			MainStageRule.GetNextWave((eStage)item.StageId, out eStage expected);
			long unlocked = (long)expected;
			if (item.NextAction == Protocol.Message.Data.eStageClearAction.Repeat) expected = (eStage)item.StageId;
			if (!IsValidMainStageState(result.StageState) || (long)result.StageState.CurrentStage != (long)expected ||
				(long)result.StageState.HighestStage < unlocked || (long)result.StageState.MainClear < item.StageId ||
				_currentSession?.Definition.Id != (eStage)item.StageId)
			{
				OnHuntRewardError(item, NetworkManager.CreateError("InvalidResponse", "클리어 후 서버 스테이지를 확인할 수 없습니다."));
				return;
			}
			StageDefinition definition = _currentSession.Definition;
			Dequeue(item);
			_saturatedTypes.Remove(item.StageId);
			bool saved = BattleEconomy.Clear(_currentSession) && LocalProgression.Execute("server-stage-clear", s =>
			{
				if (!MainWaveProgression.ApplyServerState(s, result.MainBattle)) return false;
				UserManager.ApplyCurrency(s, result.Currency);
				UserManager.ApplyEquipment(s, result.Equipment);
				UserManager.ApplyMageTower(s, result.MageTowerState);
				return ApplyMainStageState(s, result.StageState);
			});
			long next = result.StageState != null ? (long)result.StageState.CurrentStage : 0;
			Debug.Log($"[StageClear] stage=0x{item.StageId:X} first={result.IsFirstClear} rewards={result.GrantedRewards?.Count ?? 0} next=0x{next:X} client=0x{(long)_currentStage:X} inventory={UserManager.DescribeInventory(result.Equipment)} skills={result.MageTowerState?.Skills?.Count ?? -1} saved={saved}");
            if (!saved)
            {
                _huntSyncFailed = true;
                FinishHuntSettlement(false);
                Time.timeScale = 0;
                Debug.LogError("[StageClear] 서버 결과 저장 실패: " + LocalProgression.LastError);
            }
			if (saved) QuestManager.Instance?.RequestRefresh("clear");
			if (saved)
			{
				if (result.Equipment != null) EquipmentManager.Instance?.RestoreEquipment();
				if (result.MageTowerState != null) KingdomIdle.MageTower.MageTowerManager.Instance?.NotifyCommitted();
				if (EquipmentManager.IsInstanceInventory && LocalProgression.State.Equipment.Count >= EquipmentManager.InventoryCapacity &&
				    result.GrantedRewards?.Any(reward => reward.Kind == RewardKind.Equipment) == true)
					GameManager.Instance?.Mailbox?.RefreshAfterDelivery("stage-clear-overflow");
			}
			if (saved)
			{
				UpdateMaxClearedStage(definition);
				OnStageCleared?.Invoke(definition);
				if (item.ShowResult) EnterResultPending();
				else if (!TransitionStage((eStage)next, item.PresentNextAfter))
				{
					_huntSyncFailed = true;
					FinishHuntSettlement(false);
					Time.timeScale = 0;
					KingdomIdle.UGUI.UIManager.Instance?.ShowToast("서버 스테이지로 이동하지 못했습니다. 재접속해 주세요.");
					return;
				}
				RubyProgression.OnWaveCleared(definition);
			}
			TrySendNextHunt();
		}

		/// <summary>일시적 오류는 다음 틱에 재시도한다. 확정할 수 없는 배치는 보류하고 후속 입장·환생을 막는다.</summary>
		private void OnHuntRewardError(HuntOutboxItem item, PlayFab.PlayFabError error)
		{
			_huntInFlight = false;
			int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
			string kind = error?.ErrorDetails != null && error.ErrorDetails.TryGetValue("Kind", out var kinds) && kinds.Count > 0 ? kinds[0] : "";
			// 사냥의 일시 오류는 기존 재시도 정책을 유지하고, 확정할 수 없는 배치는 보존한다.
			bool definite = kind == "InvalidRequest" || kind == "NotAuthenticated" || kind == "Protocol" || kind == "InvalidResponse" || kind == "ApplyFailed";
			// Repeat 클리어는 응답 유실 후 재전송으로 완료를 판별할 수 없다. 실행 전 거절(1017)만 재시도한다.
			bool retry = (code == 1017 || (!item.IsClear && (code == 20 || (code == 0 && !definite)))) && item.Attempts < HuntMaxAttempts;
			string what = item.IsClear ? "StageClear" : "Hunt";
			if (retry)
			{
				_nextHuntSendAt = Time.realtimeSinceStartup + 1f;
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
            _huntSyncFailed = true;
            FinishHuntSettlement(false);
            Time.timeScale = 0;
            KingdomIdle.UGUI.UIManager.Instance?.ShowToast("전투 보상 정산을 확인하지 못했습니다. 재접속해 주세요.");
			Debug.LogError($"[{what}] 보류 stage=0x{item.StageId:X} kind={kind} code={code}: {error?.ErrorMessage}");
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
				new HuntOutboxItem { StageId = item.StageId, BattleId = item.BattleId, Loots = first },
				new HuntOutboxItem { StageId = item.StageId, BattleId = item.BattleId, Loots = second }
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
