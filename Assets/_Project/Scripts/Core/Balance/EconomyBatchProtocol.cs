using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Protocol.Message.Data;
using Protocol.Message.Dto;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    // 같은 검사를 통신 경계와 디스크에 보관한 요청에 적용한다.
    internal static class EconomyBatchProtocol
    {
        internal const int Version = 1, MaxCommands = 16, MaxEpochs = 102;

        internal static MageEnhanceBatchMode MageMode(EconomyBatchStateDTO state) =>
            state?.MageEnhanceMode == MageEnhanceBatchMode.Unknown && state.SupportsMageEnhance
                ? MageEnhanceBatchMode.Active : state?.MageEnhanceMode ?? MageEnhanceBatchMode.Unknown;

        internal static void ValidateRequest(OnEconomyBatchRequestDTO request)
        {
            if (request == null || !ValidIdentity(request.RequestId, request.Generation, request.Sequence) ||
                request.ExpectedEconomyRevision < 0 || request.AppliedRewardEpoch < 1 ||
                (request.AcknowledgedRequestId != null && !MainWaveProgression.ValidId(request.AcknowledgedRequestId)) ||
                request.Commands == null || request.Commands.Count < 1 || request.Commands.Count > MaxCommands ||
                request.RewardEpochCutoffs == null || request.RewardEpochCutoffs.Count > MaxEpochs)
                Invalid("Invalid economy batch request.");
            foreach (var command in request.Commands)
            {
                if (command == null || !Enum.IsDefined(typeof(EconomyCommandKind), command.Kind)) Invalid("Invalid economy command.");
                if (command.Kind == EconomyCommandKind.QuestClaim)
                {
                    if (!ValidQuestCommand(command)) Invalid("Invalid quest claim command.");
                    continue;
                }
                if (command.QuestClaim != null) Invalid("Unexpected quest claim fields.");
                if (command.Kind == EconomyCommandKind.EquipmentEnhance)
                {
                    if (!ValidEquipmentCommand(command)) Invalid("Invalid equipment enhancement fields.");
                    continue;
                }
                if (command.EquipmentInstanceId.HasValue) Invalid("Unexpected equipment ID on economy command.");
                if (command.Kind == EconomyCommandKind.MageEnhance)
                {
                    if (!ValidMageCommand(command)) Invalid("Invalid mage enhancement command fields.");
                    continue;
                }
                if (command.Kind == EconomyCommandKind.MageAwakening)
                {
                    if (!ValidAwakeningCommand(command)) Invalid("Invalid mage awakening command.");
                    continue;
                }
                if (command.SkillId.HasValue) Invalid("Unexpected skill ID on economy command.");
                if (command.Kind == EconomyCommandKind.Hunt)
                {
                    ValidateHunt(command.Stage, command.MainRunId, command.BattleId, command.TotalKills);
                    if (command.RewardEpoch < 1 || command.Mode != 0 || command.ExpectedLevel != 0 || command.CountLimit != 0 || command.SpendLimit != 0)
                        Invalid("Invalid hunt command fields.");
                }
                else
                {
                    bool ruby = command.Kind == EconomyCommandKind.RubyGold || command.Kind == EconomyCommandKind.RubyExperience;
                    int cap = ruby ? BalanceMath.RubyCap : BalanceMath.GoldCap;
                    if (command.ExpectedLevel < 0 || command.ExpectedLevel >= cap || command.CountLimit < 1 ||
                        command.CountLimit > cap - command.ExpectedLevel || command.SpendLimit <= 0 ||
                        (ruby ? command.Mode != 1 || command.CountLimit != 1 : command.Mode != 1 && command.Mode != 10 && command.Mode != -1) ||
                        (!ruby && command.Mode > 0 && command.CountLimit != Math.Min(command.Mode, cap - command.ExpectedLevel)) ||
                        (long)command.Stage != 0 || command.MainRunId != null || command.BattleId != null || command.RewardEpoch != 0 || command.TotalKills != null)
                        Invalid("Invalid growth command fields.");
                }
            }
            foreach (var cutoff in request.RewardEpochCutoffs)
            {
                if (cutoff == null || cutoff.Epoch < 1 || cutoff.Epoch > request.AppliedRewardEpoch) Invalid("Invalid reward epoch cutoff.");
                ValidateHunt(cutoff.Stage, cutoff.MainRunId, cutoff.BattleId, cutoff.TotalKills);
            }
            if (request.RewardEpochCutoffs.Select(x => x.Epoch).Distinct().Count() != request.RewardEpochCutoffs.Count)
                Invalid("Duplicate reward epoch cutoff.");
        }

        internal static bool ValidIdentity(string id, long generation, long sequence) =>
            MainWaveProgression.ValidId(id) && generation > 0 && sequence > 0 && sequence < long.MaxValue;

        internal static bool ValidEquipmentCommand(EconomyCommandDTO command) =>
            command.EquipmentInstanceId > 0 && !command.SkillId.HasValue && command.Mode == 1 && command.CountLimit == 1 &&
            command.ExpectedLevel >= 0 && command.ExpectedLevel < 15 && command.SpendLimit > 0 &&
            (long)command.Stage == 0 && command.MainRunId == null && command.BattleId == null && command.RewardEpoch == 0 && command.TotalKills == null;

        internal static bool ValidMageCommand(EconomyCommandDTO command) =>
            command.SkillId.HasValue && MageSkillRules.IsAvailable(command.SkillId.Value) &&
            command.Mode == 1 && command.CountLimit == 1 && command.ExpectedLevel >= 0 && command.ExpectedLevel < BalanceMath.MageCap &&
            command.SpendLimit > 0 && (long)command.Stage == 0 && command.MainRunId == null && command.BattleId == null &&
            command.RewardEpoch == 0 && command.TotalKills == null;

        internal static bool IsQuestAwakeningCommand(EconomyCommandDTO command) =>
            command.Kind is EconomyCommandKind.QuestClaim or EconomyCommandKind.MageAwakening;

        internal static bool ValidAwakeningCommand(EconomyCommandDTO command) =>
            ValidMageCommand(command) && command.ExpectedLevel < MageSkillRules.BloomAwakening &&
            command.SpendLimit == command.ExpectedLevel + 1 && !command.EquipmentInstanceId.HasValue && command.QuestClaim == null;

        internal static bool ValidQuestCommand(EconomyCommandDTO command)
        {
            var claim = command.QuestClaim;
            return claim?.Targets != null && claim.Targets.Count is >= 1 and <= 128 &&
                (claim.Category.HasValue ? Enum.IsDefined(typeof(QuestCategory), claim.Category.Value) && ValidPeriod(claim.BoardPeriod) :
                    claim.BoardPeriod == null && claim.Targets.Count == 1) &&
                claim.Targets.All(t => t != null && t.QuestId > 0 && ValidPeriod(t.PeriodKey)) &&
                claim.Targets.Select(t => (t.QuestId, t.PeriodKey)).Distinct().Count() == claim.Targets.Count &&
                command.Mode == 0 && command.ExpectedLevel == 0 && command.CountLimit == 0 && command.SpendLimit == 0 &&
                !command.SkillId.HasValue && !command.EquipmentInstanceId.HasValue && (long)command.Stage == 0 &&
                command.MainRunId == null && command.BattleId == null && command.RewardEpoch == 0 && command.TotalKills == null;
        }

        private static bool ValidPeriod(string period) => period == "permanent" ||
            DateTime.TryParseExact(period, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        internal static (EconomyCommandKind Kind, int? SkillId, long? EquipmentInstanceId) GrowthTarget(EconomyCommandDTO command) =>
            (command.Kind, command.SkillId, command.EquipmentInstanceId);

        internal static bool IsIsolatedGrowthRejection(EconomyCommandResultDTO result, bool independentGrowthFailures) =>
            result.Status == EconomyCommandStatus.Rejected && (result.Kind switch
            {
                EconomyCommandKind.MageEnhance => result.ErrorCode is eErrorCode.eNotEnoughArcaneKnowledge or eErrorCode.eNotExistSkillCodeInput or
                    eErrorCode.eEnchantMaxLevelSKILL or eErrorCode.EconomyExpectedLevelMismatch or eErrorCode.NumericLimit or eErrorCode.UnsupportedProtocolVersion,
                EconomyCommandKind.GoldAttack or EconomyCommandKind.GoldHealth => independentGrowthFailures &&
                    result.ErrorCode is eErrorCode.EconomyExpectedLevelMismatch or eErrorCode.InvalidEnchantCount or eErrorCode.eNotEnoughGold,
                EconomyCommandKind.RubyGold or EconomyCommandKind.RubyExperience => independentGrowthFailures &&
                    result.ErrorCode is eErrorCode.EconomyExpectedLevelMismatch or eErrorCode.RubyGrowthLocked or eErrorCode.RubyGrowthMaxLevel or
                        eErrorCode.RubyExperienceMaxLevel or eErrorCode.NumericLimit or eErrorCode.InsufficientRuby,
                EconomyCommandKind.QuestClaim => result.QuestClaimResults != null && IsQuestTargetError(result.ErrorCode),
                EconomyCommandKind.MageAwakening => result.ErrorCode is eErrorCode.eNotExistSkillCodeInput or
                    eErrorCode.EconomyExpectedLevelMismatch or eErrorCode.AwakeningLevelLimit or eErrorCode.eNotEnoughSkillCount,
                _ => false
            });

        private static void ValidateHunt(Protocol.Message.Data.eStage stage, string mainRunId, string battleId, System.Collections.Generic.List<HuntResult> kills)
        {
            long id = (long)stage;
            int kind = (int)((id >> 28) & 3), chapter = (int)((id >> 16) & 0xfff), wave = (int)(id & 0xffff);
            bool validStage = kind == 0 ? chapter > 0 && wave >= 1 && wave <= 11 :
                (kind == 1 || kind == 2) && chapter >= 1 && chapter <= 5 && wave == 1;
            if (!validStage || id != (0x200000000L | ((long)kind << 28) | ((long)chapter << 16) | (uint)wave) ||
                (kind == 0 ? !MainWaveProgression.ValidId(mainRunId) || battleId != null : mainRunId != null || !MainWaveProgression.ValidId(battleId)))
                Invalid("Invalid economy hunt run.");
            ValidateKills(kills);
        }

        private static void ValidateKills(System.Collections.Generic.List<HuntResult> kills)
        {
            if (kills == null || kills.Count > 10 || kills.Any(k => k.Count <= 0 || (ulong)k.MonsterType == 0) ||
                kills.Select(k => k.MonsterType).Distinct().Count() != kills.Count)
                Invalid("Invalid economy cumulative kills.");
        }

        internal static void ValidateMetadata(EconomyBatchStateDTO state, long? revision)
        {
            if (state == null || state.Version < 1 || !Enum.IsDefined(typeof(MageEnhanceBatchMode), state.MageEnhanceMode) ||
                !MainWaveProgression.ValidId(state.OwnerClientId) || state.Generation < 1 ||
                state.NextSequence < 1 || state.RewardEpoch < 1 || revision == null || revision < 0 ||
                state.GoldRemainderMicros < 0 || state.GoldRemainderMicros >= 1000000 ||
                state.ExpRemainderMicros < 0 || state.ExpRemainderMicros >= 1000000 ||
                state.RewardEpochs == null || state.RewardEpochs.Count < 1 || state.RewardEpochs.Count > MaxEpochs ||
                state.RewardEpochs.Any(e => e == null || e.Id < 1 || e.Id > state.RewardEpoch ||
                    e.GoldLevel < 0 || e.GoldLevel > BalanceMath.RubyCap || e.ExpLevel < 0 || e.ExpLevel > BalanceMath.RubyCap) ||
                state.RewardEpochs.Select(e => e.Id).Distinct().Count() != state.RewardEpochs.Count ||
                !state.RewardEpochs.Any(e => e.Id == state.RewardEpoch))
                Invalid("Invalid economy batch state.");
            if (state.SupportsQuestAwakeningBatch && (!state.SupportsQuestAwakeningRecovery || state.QuestGoldPreview == null) ||
                state.QuestGoldPreview != null && (state.QuestGoldPreview.BaseAmount < 0 || state.QuestGoldPreview.BaseAmount > long.MaxValue))
                Invalid("Invalid quest batch capabilities.");
            if (state.PendingReceipt == null) return;
            ValidateReceipt(state.PendingReceipt);
            if (state.PendingReceipt.Generation != state.Generation || state.PendingReceipt.Sequence + 1 != state.NextSequence ||
                state.PendingReceipt.CommittedRevision > revision) Invalid("Economy receipt does not match current state.");
        }

        internal static void ValidateReceipt(EconomyBatchReceiptDTO receipt)
        {
            if (receipt == null || !ValidIdentity(receipt.RequestId, receipt.Generation, receipt.Sequence) ||
                receipt.PayloadHash == null || receipt.PayloadHash.Length != 64 || receipt.PayloadHash.Any(c => !Uri.IsHexDigit(c)) ||
                receipt.CommittedRevision < 1 || receipt.Results == null || receipt.Results.Count < 1 || receipt.Results.Count > MaxCommands)
                Invalid("Invalid economy receipt.");
            bool stopped = false, mageRejected = false;
            var rejectedGrowth = new HashSet<EconomyCommandKind>();
            var rejectedAwakening = new HashSet<int>();
            for (int i = 0; i < receipt.Results.Count; i++)
            {
                var result = receipt.Results[i];
                if (result == null || result.Index != i || !Enum.IsDefined(typeof(EconomyCommandKind), result.Kind) ||
                    !Enum.IsDefined(typeof(EconomyCommandStatus), result.Status) || result.AppliedCount < 0 ||
                    result.SpentGold < 0 || result.SpentRuby < 0 || result.SpentArcaneKnowledge < 0 || result.SpentEquipmentStone < 0 || result.GrantedGold < 0 || result.GrantedExp < 0)
                    Invalid("Invalid economy command result.");
                bool awaken = result.Kind == EconomyCommandKind.MageAwakening;
                bool quest = result.Kind == EconomyCommandKind.QuestClaim;
                if (awaken ? !result.SkillId.HasValue || !MageSkillRules.IsAvailable(result.SkillId.Value) :
                    result.SkillId.HasValue || result.AwakeningLevel.HasValue || result.SpentFragments.HasValue)
                    Invalid("Invalid awakening result target.");
                if (!quest && result.QuestClaimResults != null) Invalid("Unexpected quest target results.");
                if (quest) ValidateClaimResults(result);
                bool skipped = stopped || rejectedGrowth.Contains(result.Kind) ||
                    awaken && rejectedAwakening.Contains(result.SkillId.Value);
                if (result.Status == EconomyCommandStatus.Applied)
                {
                    if (skipped || result.ErrorCode != null) Invalid("Invalid economy success order.");
                    bool hunt = result.Kind == EconomyCommandKind.Hunt;
                    bool gold = result.Kind is EconomyCommandKind.GoldAttack or EconomyCommandKind.GoldHealth;
                    bool ruby = result.Kind is EconomyCommandKind.RubyGold or EconomyCommandKind.RubyExperience;
                    bool mage = result.Kind == EconomyCommandKind.MageEnhance;
                    bool gear = result.Kind == EconomyCommandKind.EquipmentEnhance;
                    if (gear != (result.SpentEquipmentStone > 0) || gold != (result.SpentGold > 0) || ruby != (result.SpentRuby > 0) || mage != (result.SpentArcaneKnowledge > 0) ||
                        !hunt && (result.AppliedCount < 1 || result.GrantedGold != 0 || result.GrantedExp != 0) ||
                        (mage || ruby || gear || awaken) && result.AppliedCount != 1 ||
                        awaken && (result.AwakeningLevel is not (>= 1 and <= MageSkillRules.BloomAwakening) || result.SpentFragments != result.AwakeningLevel))
                        Invalid("Invalid economy result amounts.");
                }
                else
                {
                    if (result.AppliedCount != 0 || result.SpentGold != 0 || result.SpentRuby != 0 || result.SpentArcaneKnowledge != 0 || result.SpentEquipmentStone != 0 || result.GrantedGold != 0 || result.GrantedExp != 0)
                        Invalid("Unapplied economy command has amounts.");
                    if (result.AwakeningLevel.HasValue || result.SpentFragments.HasValue)
                        Invalid("Unapplied awakening has amounts.");
                    if (result.Status == EconomyCommandStatus.Rejected)
                    {
                        if (skipped || result.ErrorCode == null || !Enum.IsDefined(typeof(eErrorCode), result.ErrorCode.Value))
                            Invalid("Invalid economy rejection.");
                        if (!IsIsolatedGrowthRejection(result, receipt.IndependentGrowthFailures)) stopped = true;
                        else if (result.Kind == EconomyCommandKind.MageEnhance) mageRejected = true;
                        else if (awaken) rejectedAwakening.Add(result.SkillId.Value);
                        else if (!quest) rejectedGrowth.Add(result.Kind);
                    }
                    else if (result.ErrorCode != null || !skipped && (!mageRejected || result.Kind != EconomyCommandKind.MageEnhance))
                        Invalid("Invalid economy unprocessed suffix.");
                }
            }
            if (receipt.IndependentGrowthFailures && rejectedGrowth.Count == 0) Invalid("Missing independent growth rejection.");
        }

        private static bool IsQuestTargetError(eErrorCode? error) =>
            error is eErrorCode.QuestNotClaimable or eErrorCode.RewardAlreadyClaimed or eErrorCode.NumericLimit;

        private static void ValidateClaimResults(EconomyCommandResultDTO result)
        {
            var targets = result.QuestClaimResults;
            if (targets == null)
            {
                if (result.Status == EconomyCommandStatus.Applied) Invalid("Missing applied quest targets.");
                return;
            }
            if (targets.Count is < 1 or > 128 || result.Status == EconomyCommandStatus.Unprocessed)
                Invalid("Invalid quest result count.");
            int applied = 0;
            eErrorCode? rejected = null;
            var seen = new HashSet<(long, string)>();
            foreach (var target in targets)
            {
                if (target == null || target.QuestId <= 0 || !ValidPeriod(target.PeriodKey) || !seen.Add((target.QuestId, target.PeriodKey)))
                    Invalid("Invalid quest target identity.");
                if (target.Status == EconomyCommandStatus.Applied)
                {
                    if (rejected.HasValue || target.ErrorCode.HasValue || target.GrantedRewards == null ||
                        target.GrantedRewards.Any(g => g == null || g.Amount < 0 || g.Amount > long.MaxValue ||
                            g.Amount != decimal.Truncate(g.Amount) || g.IsDynamicGold || g.Kind != RewardKind.Currency ||
                            !g.CurrencyType.HasValue || !Enum.IsDefined(typeof(Protocol.Message.Data.eCurrency), g.CurrencyType.Value) ||
                            g.DefinitionId.HasValue))
                        Invalid("Invalid applied quest target.");
                    applied++;
                }
                else
                {
                    if (target.GrantedRewards != null) Invalid("Unapplied quest target has rewards.");
                    if (target.Status == EconomyCommandStatus.Rejected && !rejected.HasValue && IsQuestTargetError(target.ErrorCode))
                        rejected = target.ErrorCode;
                    else if (target.Status != EconomyCommandStatus.Unprocessed || !rejected.HasValue || target.ErrorCode.HasValue)
                        Invalid("Invalid quest target result order.");
                }
            }
            if (result.AppliedCount != applied || result.Status != (applied > 0 ? EconomyCommandStatus.Applied : EconomyCommandStatus.Rejected) ||
                applied == 0 && result.ErrorCode != rejected)
                Invalid("Quest command result does not match target results.");
        }

        internal static void ValidateResponse(OnEconomyBatchResponseDTO response)
        {
            if (!MainWaveProgression.ValidId(response.RequestId) || !Enum.IsDefined(typeof(EconomyBatchStatus), response.Status) || response.CurrentState == null)
                Invalid("Invalid economy batch response.");
            var state = response.CurrentState;
            if (state.Equipment != null) NetworkManager.ValidateEquipment(state.Equipment);
            if (response.Receipt?.Results.Any(x => x.Kind == EconomyCommandKind.EquipmentEnhance) == true && state.Equipment?.SchemaVersion != EquipmentManager.InstanceSchemaVersion)
                Invalid("Missing instance inventory for equipment receipt.");
            ValidateMetadata(state.EconomyBatchState, response.EconomyRevision);
            NetworkManager.ValidateRubyGrowth(state.RubyGrowthState);
            var epoch = state.EconomyBatchState.RewardEpochs.Single(e => e.Id == state.EconomyBatchState.RewardEpoch);
            if (epoch.GoldLevel != state.RubyGrowthState.GoldLevel || epoch.ExpLevel != state.RubyGrowthState.ExpLevel ||
                state.EnchantATKCount < 0 || state.EnchantATKCount > BalanceMath.GoldCap || state.EnchantHPCount < 0 || state.EnchantHPCount > BalanceMath.GoldCap ||
                state.Level < 1 || state.Level > BalanceMath.AccountCap || state.Exp < 0 || state.KillScore < 0 ||
                (state.Level == BalanceMath.AccountCap ? state.Exp != 0 : state.Exp >= BalanceMath.NextExp(state.Level)) ||
                state.StageState == null || state.Currency == null ||
                state.Currency.Gold < 0 || state.Currency.Ruby < 0 || state.Currency.AncientCoin < 0 || state.Currency.ArcaneKnowledge < 0 ||
                state.Currency.ClassFragment < 0 || state.Currency.EquipmentStone < 0 || state.Currency.EquipmentTicket < 0 || state.Currency.MageTicket < 0)
                Invalid("Invalid economy current state.");
            if (state.MainBattle != null)
            {
                NetworkManager.ValidateMainBattle(state.MainBattle);
                if (state.MainBattle.Stage != state.StageState.CurrentStage) Invalid("Economy main stage mismatch.");
            }
            ValidateDungeon(state.DungeonBattle);
            if (response.Receipt != null)
            {
                ValidateReceipt(response.Receipt);
                if (response.Receipt.RequestId != response.RequestId || response.Receipt.CommittedRevision > response.EconomyRevision)
                    Invalid("Economy receipt identity mismatch.");
            }
            if (response.Status == EconomyBatchStatus.Completed ? response.Receipt == null : response.Receipt != null)
                Invalid("Economy status does not match receipt.");
            if (state.MageTowerState != null || response.Receipt?.Results.Any(x => x.Kind is EconomyCommandKind.MageEnhance or EconomyCommandKind.MageAwakening) == true ||
                MageMode(state.EconomyBatchState) != MageEnhanceBatchMode.Unknown && response.Status != EconomyBatchStatus.Completed)
                NetworkManager.ValidateMageTower(state.MageTowerState);
        }

        internal static void ValidateDungeon(EconomyDungeonBattleDTO dungeon)
        {
            if (dungeon == null) return;
            ValidateHunt((Protocol.Message.Data.eStage)dungeon.DungeonId, null, dungeon.BattleId, dungeon.AcceptedKills);
            if (dungeon.RunGold < 0) Invalid("Invalid economy dungeon reward.");
        }

        internal static void MatchRequest(OnEconomyBatchResponseDTO response, RequestDTO request)
        {
            string id = request is OnEconomyBatchRequestDTO batch ? batch.RequestId : ((OnGetEconomyBatchResultRequestDTO)request).RequestId;
            long generation = request is OnEconomyBatchRequestDTO b ? b.Generation : ((OnGetEconomyBatchResultRequestDTO)request).Generation;
            long sequence = request is OnEconomyBatchRequestDTO c ? c.Sequence : ((OnGetEconomyBatchResultRequestDTO)request).Sequence;
            if (response.RequestId != id || response.Receipt != null && (response.Receipt.Generation != generation || response.Receipt.Sequence != sequence))
                Invalid("Economy response does not match request.");
            if (!(request is OnEconomyBatchRequestDTO submitted)) return;
            if (response.CurrentState.MageTowerState == null && submitted.Commands.Any(x => x.Kind is EconomyCommandKind.MageEnhance or EconomyCommandKind.MageAwakening))
                Invalid("Missing mage state for submitted enhancement.");
            if (response.Receipt == null) return;
            if (response.Receipt.PayloadHash != EconomyInputJournal.Hash(submitted)) Invalid("Economy receipt payload hash mismatch.");
            if (response.Receipt.IndependentGrowthFailures && !submitted.IndependentGrowthFailures)
                Invalid("Independent growth failure requires request opt-in.");
            if (submitted.Commands.Count != response.Receipt.Results.Count) Invalid("Economy result count mismatch.");
            var rejectedTargets = new HashSet<(EconomyCommandKind, int?, long?)>();
            Dictionary<int, int> appliedMageLevels = null;
            Dictionary<int, int> appliedAwakeningLevels = null;
            Dictionary<long, int> appliedEquipmentLevels = null;
            bool stopped = false;
            for (int i = 0; i < submitted.Commands.Count; i++)
            {
                var command = submitted.Commands[i]; var result = response.Receipt.Results[i];
                if (command.Kind != result.Kind || command.Kind is not (EconomyCommandKind.Hunt or EconomyCommandKind.QuestClaim) && result.Status == EconomyCommandStatus.Applied &&
                    (result.AppliedCount > command.CountLimit || result.SpentGold > command.SpendLimit || result.SpentRuby > command.SpendLimit ||
                     result.SpentArcaneKnowledge > command.SpendLimit || result.SpentEquipmentStone > command.SpendLimit))
                    Invalid("Economy result exceeds requested limits.");
                if (command.Kind == EconomyCommandKind.QuestClaim && result.QuestClaimResults != null)
                {
                    var targets = command.QuestClaim.Targets;
                    if (targets.Count != result.QuestClaimResults.Count || result.QuestClaimResults.Where((r, j) =>
                        r.QuestId != targets[j].QuestId || r.PeriodKey != targets[j].PeriodKey).Any())
                        Invalid("Quest results do not match submitted targets.");
                }
                if (command.Kind == EconomyCommandKind.MageAwakening &&
                    (command.SkillId != result.SkillId || result.Status == EconomyCommandStatus.Applied &&
                        (result.AwakeningLevel != command.ExpectedLevel + 1 || result.SpentFragments > command.SpendLimit)))
                    Invalid("Awakening result does not match submitted skill.");
                var target = GrowthTarget(command);
                bool skipped = stopped || rejectedTargets.Contains(target);
                if (skipped != (result.Status == EconomyCommandStatus.Unprocessed)) Invalid("Economy command dependency mismatch.");
                if (IsIsolatedGrowthRejection(result, response.Receipt.IndependentGrowthFailures))
                {
                    if (command.Kind != EconomyCommandKind.QuestClaim) rejectedTargets.Add(target);
                }
                else if (result.Status == EconomyCommandStatus.Rejected) stopped = true;
                else if (command.Kind == EconomyCommandKind.MageEnhance && result.Status == EconomyCommandStatus.Applied)
                {
                    appliedMageLevels ??= new Dictionary<int, int>();
                    appliedMageLevels[command.SkillId.Value] = command.ExpectedLevel + 1;
                }
                else if (command.Kind == EconomyCommandKind.EquipmentEnhance && result.Status == EconomyCommandStatus.Applied)
                {
                    appliedEquipmentLevels ??= new Dictionary<long, int>();
                    appliedEquipmentLevels[command.EquipmentInstanceId.Value] = command.ExpectedLevel + 1;
                }
                else if (command.Kind == EconomyCommandKind.MageAwakening && result.Status == EconomyCommandStatus.Applied)
                {
                    appliedAwakeningLevels ??= new Dictionary<int, int>();
                    appliedAwakeningLevels[command.SkillId.Value] = result.AwakeningLevel.Value;
                }
            }
            if (appliedEquipmentLevels != null && response.EconomyRevision == response.Receipt.CommittedRevision)
            {
                foreach (var pair in appliedEquipmentLevels)
                    if (!response.CurrentState.Equipment.Items.Any(i => i.ItemCode.InstanceId == pair.Key && i.EnhancementLevel == pair.Value))
                        Invalid("Equipment state does not match the confirmed enhancement.");
            }
            if (appliedAwakeningLevels != null)
                foreach (var applied in appliedAwakeningLevels)
                {
                    var skill = response.CurrentState.MageTowerState.Skills.Find(x => x.SkillId == applied.Key);
                    if (skill == null || skill.AwakeningLevel < applied.Value ||
                        response.EconomyRevision == response.Receipt.CommittedRevision &&
                        (skill.AwakeningLevel != applied.Value || applied.Value == MageSkillRules.BloomAwakening && !skill.BloomEnabled))
                        Invalid("Economy mage snapshot does not match applied awakening.");
                }
            if (appliedMageLevels != null)
                foreach (var applied in appliedMageLevels)
                {
                    var skill = response.CurrentState.MageTowerState.Skills.Find(x => x.SkillId == applied.Key);
                    if (skill == null || response.EconomyRevision == response.Receipt.CommittedRevision && skill.EnhancementLevel != applied.Value)
                        Invalid("Economy mage snapshot does not match the applied enhancements.");
                }
        }

        private static void Invalid(string message) => throw new JsonSerializationException(message);
    }
}
