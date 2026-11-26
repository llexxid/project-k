using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public enum EconomyInputStatus { PendingSave, NotReady, Busy, InvalidRequest, InsufficientFunds, MaxLevel, QueueFull, SaveFailed }
    public readonly struct EconomyInputCompletion
    {
        public readonly long Sequence;
        public readonly EconomyCommandKind Kind;
        public readonly bool Success;
        public readonly long? EquipmentId;
        public readonly long SpentStones;
        public readonly string Message;
        internal EconomyInputCompletion(EconomyInput input, bool success, string message)
        { Sequence = input.Sequence; Kind = input.Command.Kind; EquipmentId = input.Command.EquipmentInstanceId; SpentStones = success && EquipmentId.HasValue ? input.Command.SpendLimit : 0; Success = success; Message = message; }
    }

    public static class EconomyBatchProgression
    {
        public const string FailureMessage = "서버 불안정으로 강화 시도가 실패했습니다. 다시 시도해주세요";
        private const int MaxInputs = 64, MaxBatchKills = 149;
        private const int StorageRetrySendLimit = 3;
        private const float BatchWindow = .1f, HuntWindow = 3f;
        private static EconomyInputJournal _journal;
        private static NetworkManager _network;
        private static long _localGeneration = -1, _networkGeneration = -1;
        private static bool _supported, _recovered, _requestPending, _queryRequired, _batchUnknown, _externalUnknown, _externalWrite;
        private static bool _recovering, _startupMaySplit, _sentThisSession, _huntsHeld, _blocked;
        private static bool _storageRetryActive, _storageRetryPaused, _storageRetryAllowsInput;
        private static int _storageRetryRemaining, _storageRetrySends;
        private static float _sealAfter, _nextHuntAt, _retryAt;
        private static Action<bool> _recoveryDone;
        private sealed class Fence { internal long Sequence; internal Action<bool> Completed; }
        private static readonly Queue<Fence> _fences = new();
        private static readonly List<EconomyInput> _discardedInputs = new();
        private static long _discardAfterRevision;
        private static string _clientId;
        private static EconomyProjection _projection;
        private static long _projectionAt = -1;
        public static event Action Changed;
        public static event Action<EconomyInputCompletion> InputCompleted;
        public static event Action SpendingFailed;
        public static long ProjectionRevision { get; private set; }
        public static string Status { get; private set; }
        private static bool CurrentSession => LocalProgression.IsReady && _localGeneration == LocalProgression.AccountGeneration &&
            _network != null && _network == NetworkManager.Instance && _networkGeneration == _network.AccountGeneration;
        public static bool Available => _supported && CurrentSession && NetworkManager.Instance.IsGameDataReady;
        public static bool SupportsEquipmentEnhance => Available && EquipmentManager.IsInstanceInventory &&
            LocalProgression.State.Economy?.Server?.EquipmentEnhanceAvailable == true;
        public static bool HasPendingEquipmentEnhance => CurrentSession && _journal != null &&
            (_journal.Data.InFlight?.Commands.Exists(x => x.Kind == EconomyCommandKind.EquipmentEnhance) == true ||
             _journal.Data.Inputs.Exists(x => x.Command.Kind == EconomyCommandKind.EquipmentEnhance));
        public static bool SupportsMageEnhance => Available &&
            EconomyBatchProtocol.MageMode(LocalProgression.State.Economy?.Server) == MageEnhanceBatchMode.Active;
        internal static bool CanStartSingleMageEnhance => CurrentSession && _journal != null && !SupportsMageEnhance &&
            !HasUnknownResult && (!Available || RecoveryComplete && !HasHeldSpending) &&
            _journal.Data.InFlight?.Commands.Exists(x => x.Kind == EconomyCommandKind.MageEnhance) != true &&
            !_journal.Data.Inputs.Exists(x => x.Command.Kind == EconomyCommandKind.MageEnhance);
        public static bool RecoveryComplete => _recovered && CurrentSession;
        public static bool HasUnknownResult => _batchUnknown || _externalUnknown;
        public static bool HasUnresolvedBatch
        {
            get
            {
                var request = _journal?.Data.InFlight;
                return CurrentSession && request != null && LocalProgression.TryGetCommittedState(out var state) &&
                    state.Economy.LastAppliedRequestId != request.RequestId;
            }
        }
        public static bool HasHeldSpending => _journal?.Data.Inputs.Any(x => x.Held && x.Command.Kind != EconomyCommandKind.Hunt) == true;
        internal static bool HasPendingFailureNotice => _discardedInputs.Count != 0;
        public static bool IsStorageRetryWaiting => Available && RecoveryComplete && !_recovering && !_externalUnknown && !_blocked &&
            _storageRetryActive && !_storageRetryPaused && _storageRetryAllowsInput && _journal?.Data.InFlight != null;
        public static int PendingSpendingCount => CurrentSession && _journal != null
            ? _journal.Data.Inputs.Concat(_journal.Data.InFlightInputs).Count(x => x.Command.Kind != EconomyCommandKind.Hunt) : 0;
        public static bool CanAcceptGrowth => Available && RecoveryComplete && (!HasUnknownResult || IsStorageRetryWaiting) && !HasHeldSpending &&
            !_externalWrite && !_huntsHeld && _fences.Count == 0 && _journal?.Error == null && !RubyProgression.HasPendingRequest &&
            LocalProgression.State.PendingMainCompletion == null;
        public static bool HasPendingWork => _requestPending || _journal?.Data.InFlight != null ||
            _journal?.Data.Inputs.Any(x => !x.Held) == true || _journal?.IsDirty == true;

        public static string ClientId
        {
            get
            {
                if (_clientId != null) return _clientId;
                string path = Path.Combine(Application.persistentDataPath, "economy-client-id.json");
                string id = File.Exists(path) ? JsonConvert.DeserializeObject<string>(File.ReadAllText(path)) : Guid.NewGuid().ToString("N");
                if (!MainWaveProgression.ValidId(id)) throw new InvalidDataException("기기의 경제 복구 식별자가 손상되었습니다.");
                if (!File.Exists(path)) LocalProgression.WriteAtomicJson(path, JsonConvert.SerializeObject(id));
                return _clientId = id;
            }
        }

        static EconomyBatchProgression()
        {
            LocalProgression.AccountChanged += ResetAccount;
            LocalProgression.Changed += Notify;
        }

        private static void ResetAccount()
        {
            FlushLocalWrites();
            _journal = null; _supported = _recovered = _requestPending = _queryRequired = _batchUnknown = _externalUnknown = _externalWrite = false;
            _recovering = _startupMaySplit = _sentThisSession = _huntsHeld = _blocked = false;
            _network = null;
            _recoveryDone = null; _fences.Clear(); _discardedInputs.Clear(); _discardAfterRevision = 0;
            _retryAt = _sealAfter = _nextHuntAt = 0;
            ResetStorageRetry();
            Status = null; Notify();
        }

        public static void ReceiveServerState(bool supported)
        {
            bool changedSession = !CurrentSession;
            _network = NetworkManager.Instance;
            _localGeneration = LocalProgression.AccountGeneration;
            _networkGeneration = NetworkManager.Instance != null ? NetworkManager.Instance.AccountGeneration : -1;
            _supported = supported && LocalProgression.State.Economy?.Server?.Version == EconomyBatchProtocol.Version;
            try
            {
                _journal ??= new EconomyInputJournal(LocalProgression.EconomyJournalPath, LocalProgression.AccountKey);
                if (!_supported)
                {
                    if (_journal.Data.InFlight != null || _journal.Data.Inputs.Count != 0 || LocalProgression.State.Economy.Hunts.Count != 0)
                        MarkBlocked("보존된 경제 입력을 지원하는 서버에서 결과를 먼저 확인해야 합니다.");
                    Notify(); return;
                }
                ProgressionAutosave.EnsureRunning();
                if (changedSession) { _recovered = false; _requestPending = false; _sentThisSession = false; ResetStorageRetry(); }
                var metadata = LocalProgression.State.Economy.Server;
                if (metadata.OwnerClientId != ClientId) { MarkBlocked("다른 기기의 경제 상태를 이 기기에서 실행할 수 없습니다."); return; }
                if (_journal.Data.Generation != 0 && _journal.Data.Generation != metadata.Generation) RetireInputs();
                if (_journal.Data.Generation != metadata.Generation)
                { _journal.Data.Generation = metadata.Generation; SaveJournal(); }
                ReconcileMageInputs(metadata);
                RestoreStorageRetryDeadline();
            }
            catch (Exception error) { MarkBlocked(error.Message); }
            Notify();
        }

        private static void ReconcileMageInputs(EconomyBatchStateDTO metadata)
        {
            var mode = EconomyBatchProtocol.MageMode(metadata);
            bool IsMage(EconomyInput input) => input.Command.Kind == EconomyCommandKind.MageEnhance;
            if (mode == MageEnhanceBatchMode.RecoveryOnly && _journal.Data.Inputs.Exists(IsMage))
            {
                var discarded = _journal.Data.Inputs.FindAll(IsMage);
                _journal.Data.Inputs.RemoveAll(IsMage);
                SaveDiscardedInputs(discarded);
            }
            else if (mode == MageEnhanceBatchMode.Unknown && (_journal.Data.Inputs.Exists(IsMage) || _journal.Data.InFlightInputs.Exists(IsMage)))
            {
                _supported = false;
                MarkUnknown("보존된 마탑 강화 입력을 지원하는 서버에서 결과를 먼저 확인해야 합니다.");
            }
        }

        public static void MarkUnknown(string reason) { _externalUnknown = true; Status = reason; Notify(); }
        private static void MarkBlocked(string reason) { _blocked = true; MarkUnknown(reason); }
        public static void ConfirmFullRefresh()
        {
            if (_blocked || !Available || _journal == null || _journal.Error != null ||
                LocalProgression.State.Economy.Server.OwnerClientId != ClientId || _journal.Data.Generation != LocalProgression.State.Economy.Server.Generation) return;
            _externalUnknown = false; Notify();
        }
        public static void SetExternalWritePending(bool pending)
        { if (_externalWrite != pending) { _externalWrite = pending; Notify(); } }
        public static void SetHuntsHeld(bool held)
        { if (_huntsHeld != held) { _huntsHeld = held; Notify(); } }

        public static EconomyProjection GetProjection()
        {
            if (!LocalProgression.TryGetCommittedState(out var state)) return default;
            if (_projectionAt != ProjectionRevision)
            { _projection = EconomyProjectionMath.Calculate(state, ProjectedCommands(state)); _projectionAt = ProjectionRevision; }
            return _projection;
        }

        private static IEnumerable<EconomyCommandDTO> ProjectedCommands(ProgressionState state)
        {
            if (_journal != null)
            {
                if (_journal.Data.InFlight != null && state.Economy.LastAppliedRequestId != _journal.Data.InFlight.RequestId)
                    foreach (var c in _journal.Data.InFlight.Commands) yield return c;
                foreach (var input in _journal.Data.Inputs) if (!input.Held) yield return input.Command;
            }
            foreach (var hunt in RemainingHunts(state)) yield return hunt;
        }

        public static EconomyInputStatus TryQueueGold(bool attack, int mode, string guideReceipt, out long localSequence) =>
            TryQueue(attack ? EconomyCommandKind.GoldAttack : EconomyCommandKind.GoldHealth, mode, guideReceipt, out localSequence);
        public static EconomyInputStatus TryQueueRuby(bool experience, out long localSequence) =>
            TryQueue(experience ? EconomyCommandKind.RubyExperience : EconomyCommandKind.RubyGold, 1, null, out localSequence);

        public static EconomyInputStatus TryQueueMageEnhance(int skillId) =>
            TryQueue(EconomyCommandKind.MageEnhance, 1, null, out _, skillId);

        public static EconomyInputStatus TryQueueEquipmentEnhance(long id) =>
            TryQueue(EconomyCommandKind.EquipmentEnhance, 1, null, out _, gearId: id);

        private static EconomyInputStatus TryQueue(EconomyCommandKind kind, int mode, string guideReceipt, out long sequence, int? skillId = null, long? gearId = null)
        {
            sequence = 0;
            bool gold = kind == EconomyCommandKind.GoldAttack || kind == EconomyCommandKind.GoldHealth;
            if (gold ? mode != 1 && mode != 10 && mode != -1 : mode != 1) return EconomyInputStatus.InvalidRequest;
            if (!Available || !RecoveryComplete) return EconomyInputStatus.NotReady;
            if (!CanAcceptGrowth) return _journal?.Error != null ? EconomyInputStatus.SaveFailed : EconomyInputStatus.Busy;
            bool mage = kind == EconomyCommandKind.MageEnhance;
            if (mage && (!SupportsMageEnhance || !skillId.HasValue || !LocalProgression.State.MageSkills.ContainsKey(skillId.Value)))
                return EconomyInputStatus.NotReady;
            bool gear = kind == EconomyCommandKind.EquipmentEnhance;
            if (gear && (!SupportsEquipmentEnhance || gearId == null)) return EconomyInputStatus.NotReady;
            if (!gold && !mage && !gear && (!RubyProgression.Unlocked || kind == EconomyCommandKind.RubyExperience && LocalProgression.State.AccountLevel >= BalanceMath.AccountCap))
                return EconomyInputStatus.NotReady;
            if (_journal.Data.Inputs.Concat(_journal.Data.InFlightInputs).Count(x => x.Command.Kind != EconomyCommandKind.Hunt) >= MaxInputs)
                return EconomyInputStatus.QueueFull;
            var projection = GetProjection();
            if (projection.Level(kind, skillId, gearId) >= LevelCap(kind, gearId)) return EconomyInputStatus.MaxLevel;
            var command = EconomyProjectionMath.Quote(projection, kind, mode, skillId, gearId);
            if (command == null) return EconomyInputStatus.InsufficientFunds;
            AppendHunts();
            var input = NewInput(command); input.GuideReceipt = guideReceipt;
            _journal.Data.Inputs.Add(input); sequence = input.Sequence;
            if (!IsStorageRetryWaiting) Status = null;
            if (_sealAfter == 0) _sealAfter = Time.realtimeSinceStartup + BatchWindow;
            SaveJournal(); Notify();
            return EconomyInputStatus.PendingSave;
        }

        private static EconomyInput NewInput(EconomyCommandDTO command) => new()
        { Sequence = _journal.Data.NextInputSequence++, Command = EconomyCopies.Copy(command) };

        private static int LevelCap(EconomyCommandKind kind, long? gearId = null) => kind == EconomyCommandKind.EquipmentEnhance ?
            EquipmentEconomy.EnhanceCap(ItemCode.DecodeRarity(LocalProgression.State.Equipment.Find(x => x.ServerInstanceId == gearId)?.Code ?? 0)) : kind == EconomyCommandKind.MageEnhance ? BalanceMath.MageCap :
            kind == EconomyCommandKind.GoldAttack || kind == EconomyCommandKind.GoldHealth ? BalanceMath.GoldCap : BalanceMath.RubyCap;

        public static bool RecoverPending(Action<bool> completed, bool previousSessionReplaced = false)
        {
            if (!Available || _journal == null || _recovering || _requestPending ||
                _journal.Data.InFlight == null && LocalProgression.State.PendingMainCompletion != null)
                return false;
            ResetStorageRetry();
            _recovering = true; _recoveryDone = completed; _startupMaySplit = previousSessionReplaced && !_sentThisSession;
            foreach (var input in _journal.Data.Inputs) if (input.Command.Kind != EconomyCommandKind.Hunt) input.Held = true;
            SaveJournal();
            if (_journal.Data.InFlight != null) { _queryRequired = true; _batchUnknown = true; }
            else
            {
                var receipt = LocalProgression.State.Economy.Server.PendingReceipt;
                if (receipt != null && receipt.RequestId != LocalProgression.State.Economy.LastAppliedRequestId)
                {
                    MarkUnknown("서버 결과에 대응하는 입력 기록이 없습니다. 현재 상태 확인이 필요합니다.");
                    FinishRecovery(false); return true;
                }
                _batchUnknown = false;
            }
            Pump(); return true;
        }

        public static bool TryGetHeldPreview(out int inputCount, out long goldCost, out long rubyCost, out long knowledgeCost) =>
            TryGetHeldPreview(out inputCount, out goldCost, out rubyCost, out knowledgeCost, out _);

        public static bool TryGetHeldPreview(out int inputCount, out long goldCost, out long rubyCost, out long knowledgeCost, out long stoneCost)
        {
            stoneCost = 0;
            inputCount = 0; goldCost = rubyCost = knowledgeCost = 0;
            if (!TryPrepareHeld(out var commands, out var after)) return false;
            inputCount = commands.Count;
            stoneCost = LocalProgression.Balance(eCurrency.EquipmentStone) - after.EquipmentStone;
            goldCost = LocalProgression.Balance(eCurrency.Gold) - after.Gold;
            rubyCost = LocalProgression.Balance(eCurrency.Ruby) - after.Ruby;
            knowledgeCost = LocalProgression.Balance(eCurrency.ArcaneKnowledge) - after.ArcaneKnowledge;
            return true;
        }

        private static bool TryPrepareHeld(out List<EconomyCommandDTO> commands, out EconomyProjection current)
        {
            commands = new(); current = default;
            if (!Available || !RecoveryComplete || HasUnknownResult || _requestPending || _externalWrite || _fences.Count != 0 ||
                _journal == null || _journal.IsDirty || _journal.Data.InFlight != null || _journal.Data.Inputs.Any(x => !x.Held)) return false;
            var state = LocalProgression.State;
            current = EconomyProjectionMath.Calculate(state, commands);
            var invalidTargets = new HashSet<(EconomyCommandKind, int?, long?)>();
            var discarded = new List<EconomyInput>();
            foreach (var input in _journal.Data.Inputs.Where(x => x.Held && x.Command.Kind != EconomyCommandKind.Hunt))
            {
                var command = EconomyCopies.Copy(input.Command);
                bool mage = command.Kind == EconomyCommandKind.MageEnhance;
                if (mage && !SupportsMageEnhance) return false;
                var target = EconomyBatchProtocol.GrowthTarget(command);
                if (invalidTargets.Contains(target)) { discarded.Add(input); continue; }
                command.ExpectedLevel = current.Level(command.Kind, command.SkillId, command.EquipmentInstanceId);
                int cap = LevelCap(command.Kind, command.EquipmentInstanceId);
                int priorLimit = command.CountLimit;
                command.CountLimit = Math.Min(command.Mode > 0 ? command.Mode : priorLimit, cap - command.ExpectedLevel);
                commands.Add(command);
                var next = (command.Kind != EconomyCommandKind.EquipmentEnhance || SupportsEquipmentEnhance) &&
                    command.CountLimit <= priorLimit && EconomyInputJournal.ValidCommand(command) &&
                    (command.Kind != EconomyCommandKind.RubyExperience || state.AccountLevel < BalanceMath.AccountCap)
                    ? EconomyProjectionMath.Calculate(state, commands) : default;
                if (next.Valid) { current = next; continue; }
                commands.RemoveAt(commands.Count - 1);
                invalidTargets.Add(target);
                discarded.Add(input);
            }
            if (discarded.Count > 0)
            {
                _journal.Data.Inputs.RemoveAll(discarded.Contains);
                SaveDiscardedInputs(discarded);
                return false;
            }
            return commands.Count > 0;
        }

        public static bool ResumeHeldInputs()
        {
            if (!TryPrepareHeld(out var commands, out _)) return false;
            int index = 0;
            foreach (var input in _journal.Data.Inputs.Where(x => x.Held && x.Command.Kind != EconomyCommandKind.Hunt))
            { input.Command = commands[index++]; input.Held = false; }
            _sealAfter = 0; Status = null; SaveJournal(); Notify(); return true;
        }

        // Quest, mailbox and attendance lookups can persist rollover, delivery or recovery changes on the server.
        // Keep their revision fence, but do not create a new hunt settlement just to read a menu.
        internal static bool CanQueueMenuSnapshot => Available && RecoveryComplete && !HasUnknownResult && !_blocked &&
            _journal != null && _journal.Error == null && LocalProgression.State.PendingMainCompletion == null;
        internal static bool CanRefreshMenuRecovery => Available && RecoveryComplete && _externalUnknown &&
            !_blocked && !_batchUnknown && !_queryRequired && !_requestPending && !HasUnresolvedBatch && _journal?.Error == null;
        internal static bool CanRetryMenuSnapshot => !Available || CanQueueMenuSnapshot || CanRefreshMenuRecovery;
        internal static bool TryQueueMenuSnapshot(Action<bool> completed)
        {
            if (!CanQueueMenuSnapshot || completed == null) return false;
            EnqueueFence(completed);
            _sealAfter = 0;
            Pump();
            return true;
        }

        public static bool FlushBeforeWrite(Action<bool> completed)
        {
            if (!Available || !RecoveryComplete || HasUnknownResult || _journal == null || completed == null) return false;
            AppendHunts(); EnqueueFence(completed);
            _sealAfter = 0; SaveJournal(); Pump(); return true;
        }

        private static void EnqueueFence(Action<bool> completed) =>
            _fences.Enqueue(new Fence { Sequence = _journal.Data.NextInputSequence - 1, Completed = completed });

        // 기존 루비 초기화가 전투를 멈춘 뒤에만 호출한다. 이후 같은 epoch의 처치는 더 받을 수 없다.
        public static bool SealCurrentRewardEpoch(Action<bool> completed)
        {
            if (!Available || !RecoveryComplete || HasUnknownResult || _journal == null || completed == null) return false;
            EconomyRewardEpochCutoffDTO cutoff = null;
            if (!LocalProgression.Execute("close-ruby-reward-epoch", state =>
            {
                long epoch = state.Economy.Server.RewardEpoch;
                cutoff = state.Economy.Cutoffs.FirstOrDefault(x => x.Epoch == epoch) ?? CaptureCutoff(state, epoch);
                if (cutoff == null) return false;
                if (!state.Economy.Cutoffs.Any(x => x.Epoch == epoch)) state.Economy.Cutoffs.Add(cutoff);
                return true;
            })) return false;
            AppendHunts();
            _journal.Data.Inputs.Add(NewInput(new EconomyCommandDTO
            {
                Kind = EconomyCommandKind.Hunt, RewardEpoch = cutoff.Epoch, Stage = cutoff.Stage,
                MainRunId = cutoff.MainRunId, BattleId = cutoff.BattleId, TotalKills = EconomyCopies.Kills(cutoff.TotalKills)
            }));
            EnqueueFence(completed);
            _sealAfter = 0; SaveJournal(); Pump(); return true;
        }

        internal static void Pump()
        {
            if (_journal == null || !CurrentSession) return;
            try
            {
                bool saved = _journal.Pump();
                if (_journal.Error != null) { HandleJournalFailure(); return; }
                PublishDiscardedInputs();
                if (saved) Notify();
                if (!Available || _journal.IsDirty || _requestPending) return;
                if (_queryRequired)
                {
                    // 재접속 확인 실패는 화면의 명시적 재확인을 기다린다. 이를 live retry로 바꾸면
                    // NotFound 뒤 보류할 소비가 같은 원문으로 자동 실행될 수 있다.
                    if ((_recovered || _recovering) && Time.realtimeSinceStartup >= _retryAt) SendQuery();
                    return;
                }
                if (_recovering && _journal.Data.InFlight == null) FinishRecovery(true);
                if (_storageRetryActive && _journal.Data.InFlight != null) { SendBatch(); return; }
                if (!_recovered || HasUnknownResult || _externalWrite || LocalProgression.State.PendingMainCompletion != null) return;
                if (_journal.Data.InFlight != null) { SendBatch(); return; }
                if (TryFinishFence()) return;
                if (!_huntsHeld && _fences.Count == 0 && Time.realtimeSinceStartup >= _nextHuntAt)
                {
                    _nextHuntAt = Time.realtimeSinceStartup + HuntWindow;
                    if (AppendHunts()) { SaveJournal(); return; }
                }
                if (_sealAfter > Time.realtimeSinceStartup) return;
                if (_huntsHeld && _fences.Count == 0) return;
                SealBatch();
            }
            catch (Exception error) { MarkUnknown(error.Message); FinishFences(false); FinishRecovery(false); }
        }

        private static void SealBatch()
        {
            var state = LocalProgression.State;
            if (RubyProgression.HasPendingRequest && _journal.Data.Inputs.Any(x => !x.Held && x.Command.Kind != EconomyCommandKind.Hunt))
            { MarkUnknown("기존 루비 요청의 결과를 먼저 확인해 주세요."); return; }
            var inputs = new List<EconomyInput>();
            var accepted = Accepted(state);
            int kills = 0;
            foreach (var input in _journal.Data.Inputs.Where(x => !x.Held))
            {
                if (_fences.Count != 0 && input.Sequence > _fences.Peek().Sequence) break;
                if (inputs.Count == EconomyBatchProtocol.MaxCommands) break;
                if (input.Command.Kind == EconomyCommandKind.Hunt)
                {
                    string run = input.Command.MainRunId ?? input.Command.BattleId;
                    if (!accepted.TryGetValue(run, out var counts)) { MarkUnknown("정산할 전투 회차를 확인하지 못했습니다."); return; }
                    int delta = input.Command.TotalKills.Sum(x => Math.Max(0, x.Count - counts.GetValueOrDefault((ulong)x.MonsterType)));
                    if (kills + delta > MaxBatchKills) break;
                    kills += delta;
                    foreach (var total in input.Command.TotalKills) counts[(ulong)total.MonsterType] = Math.Max(counts.GetValueOrDefault((ulong)total.MonsterType), total.Count);
                }
                inputs.Add(input);
            }
            if (inputs.Count == 0) return;
            var metadata = state.Economy.Server;
            var request = new OnEconomyBatchRequestDTO
            {
                RequestId = Guid.NewGuid().ToString("N"), Generation = metadata.Generation, Sequence = metadata.NextSequence,
                ExpectedEconomyRevision = state.ServerEconomyRevision, AppliedRewardEpoch = metadata.RewardEpoch,
                AcknowledgedRequestId = state.Economy.LastAppliedRequestId,
                IndependentGrowthFailures = metadata.IndependentGrowthFailures,
                QuestVersion = QuestManager.Instance?.ServerQuests?.Version,
                RewardEpochCutoffs = state.Economy.Cutoffs.Select(EconomyCopies.Copy).ToList(),
                Commands = inputs.Select(x => EconomyCopies.Copy(x.Command)).ToList()
            };
            EconomyBatchProtocol.ValidateRequest(request);
            _journal.Data.InFlight = request; _journal.Data.InFlightInputs = inputs;
            foreach (var input in inputs) _journal.Data.Inputs.Remove(input);
            _sealAfter = 0; SaveJournal();
        }

        private static void SendBatch()
        {
            var request = _journal.Data.InFlight;
            if (request == null || _requestPending || _externalWrite || !TryBeginRetrySend()) return;
            _requestPending = true; _sentThisSession = true; _startupMaySplit = false;
            long generation = _localGeneration, networkGeneration = _networkGeneration;
            NetworkManager.Instance.Call<OnEconomyBatchRequestDTO, OnEconomyBatchResponseDTO>(request,
                r => { if (Owns(generation, networkGeneration, request.RequestId)) ApplyResult(r, false); },
                e => { if (Owns(generation, networkGeneration, request.RequestId)) FailRequest(e); });
        }

        private static void SendQuery()
        {
            var request = _journal.Data.InFlight;
            if (request == null) { _queryRequired = false; return; }
            if (_requestPending || !TryBeginRetrySend()) return;
            _requestPending = true;
            long generation = _localGeneration, networkGeneration = _networkGeneration;
            NetworkManager.Instance.Call<OnGetEconomyBatchResultRequestDTO, OnEconomyBatchResponseDTO>(
                new() { RequestId = request.RequestId, Generation = request.Generation, Sequence = request.Sequence,
                    QuestVersion = QuestManager.Instance?.ServerQuests?.Version },
                r => { if (Owns(generation, networkGeneration, request.RequestId)) ApplyResult(r, true); },
                e => { if (Owns(generation, networkGeneration, request.RequestId)) FailRequest(e); });
        }

        private static bool Owns(long local, long network, string id) => CurrentSession && local == _localGeneration && network == _networkGeneration && _journal?.Data.InFlight?.RequestId == id;
        private static void ResetStorageRetry()
        {
            _storageRetryActive = _storageRetryPaused = _storageRetryAllowsInput = false;
            _storageRetryRemaining = _storageRetrySends = 0;
        }

        private static void RestoreStorageRetryDeadline()
        {
            var data = _journal?.Data;
            var metadata = LocalProgression.State.Economy?.Server;
            if (data?.InFlight == null || data.Account != LocalProgression.AccountKey || metadata == null ||
                data.Generation != metadata.Generation || data.InFlight.Generation != metadata.Generation) return;
            long remaining = data.StorageRetryNotBeforeUtcMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (remaining > 0) _retryAt = Math.Max(_retryAt, Time.realtimeSinceStartup + (float)(remaining / 1000d));
        }

        private static void PauseStorageRetry()
        {
            _storageRetryPaused = true; _batchUnknown = true;
            Status = "서버 결과 확인이 지연되고 있습니다. 잠시 후 육성 버튼에서 다시 확인해 주세요.";
        }

        // Query and NotFound resubmission share one budget and one server-requested cooldown.
        private static bool TryBeginRetrySend()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _retryAt) return false;
            if (!_storageRetryActive) return true;
            if (_storageRetryPaused || _externalUnknown || _recovering || !RecoveryComplete) return false;
            if (_storageRetryRemaining == 0) { PauseStorageRetry(); Notify(); return false; }
            _storageRetryRemaining--; _storageRetrySends++;
            return true;
        }

        public static bool TryResumeStorageRetry()
        {
            if (!Available || !RecoveryComplete || _recovering || !_storageRetryActive || !_storageRetryPaused ||
                _requestPending || _externalUnknown || _blocked || _journal?.Data.InFlight == null || _journal.Error != null ||
                Time.realtimeSinceStartup < _retryAt) return false;
            // A click authorizes one more transmission. A preceding NotFound keeps the original body ready to resend.
            _storageRetryPaused = false; _storageRetryRemaining = 1;
            Status = "이전 경제 요청의 결과를 확인하고 있습니다.";
            Pump(); Notify(); return true;
        }

        private static void FailRequest(PlayFabError error)
        {
            _requestPending = false; _queryRequired = _batchUnknown = true;
            float delay = 2f;
            if (NetworkManager.TryGetStorageThrottle(error, out int? retryAfterSeconds))
            {
                if (CurrentSession && _recovered && !_recovering && _journal?.Data.InFlight != null)
                {
                    if (!_storageRetryActive) { _storageRetryActive = true; _storageRetryRemaining = StorageRetrySendLimit; _storageRetrySends = 0; }
                    _storageRetryAllowsInput = true;
                    delay = retryAfterSeconds.HasValue ? Math.Max(0, retryAfterSeconds.Value) : Math.Min(30, 2 << Math.Min(_storageRetrySends, 4));
                }
                else delay = retryAfterSeconds.HasValue ? Math.Max(0, retryAfterSeconds.Value) : 2f;
                if (_journal?.Data.InFlight != null)
                {
                    long deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)Math.Ceiling(delay * 1000d);
                    _journal.Data.StorageRetryNotBeforeUtcMs = Math.Max(_journal.Data.StorageRetryNotBeforeUtcMs, deadline);
                    SaveJournal();
                }
            }
            else _storageRetryAllowsInput = false;
            _retryAt = Math.Max(_retryAt, Time.realtimeSinceStartup + delay);
            Status = "이전 경제 요청의 결과를 확인하고 있습니다.";
            if (_storageRetryActive && _storageRetryRemaining == 0) PauseStorageRetry();
            FinishFences(false); FinishRecovery(false); Notify();
        }

        private static void ApplyResult(OnEconomyBatchResponseDTO response, bool query)
        {
            // 확정 상태와 입력 일지를 모두 반영할 때까지 동기 UI 콜백의 재전송을 막는다.
            _requestPending = true;
            var request = _journal.Data.InFlight;
            long localGeneration = _localGeneration, networkGeneration = _networkGeneration;
            try
            {
                EconomyBatchProtocol.ValidateResponse(response);
                EconomyBatchProtocol.MatchRequest(response, request);
                bool alreadyApplied = LocalProgression.State.Economy.LastAppliedRequestId == request.RequestId;
                var loot = !alreadyApplied && !_recovering && response.Status == EconomyBatchStatus.Completed &&
                    response.Receipt.Results.Any(x => x.Kind == EconomyCommandKind.Hunt && x.Status == EconomyCommandStatus.Applied && x.GrantedGold > 0)
                    ? KingdomIdle.Rewards.LootNotifications.Capture(false, false) : null;
                bool saved = LocalProgression.Execute("server-economy-batch", state =>
                {
                    if (!ApplyServerState(state, response.CurrentState, response.EconomyRevision.Value)) return false;
                    if (response.Status == EconomyBatchStatus.Completed)
                    {
                        state.Economy.LastAppliedRequestId = request.RequestId;
                        for (int i = 0; i < response.Receipt.Results.Count; i++)
                            if (response.Receipt.Results[i].Status == EconomyCommandStatus.Applied && request.Commands[i].Kind == EconomyCommandKind.GoldAttack)
                                Direction.GameDirectInteraction.RecordSuccess(state, Direction.GuideAction.AttackOnce, _journal.Data.InFlightInputs[i].GuideReceipt);
                    }
                    return true;
                });
                if (!saved) throw new IOException("경제 결과를 저장하지 못했습니다: " + LocalProgression.LastError);
                if (!Owns(localGeneration, networkGeneration, request.RequestId)) return;
                ReconcileMageInputs(response.CurrentState.EconomyBatchState);
                if (response.CurrentState.Equipment != null) EquipmentManager.Instance?.RestoreEquipment();
                if (response.CurrentState.MageTowerState != null)
                    KingdomIdle.MageTower.MageTowerManager.Instance?.NotifyCommitted();
                _queryRequired = _batchUnknown = false;
                if (response.Status == EconomyBatchStatus.NotFound)
                {
                    bool split = query && _recovering && _startupMaySplit && !_sentThisSession &&
                        response.CurrentState.EconomyBatchState.Generation == request.Generation && response.CurrentState.EconomyBatchState.NextSequence == request.Sequence;
                    if (split)
                    {
                        _journal.Data.AbandonedRequest = request; _journal.Data.AbandonedAtRevision = response.EconomyRevision.Value;
                        foreach (var input in _journal.Data.InFlightInputs) { input.Held = input.Command.Kind != EconomyCommandKind.Hunt; _journal.Data.Inputs.Add(input); }
                        ClearInFlight(); _journal.Data.Inputs.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
                        SaveJournal(); ReconcileMageInputs(response.CurrentState.EconomyBatchState); return;
                    }
                    if (_recovering) { _batchUnknown = true; Status = "이전 요청을 같은 ID로 확인해야 합니다."; FinishRecovery(false); return; }
                    if (_storageRetryActive)
                    {
                        _batchUnknown = true;
                        if (_storageRetryRemaining == 0) { PauseStorageRetry(); Notify(); return; }
                    }
                    return; // Pump resends the frozen request after this callback finishes.
                }
                if (response.Status == EconomyBatchStatus.Retired) { RetireInputs(); return; }
                if (response.Status != EconomyBatchStatus.Completed) { MarkUnknown("경제 요청 내용이 충돌해 실행을 보류했습니다."); FinishRecovery(false); return; }
                ResetStorageRetry(); _retryAt = 0;
                if (loot != null) KingdomIdle.Rewards.LootNotifications.PublishEconomyBatch(loot, response.Receipt);
                ResolveCompletedInputs(response.Receipt, alreadyApplied);
                if (CurrentSession && localGeneration == _localGeneration && networkGeneration == _networkGeneration &&
                    LocalProgression.State.Economy.LastAppliedRequestId == request.RequestId)
                {
                    var quests = QuestManager.Instance;
                    if (response.QuestRefreshRequired == true ||
                        response.QuestChanges != null && quests != null && !quests.TryApplyChanges(response.QuestChanges))
                        quests?.RequestRefresh("economy-batch");
                    else if (!alreadyApplied && response.Quests != null)
                        QuestManager.Receive(response.Quests, "economy-batch");
                    else if (!alreadyApplied && !response.QuestRefreshRequired.HasValue && response.QuestChanges == null &&
                        response.Receipt.Results.Any(x => x.Status == EconomyCommandStatus.Applied && x.AppliedCount > 0))
                        quests?.RequestRefresh("economy-batch");
                }
            }
            catch (Exception error)
            {
                _batchUnknown = _queryRequired = true; _storageRetryAllowsInput = false;
                _retryAt = Math.Max(_retryAt, Time.realtimeSinceStartup + 2f);
                Status = error.Message;
                if (_storageRetryActive && _storageRetryRemaining == 0) PauseStorageRetry();
                FinishFences(false); FinishRecovery(false); Notify();
            }
            finally
            {
                if (_journal != null && localGeneration == _localGeneration && networkGeneration == _networkGeneration)
                {
                    _requestPending = false;
                    Notify();
                }
            }
        }

        private static void ResolveCompletedInputs(EconomyBatchReceiptDTO receipt, bool alreadyApplied)
        {
            bool stopSpending = false, invalidHunt = false;
            var failedTargets = new HashSet<(EconomyCommandKind, int?, long?)>();
            var discarded = new List<EconomyInput>();
            for (int i = 0; i < receipt.Results.Count; i++)
            {
                var result = receipt.Results[i]; var input = _journal.Data.InFlightInputs[i];
                if (result.Status == EconomyCommandStatus.Rejected)
                {
                    if (EconomyBatchProtocol.IsIsolatedGrowthRejection(result, receipt.IndependentGrowthFailures))
                        failedTargets.Add(EconomyBatchProtocol.GrowthTarget(input.Command));
                    else stopSpending = true;
                }
                if (input.Command.Kind == EconomyCommandKind.Hunt)
                {
                    if (result.Status == EconomyCommandStatus.Applied) continue;
                    input.Held = result.Status == EconomyCommandStatus.Rejected &&
                        result.ErrorCode != Protocol.Message.Data.eErrorCode.EconomyRevisionMismatch;
                    invalidHunt |= input.Held;
                    _journal.Data.Inputs.Add(input);
                }
                else if (result.Status != EconomyCommandStatus.Applied) discarded.Add(input);
                else if (!alreadyApplied) CompleteInput(input, true, "강화가 적용되었습니다.");
            }
            if (stopSpending || failedTargets.Count != 0)
            {
                bool Cancel(EconomyInput input) => input.Command.Kind != EconomyCommandKind.Hunt &&
                    (stopSpending || failedTargets.Contains(EconomyBatchProtocol.GrowthTarget(input.Command)));
                discarded.AddRange(_journal.Data.Inputs.Where(Cancel));
                _journal.Data.Inputs.RemoveAll(Cancel);
            }
            ClearInFlight(); _journal.Data.Inputs.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            if (!HasUnknownResult) Status = null;
            if (invalidHunt) { MarkBlocked("보존된 사냥 기록을 서버가 거절했습니다. 기록을 유지하고 실행을 보류했습니다."); FinishFences(false); }
            SaveDiscardedInputs(discarded);
        }

        private static void SaveDiscardedInputs(List<EconomyInput> discarded)
        {
            SaveJournal();
            if (discarded.Count != 0)
            { _discardedInputs.AddRange(discarded); _discardAfterRevision = _journal.Revision; }
            Notify();
        }

        private static void PublishDiscardedInputs()
        {
            if (_discardedInputs.Count == 0 || _journal.SavedRevision < _discardAfterRevision) return;
            var discarded = _discardedInputs.ToArray(); _discardedInputs.Clear(); _discardAfterRevision = 0;
            Status = FailureMessage;
            foreach (var input in discarded) CompleteInput(input, false, FailureMessage);
            if (SpendingFailed != null)
                foreach (Action observer in SpendingFailed.GetInvocationList())
                    try { observer(); } catch (Exception error) { Debug.LogException(error); }
            Notify();
        }

        private static void ClearInFlight()
        {
            _journal.Data.InFlight = null; _journal.Data.InFlightInputs = new(); _journal.Data.StorageRetryNotBeforeUtcMs = 0;
        }
        private static void RetireInputs()
        {
            var old = _journal.Data;
            if (old.InFlight != null) { old.AbandonedRequest = old.InFlight; old.AbandonedAtRevision = LocalProgression.State.ServerEconomyRevision; }
            if (old.Inputs.Count != 0 || old.InFlight != null)
            {
                string archive = JsonConvert.SerializeObject(old);
                if (!LocalProgression.Execute("retired-economy-inputs", s => { s.Modules["retired-economy-inputs"] = archive; return true; }))
                    throw new IOException("이전 기기의 입력 기록을 보존하지 못했습니다.");
            }
            ResetStorageRetry(); _retryAt = 0;
            old.Inputs.Clear(); ClearInFlight(); old.Generation = LocalProgression.State.Economy.Server.Generation;
            _batchUnknown = _queryRequired = false;
            Status = "다른 기기에서 접속하여 이전 기기의 미처리 강화가 취소되었습니다.";
            SaveJournal(); Notify();
        }

        private static void FinishRecovery(bool ok)
        { if (!_recovering) return; _recovering = false; _recovered = ok; var done = _recoveryDone; _recoveryDone = null; done?.Invoke(ok); Notify(); }
        private static bool TryFinishFence()
        {
            if (_fences.Count == 0 || _externalWrite || HasUnknownResult || _journal.IsDirty || _journal.Data.InFlight != null ||
                _journal.Data.Inputs.Any(x => !x.Held && x.Sequence <= _fences.Peek().Sequence)) return false;
            var fence = _fences.Dequeue(); fence.Completed(true); Notify(); return true;
        }
        private static void FinishFences(bool ok)
        {
            var pending = _fences.ToArray(); _fences.Clear();
            foreach (var fence in pending)
                try { fence.Completed(ok); } catch (Exception error) { Debug.LogException(error); }
        }
        private static void SaveJournal() { _journal.Changed(); _journal.Pump(); }
        internal static void FlushLocalWrites() { if (_journal != null) _journal.Pump(true); }
        private static void HandleJournalFailure()
        {
            string error = _journal.Error;
            var committed = _journal.Committed.Inputs.Select(x => x.Sequence).Concat(_journal.Committed.InFlightInputs.Select(x => x.Sequence)).ToHashSet();
            foreach (var input in _journal.Data.Inputs.Where(x => !committed.Contains(x.Sequence) && x.Command.Kind != EconomyCommandKind.Hunt)) CompleteInput(input, false, "강화 입력을 저장하지 못했습니다.");
            _discardedInputs.Clear(); _discardAfterRevision = 0;
            _journal.RestoreCommitted(); MarkBlocked("경제 입력 저장 실패: " + error); FinishFences(false); FinishRecovery(false);
            if (_journal.Data.InFlight != null) _queryRequired = _batchUnknown = true;
        }
        private static void CompleteInput(EconomyInput input, bool ok, string message)
        {
            if (InputCompleted == null) return;
            foreach (Action<EconomyInputCompletion> observer in InputCompleted.GetInvocationList())
                try { observer(new EconomyInputCompletion(input, ok, message)); } catch (Exception error) { Debug.LogException(error); }
        }
        private static void Notify()
        {
            ProjectionRevision++;
            if (Changed == null) return;
            foreach (Action observer in Changed.GetInvocationList()) try { observer(); } catch (Exception error) { Debug.LogException(error); }
        }

        private static Dictionary<string, Dictionary<ulong, int>> Accepted(ProgressionState state)
        {
            var result = new Dictionary<string, Dictionary<ulong, int>>();
            if (state.MainBattle != null) result[state.MainBattle.RunId] = new(state.MainBattle.AcceptedKills);
            if (state.Economy?.Dungeon != null) result[state.Economy.Dungeon.BattleId] = EconomyCopies.Counts(state.Economy.Dungeon.AcceptedKills);
            return result;
        }

        private static IEnumerable<EconomyCommandDTO> RemainingHunts(ProgressionState state)
        {
            var countsByRun = Accepted(state);
            if (_journal != null)
            {
                var queued = _journal.Data.InFlightInputs.Concat(_journal.Data.Inputs).Select(x => x.Command).Where(x => x.Kind == EconomyCommandKind.Hunt);
                foreach (var c in queued)
                    if (countsByRun.TryGetValue(c.MainRunId ?? c.BattleId, out var counts))
                        foreach (var t in c.TotalKills) counts[(ulong)t.MonsterType] = Math.Max(counts.GetValueOrDefault((ulong)t.MonsterType), t.Count);
            }
            foreach (var source in state.Economy.Hunts)
            {
                if (!countsByRun.TryGetValue(source.MainRunId ?? source.BattleId, out var counts)) continue;
                while (source.TotalKills.Any(t => t.Count > counts.GetValueOrDefault((ulong)t.MonsterType)))
                {
                    int remaining = MaxBatchKills;
                    foreach (var t in source.TotalKills)
                    {
                        ulong type = (ulong)t.MonsterType;
                        int take = Math.Min(remaining, Math.Max(0, t.Count - counts.GetValueOrDefault(type)));
                        counts[type] = counts.GetValueOrDefault(type) + take; remaining -= take;
                    }
                    var command = EconomyCopies.Copy(source); command.TotalKills = EconomyCopies.Kills(counts);
                    yield return command;
                }
            }
        }
        private static bool AppendHunts()
        {
            var hunts = RemainingHunts(LocalProgression.State).ToArray();
            foreach (var hunt in hunts) _journal.Data.Inputs.Add(NewInput(hunt));
            return hunts.Length != 0;
        }

        public static bool RecordHunt(ProgressionState state, long stage, string mainRunId, string battleId, IReadOnlyDictionary<eMonsterType, int> cumulative)
        {
            var metadata = state.Economy?.Server;
            if (metadata == null || !Available) return true;
            if (!EconomyCopies.ValidRun(stage, mainRunId, battleId) || cumulative == null || cumulative.Count > 10 || cumulative.Any(x => x.Value < 0 || x.Value > short.MaxValue)) return false;
            var latest = state.Economy.Hunts.LastOrDefault(x => x.RewardEpoch == metadata.RewardEpoch && (long)x.Stage == stage && x.MainRunId == mainRunId && x.BattleId == battleId);
            if (latest == null)
            {
                latest = new EconomyCommandDTO { Kind = EconomyCommandKind.Hunt, Stage = (Protocol.Message.Data.eStage)stage, MainRunId = mainRunId, BattleId = battleId, RewardEpoch = metadata.RewardEpoch, TotalKills = new() };
                state.Economy.Hunts.Add(latest);
            }
            var counts = EconomyCopies.Counts(latest.TotalKills);
            foreach (var pair in cumulative) counts[(ulong)pair.Key] = Math.Max(counts.GetValueOrDefault((ulong)pair.Key), pair.Value);
            latest.TotalKills = EconomyCopies.Kills(counts); return true;
        }

        public static bool ApplyBatchMetadata(ProgressionState state, EconomyBatchStateDTO metadata, long revision)
        {
            if (metadata == null) return true;
            EconomyBatchProtocol.ValidateMetadata(metadata, revision);
            if (revision < state.ServerEconomyRevision) return false;
            state.Economy ??= new EconomyLocalState();
            var old = state.Economy.Server;
            if (old != null && old.Generation != metadata.Generation)
            {
                if (state.Economy.Hunts.Count != 0 || state.Economy.Cutoffs.Count != 0) state.Modules["retired-economy-hunts"] = JsonConvert.SerializeObject(state.Economy);
                state.Economy.Hunts.Clear(); state.Economy.Cutoffs.Clear(); state.Economy.LastAppliedRequestId = null;
            }
            else if (old != null && old.RewardEpoch != metadata.RewardEpoch)
            {
                var cutoff = CaptureCutoff(state, old.RewardEpoch);
                if (cutoff != null && !state.Economy.Cutoffs.Any(x => x.Epoch == old.RewardEpoch)) state.Economy.Cutoffs.Add(cutoff);
            }
            state.Economy.Server = EconomyCopies.Copy(metadata);
            state.Economy.Cutoffs.RemoveAll(x => !metadata.RewardEpochs.Any(e => e.Id == x.Epoch));
            state.ServerEconomyRevision = revision; return true;
        }

        private static EconomyRewardEpochCutoffDTO CaptureCutoff(ProgressionState state, long epoch)
        {
            var last = state.Economy.Hunts.LastOrDefault(x => x.RewardEpoch == epoch);
            if (last == null && state.ActiveDungeon != null && state.Economy.Dungeon != null) last = new EconomyCommandDTO
            { Stage = (Protocol.Message.Data.eStage)state.Economy.Dungeon.DungeonId, BattleId = state.Economy.Dungeon.BattleId, TotalKills = EconomyCopies.Kills(state.Economy.Dungeon.AcceptedKills) };
            if (last == null && state.MainBattle != null) last = new EconomyCommandDTO
            { Stage = (Protocol.Message.Data.eStage)state.MainBattle.Stage, MainRunId = state.MainBattle.RunId, TotalKills = EconomyCopies.Kills(state.MainBattle.AcceptedKills) };
            return last == null ? null : new EconomyRewardEpochCutoffDTO
            { Epoch = epoch, Stage = last.Stage, MainRunId = last.MainRunId, BattleId = last.BattleId, TotalKills = EconomyCopies.Kills(last.TotalKills) };
        }

        public static bool ApplyServerState(ProgressionState state, EconomyStateDTO response, long revision)
        {
            if (response == null || !ApplyBatchMetadata(state, response.EconomyBatchState, revision)) return false;
            if (!RubyProgression.ApplyServerState(state, response.RubyGrowthState) || !MainWaveProgression.ApplyServerState(state, response.MainBattle)) return false;
            UserManager.ApplyCurrency(state, response.Currency);
            if (response.Equipment != null) UserManager.ApplyEquipment(state, response.Equipment);
            if (response.MageTowerState != null) UserManager.ApplyMageTower(state, response.MageTowerState);
            state.AttackLevel = checked((int)response.EnchantATKCount); state.HealthLevel = checked((int)response.EnchantHPCount);
            state.AccountLevel = response.Level; state.Experience = response.Exp; state.Kills = response.KillScore;
            state.MainStage = (long)response.StageState.CurrentStage; state.HighestMainClear = (long)response.StageState.MainClear;
            state.CycleFrontierStage = Math.Max(state.MainStage, (long)response.StageState.HighestStage);
            state.CycleBossStage = UserManager.CycleBossClears(state.CycleFrontierStage);
            state.Economy.Dungeon = EconomyCopies.Copy(response.DungeonBattle);
            ReconcileSettledHunts(state); return true;
        }

        public static void ReconcileSettledHunts(ProgressionState state)
        {
            var accepted = Accepted(state);
            state.Economy.Hunts.RemoveAll(x => accepted.TryGetValue(x.MainRunId ?? x.BattleId, out var counts) &&
                x.TotalKills.All(t => t.Count <= counts.GetValueOrDefault((ulong)t.MonsterType)));
        }

        internal static void ValidateSaved(ProgressionState state)
        {
            var economy = state.Economy;
            if (economy == null) return;
            if (economy.Server != null) EconomyBatchProtocol.ValidateMetadata(economy.Server, state.ServerEconomyRevision);
            if (economy.Hunts == null || economy.Cutoffs == null || economy.Hunts.Count > 104 || economy.Cutoffs.Count > 102 ||
                economy.Hunts.Any(x => !EconomyInputJournal.ValidCommand(x)) || economy.Cutoffs.Any(x => x == null || x.Epoch < 1 ||
                    !EconomyCopies.ValidRun((long)x.Stage, x.MainRunId, x.BattleId) || !EconomyCopies.ValidKills(x.TotalKills)) ||
                economy.Cutoffs.Select(x => x.Epoch).Distinct().Count() != economy.Cutoffs.Count)
                throw new InvalidDataException("Invalid saved economy state.");
        }
    }
}
