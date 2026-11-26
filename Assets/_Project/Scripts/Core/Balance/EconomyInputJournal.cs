using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protocol.Message.Dto;

namespace KingdomIdle.Balance
{
    [Serializable]
    internal sealed class EconomyInput
    {
        public long Sequence;
        public string GuideReceipt;
        public bool Held;
        public EconomyCommandDTO Command;
    }

    [Serializable]
    internal sealed class EconomyJournalData
    {
        public int Schema = 1;
        public string Account;
        public long Generation, NextInputSequence = 1;
        public List<EconomyInput> Inputs = new();
        public OnEconomyBatchRequestDTO InFlight;
        public long StorageRetryNotBeforeUtcMs;
        public List<EconomyInput> InFlightInputs = new();
        public OnEconomyBatchRequestDTO AbandonedRequest;
        public long AbandonedAtRevision = -1;
    }

    // 작은 입력 파일만 작업자에게 넘긴다. 확정 계정 snapshot의 writer는 기존 LocalProgression이다.
    internal sealed class EconomyInputJournal
    {
        private readonly string _path;
        private Task _write;
        private EconomyJournalData _writing;
        private long _revision, _writingRevision, _savedRevision;
        internal EconomyJournalData Data { get; private set; }
        internal EconomyJournalData Committed { get; private set; }
        internal bool IsDirty => _write != null || _revision != _savedRevision;
        internal string Error { get; private set; }
        internal long Revision => _revision;
        internal long SavedRevision => _savedRevision;

        internal EconomyInputJournal(string path, string account)
        {
            _path = path;
            Data = File.Exists(path) ? JsonConvert.DeserializeObject<EconomyJournalData>(File.ReadAllText(path)) : new() { Account = account };
            Validate(Data, account);
            Committed = Clone(Data);
        }

        internal void Changed() { _revision = checked(_revision + 1); Error = null; }
        internal bool Pump(bool wait = false)
        {
            bool saved = false;
            if (_write != null && (wait || _write.IsCompleted))
            {
                try
                {
                    _write.GetAwaiter().GetResult();
                    Committed = _writing;
                    _savedRevision = _writingRevision;
                    saved = true;
                }
                catch (Exception error) { Error = error.Message; }
                finally { _write = null; _writing = null; }
            }
            if (Error == null && _write == null && _revision != _savedRevision)
            {
                Validate(Data, Data.Account);
                _writing = Clone(Data);
                _writingRevision = _revision;
                string json = JsonConvert.SerializeObject(_writing);
                string path = _path;
                _write = Task.Run(() => LocalProgression.WriteAtomicJson(path, json));
                if (wait) return Pump(true) || saved;
            }
            return saved;
        }

        internal void RestoreCommitted()
        {
            if (_write != null) throw new InvalidOperationException("A journal write is still pending.");
            Data = Clone(Committed); _revision = _savedRevision; Error = null;
        }

        internal static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        internal static void Validate(EconomyJournalData data, string account)
        {
            if (data == null || data.Schema != 1 || data.Account != account || data.Generation < 0 || data.NextInputSequence < 1 ||
                data.Inputs == null || data.InFlightInputs == null || data.Inputs.Count > 512 || data.InFlightInputs.Count > 16 ||
                data.StorageRetryNotBeforeUtcMs < 0 || data.InFlight == null && data.StorageRetryNotBeforeUtcMs != 0 ||
                data.Inputs.Concat(data.InFlightInputs).Any(x => x == null || x.Sequence < 1 || x.Sequence >= data.NextInputSequence || !ValidCommand(x.Command)) ||
                data.Inputs.Concat(data.InFlightInputs).Select(x => x.Sequence).Distinct().Count() != data.Inputs.Count + data.InFlightInputs.Count ||
                (data.InFlight == null ? data.InFlightInputs.Count != 0 : !MainWaveProgression.ValidId(data.InFlight.RequestId) ||
                    data.InFlight.Generation < 1 || data.InFlight.Sequence < 1 || data.InFlight.Commands == null ||
                    data.InFlight.Commands.Count != data.InFlightInputs.Count || data.InFlight.Commands.Count == 0 ||
                    !data.InFlight.Commands.All(ValidCommand)))
                throw new InvalidDataException("경제 입력 기록이 손상되어 자동 실행하지 않습니다.");
            if (data.InFlight != null && data.InFlight.Commands.Where((x, i) =>
                JsonConvert.SerializeObject(x) != JsonConvert.SerializeObject(data.InFlightInputs[i].Command)).Any())
                throw new InvalidDataException("저장된 경제 요청의 입력이 일치하지 않습니다.");
            if (data.InFlight != null) EconomyBatchProtocol.ValidateRequest(data.InFlight);
        }

        internal static bool ValidCommand(EconomyCommandDTO c)
        {
            if (c == null || !Enum.IsDefined(typeof(EconomyCommandKind), c.Kind)) return false;
            if (c.Kind == EconomyCommandKind.QuestClaim) return EconomyBatchProtocol.ValidQuestCommand(c);
            if (c.QuestClaim != null) return false;
            if (c.Kind == EconomyCommandKind.EquipmentEnhance) return EconomyBatchProtocol.ValidEquipmentCommand(c);
            if (c.EquipmentInstanceId.HasValue) return false;
            if (c.Kind == EconomyCommandKind.MageEnhance) return EconomyBatchProtocol.ValidMageCommand(c);
            if (c.Kind == EconomyCommandKind.MageAwakening) return EconomyBatchProtocol.ValidAwakeningCommand(c);
            if (c.SkillId.HasValue) return false;
            if (c.Kind == EconomyCommandKind.Hunt) return c.Mode == 0 && c.ExpectedLevel == 0 && c.CountLimit == 0 && c.SpendLimit == 0 &&
                c.RewardEpoch > 0 && EconomyCopies.ValidRun((long)c.Stage, c.MainRunId, c.BattleId) && EconomyCopies.ValidKills(c.TotalKills);
            bool gold = c.Kind == EconomyCommandKind.GoldAttack || c.Kind == EconomyCommandKind.GoldHealth;
            return (gold ? c.Mode == 1 || c.Mode == 10 || c.Mode == -1 : c.Mode == 1) && c.ExpectedLevel >= 0 &&
                c.ExpectedLevel < (gold ? BalanceMath.GoldCap : BalanceMath.RubyCap) && c.CountLimit >= 1 &&
                c.CountLimit <= (gold ? BalanceMath.GoldCap : 1) && c.SpendLimit > 0 && c.TotalKills == null &&
                c.MainRunId == null && c.BattleId == null && c.RewardEpoch == 0 && (long)c.Stage == 0;
        }

        internal static string Hash(OnEconomyBatchRequestDTO r)
        {
            object Kills(List<HuntResult> kills) => kills?.OrderBy(x => (ulong)x.MonsterType)
                .Select(x => new { MonsterType = (ulong)x.MonsterType, x.Count }).ToArray();
            object Command(EconomyCommandDTO x)
            {
                var fields = new { Kind = (int)x.Kind, x.Mode, x.ExpectedLevel, x.CountLimit, x.SpendLimit, Stage = (long)x.Stage,
                    x.MainRunId, x.BattleId, x.RewardEpoch, TotalKills = Kills(x.TotalKills) };
                if (x.QuestClaim != null)
                {
                    var quest = JObject.FromObject(fields);
                    var claim = x.QuestClaim;
                    var body = new JObject();
                    if (claim.Category.HasValue) body.Add(nameof(claim.Category), (int)claim.Category.Value);
                    if (claim.BoardPeriod != null) body.Add(nameof(claim.BoardPeriod), claim.BoardPeriod);
                    body.Add(nameof(claim.Targets), JArray.FromObject(claim.Targets.Select(t => new { t.QuestId, t.PeriodKey })));
                    quest.Add(nameof(x.QuestClaim), body);
                    return quest;
                }
                if (x.EquipmentInstanceId.HasValue)
                {
                    var equipment = JObject.FromObject(fields);
                    equipment.Add(nameof(x.EquipmentInstanceId), x.EquipmentInstanceId.Value);
                    return equipment;
                }
                if (!x.SkillId.HasValue) return fields;
                var mage = JObject.FromObject(fields);
                mage.Add(nameof(x.SkillId), x.SkillId.Value);
                return mage;
            }
            var body = new
            {
                r.RequestId, r.Generation, r.Sequence, r.ExpectedEconomyRevision, r.AppliedRewardEpoch, r.AcknowledgedRequestId,
                RewardEpochCutoffs = r.RewardEpochCutoffs.OrderBy(x => x.Epoch).Select(x => new
                { x.Epoch, Stage = (long)x.Stage, x.MainRunId, x.BattleId, TotalKills = Kills(x.TotalKills) }).ToArray(),
                Commands = r.Commands.Select(Command).ToArray()
            };
            string json;
            if (r.IndependentGrowthFailures)
            {
                var optedIn = JObject.FromObject(body);
                optedIn.Add(nameof(r.IndependentGrowthFailures), true);
                json = optedIn.ToString(Formatting.None);
            }
            else json = JsonConvert.SerializeObject(body, Formatting.None);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "");
        }
    }
}
