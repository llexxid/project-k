using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    /// <summary>
    /// Explicit local authority for this client beta. Authentication never authorizes a legacy
    /// unversioned economy response to overwrite it. Server migration must import a reviewed
    /// snapshot with account, authority, balance version, revision and transaction id together.
    /// Economic mutators publish only after durable replacement. Non-economic skill counters
    /// are autosaved between casts and flushed before rewards, account changes and suspension.
    /// </summary>
    public static partial class LocalProgression
    {
        public static bool IsLocalAuthority => true;
        public static event Action Changed;
        /// <summary>계정의 복원·이관·기간 정리가 내구 저장까지 끝났을 때 발생한다.</summary>
        public static event Action AccountChanged;
        public static string AccountKey { get; private set; }
        /// <summary>이전 계정 화면의 수령 요청을 거절하는 실행 세대다.</summary>
        public static long AccountGeneration { get; private set; }
        public static bool IsReady => _state != null;
        public static ProgressionState State { get { Ensure(); return _state; } }
        private static ProgressionState _state;
        private static string _path;
        private static bool _busy;
        // 계정 전환 중 이전 계정 flush가 Ensure를 다시 호출하는 재진입을 막는다.
        private static bool _opening;
        public static string LastError { get; private set; }
        public static long UtcNow =>
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            TestUtcNow ??
#endif
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static string KstDay => QuestPeriod.At(UtcNow).Day;

        #if UNITY_EDITOR || LOBBY_DEVICE_QA
        private static string _testAccount;
        /// <summary>기간 경계 인수 검사에서만 사용하는 고정 UTC 시각이다.</summary>
        public static long? TestUtcNow;
        public static void OpenTestAccount(string id)
        {
            string previous = _testAccount;
            _testAccount = "balance-qa-" + id;
            try { Open(_testAccount); }
            catch { _testAccount = previous; throw; }
        }
        public static string SnapshotPath => _path;
        public static bool TestFailedWrite(Func<ProgressionState,bool> action)
        { string previous = _path; _path += "/cannot-write/snapshot.json"; try { return Execute("expected-storage-failure",action); } finally { _path = previous; } }
        /// <summary>수령 API 자체의 저장 실패와 재시도를 검증한다.</summary>
        public static bool TestFailedCommit(Func<bool> action)
        { string previous = _path; _path += "/cannot-write/snapshot.json"; try { return action(); } finally { _path = previous; } }
        /// <summary>인수 검사의 계정/시각 변경을 실행 전 컨텍스트로 되돌리는 범위다.</summary>
        public static IDisposable BeginTestSession() => new TestSession();
        private sealed class TestSession : IDisposable
        {
            private readonly string _previousTest = _testAccount, _previousAccount = AccountKey;
            private readonly long? _previousTime = TestUtcNow;
            public TestSession() { BattleEconomy.Suspend(); }
            public void Dispose()
            {
                TestUtcNow = _previousTime;
                _testAccount = _previousTest;
                if (_previousAccount != null) Open(_previousAccount);
                else
                {
                    // 테스트가 시작한 비동기 저장도 끝낸 뒤 정적 계정 참조를 비운다.
                    if (!FlushSkillCounters()) throw new IOException("Test skill counters could not be saved.");
                    _state = null; _path = null; AccountKey = null; LastError = null;
                    AccountGeneration = checked(AccountGeneration + 1);
                    BattleEconomy.OnAccountOpened(AccountGeneration);
                    Notify(AccountChanged); Notify(Changed);
                }
                BattleEconomy.Resume();
            }
        }
#endif
        public static void Ensure()
        {
            if (_opening || _busy) return;
            string account = NetworkManager.Instance?.GetSessionID();
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            if (_testAccount != null) account = _testAccount;
#endif
            if (string.IsNullOrEmpty(account)) account = "local-guest";
            if (_state == null || account != AccountKey) Open(account);
        }
        public static void Open(string account)
        {
            if (_busy) throw new InvalidOperationException("Cannot change account during a transaction.");
            if (_opening) throw new InvalidOperationException("Account open is already in progress.");
            _opening = true;
            try
            {
                // 전투 시간 거래는 미저장 스킬 횟수도 함께 저장한다. 시간이 없으면 별도로 flush한다.
                // 두 저장 모두 이전 _path를 사용하며 실패하면 계정과 세대를 바꾸지 않는다.
                if (_state != null && !BattleEconomy.TryFlushQuestTime())
                    throw new IOException("Previous account battle time could not be saved.");
                if (_state != null && !FlushSkillCounters())
                    throw new IOException("Pending skill counters could not be saved before changing account.");
                using var sha = SHA256.Create();
                string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account))).Replace("-", "").ToLowerInvariant();
                string directory = Path.Combine(Application.persistentDataPath, "progression-local-v1");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, key + ".json");
                ProgressionState state;
                if (File.Exists(path))
                {
                    // 손상된 원본을 신규 계정이나 오래된 백업으로 조용히 대체하지 않는다.
                    state = JsonConvert.DeserializeObject<ProgressionState>(File.ReadAllText(path));
                }
                else state = new ProgressionState { CycleStartedUtc = UtcNow };
                // 기존 원본은 교체 시 .bak에 남고, 실패한 이관은 메모리에도 공개하지 않는다.
                string before = JsonConvert.SerializeObject(state);
                QuestEconomy.Migrate(state);
                // 퀘스트와 마법 이관 결과는 하나의 저장으로 공개한다. 제거한 스킬 ID는 복원하지 않는다.
                for (int i = 0; i < state.MageSlots.Length; i++)
                    if (state.MageSlots[i] == 6) state.MageSlots[i] = -1;
                KingdomIdle.MageTower.MageCatalogMigration.Apply(state);
                Validate(state);
                long now = UtcNow;
                QuestEconomy.Before(state, now);
                QuestEconomy.After(state, now);
                Validate(state);
                if (!File.Exists(path) || before != JsonConvert.SerializeObject(state))
                {
                    state.Revision = checked(state.Revision + 1);
                    WriteSnapshot(path, state);
                }
                _path = path; _state = state; AccountKey = account; LastError = null;
                AccountGeneration = checked(AccountGeneration + 1);
                BattleEconomy.OnAccountOpened(AccountGeneration);
            }
            finally { _opening = false; }
            Notify(AccountChanged);
            Notify(Changed);
        }
        /// <summary>UI 조회 전용. Ensure/Open 또는 파일 접근을 절대로 수행하지 않는다.</summary>
        public static bool TryGetCommittedState(out ProgressionState state)
        {
            state = _state;
            return state != null;
        }

        /// <summary>패널 표시와 독립적인 기간 동기화 명령이다.</summary>
        public static bool SynchronizeQuests()
        {
            Ensure();
            if (!QuestPeriod.NeedsSynchronization(_state, UtcNow)) return true;
            return Execute("quest-period", state => true);
        }

        /// <summary>원본과 분리된 복제본만 변경하고 파일 교체 이후 상태와 이벤트를 공개한다.</summary>
        public static bool Execute(string operation, Func<ProgressionState, bool> mutate, string claimId = null)
        {
            try { Ensure(); }
            catch (Exception error) { LastError = operation + ": " + error.Message; return false; }
            if (_busy || mutate == null || string.IsNullOrWhiteSpace(operation)) return false;
            // 먼저 시작한 자동 저장이 같은 파일을 뒤늦게 덮어쓰지 않도록 완료를 기다린다.
            // 이후 거래가 거절되어도 이미 성공한 저장의 알림은 finally에서 발행한다.
            bool published = CompleteCounterSave(true);
            _busy = true;
            try
            {
                if (claimId != null && _state.Claims.Contains(claimId)) return false;
                var draft = _state.DeepClone();
                // 모든 기간 계산은 같은 UTC를 사용한다. 샘플의 제거는 저장 성공 뒤에만 한다.
                long now = Math.Max(UtcNow, draft.QuestLastObservedUtc);
                long timeBatch = BattleEconomy.PrepareQuestTime(draft, now);
                QuestEconomy.Before(draft, now);
                if (!mutate(draft)) return false;
                QuestEconomy.After(draft, now);
                if (claimId != null) draft.Claims.Add(claimId);
                draft.Revision = checked(_state.Revision + 1);
                Validate(draft);
                WriteSnapshot(_path, draft);
                _state = draft; LastError = null;
                BattleEconomy.AcknowledgeQuestTime(timeBatch);
                _countersDirty = false;
                published = true;
                return true;
            }
            catch (Exception exception)
            {
                LastError = operation + ": " + exception.Message;
                Debug.LogWarning("[Progression] Transaction rejected: " + LastError);
                return false;
            }
            finally
            {
                _busy = false;
                if (published) Notify(Changed);
            }
        }

        /// <summary>전체 경제 상태를 같은 원자적 파일 교체로 저장한다.</summary>
        private static void WriteSnapshot(string path, ProgressionState state)
        {
            string json = JsonConvert.SerializeObject(state);
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

        /// <summary>한 관찰자의 실패가 저장된 결과나 다른 관찰자를 방해하지 않게 한다.</summary>
        private static void Notify(Action observers)
        {
            // Observers cannot roll back a committed transaction or prevent other observers.
            if (observers != null) foreach (Action observer in observers.GetInvocationList())
                try { observer(); } catch (Exception ex) { Debug.LogException(ex); }
        }
        public static long Balance(eCurrency currency) => State.Wallet.TryGetValue(currency, out long amount) ? amount : 0;
        public static bool Spend(ProgressionState state, eCurrency currency, long amount)
        {
            if (amount < 0 || !Enum.IsDefined(typeof(eCurrency), currency)) return false;
            long current = state.Wallet.TryGetValue(currency, out long value) ? value : 0;
            if (current < amount) return false;
            state.Wallet[currency] = current - amount;
            return true;
        }
        public static void Credit(ProgressionState state, eCurrency currency, long amount)
        {
            if (amount < 0 || !Enum.IsDefined(typeof(eCurrency), currency)) throw new ArgumentOutOfRangeException();
            state.Wallet[currency] = checked((state.Wallet.TryGetValue(currency, out long v) ? v : 0) + amount);
        }
        public static void Validate(ProgressionState state)
        {
            if (state == null || state.Schema != 1 || state.BalanceVersion != BalanceMath.Version || state.Authority != "local-client")
                throw new InvalidDataException("Unsupported progression snapshot; migration required.");
            if (state.QuestSchemaVersion != QuestEconomy.SchemaVersion || state.CompletedQuests == null || state.QuestLastObservedUtc < 0 ||
                state.Counters == null || state.Claims == null || state.PendingQuests == null || state.Counters.Any(x => x.Value < 0))
                throw new InvalidDataException("Invalid quest snapshot.");
            foreach (var pair in state.PendingQuests)
            {
                QuestPending pending = pair.Value;
                if (pending == null || !pair.Key.StartsWith($"quest:{pending.Id}:", StringComparison.Ordinal) || pending.RequiredCount <= 0 ||
                    pending.ExpiresUtc <= 0 || string.IsNullOrEmpty(pending.DefinitionVersion) || pending.Rewards == null || pending.Rewards.Count == 0 ||
                    pending.Gold < 0 || (pending.Category != eQuestCategory.Daily && pending.Category != eQuestCategory.Weekly) ||
                    pending.Rewards.Any(x => x.Amount < 0 || !Enum.IsDefined(typeof(eCurrency), x.Currency) || (x.IsDynamicGold && x.Currency != eCurrency.Gold)))
                    throw new InvalidDataException("Unresolved pending quest: " + pair.Key);
            }
            foreach (var entry in state.Wallet) if (entry.Value < 0 || !Enum.IsDefined(typeof(eCurrency),entry.Key)) throw new InvalidDataException("Negative wallet.");
            if (state.AccountLevel < 1 || state.AccountLevel > 200 || state.Experience < 0 ||
                state.AttackLevel < 0 || state.AttackLevel > 300 || state.HealthLevel < 0 || state.HealthLevel > 300 ||
                state.RubyGoldLevel < 0 || state.RubyGoldLevel > 50 || state.RubyExpLevel < 0 || state.RubyExpLevel > 50 ||
                state.ReincarnationLevel < 0 || state.ReincarnationLevel > 300 ||
                state.GoldRemainder < 0 || state.GoldRemainder >= 1000000 || state.ExpRemainder < 0 || state.ExpRemainder >= 1000000)
                throw new InvalidDataException("Invalid progression bounds.");
            if (state.Kills < 0 || state.Revision < 0 || state.RubyGoldSpent < 0 || state.RubyExpSpent < 0 ||
                state.EquipmentPity < 0 || state.EquipmentPity > 39 || state.GoldTickets < 0 || state.GoldTickets > 2 || state.RubyTickets < 0 || state.RubyTickets > 2 ||
                state.GoldDungeonClear < 0 || state.GoldDungeonClear > 5 || state.RubyDungeonClear < 0 || state.RubyDungeonClear > 5 ||
                state.OfflineKpm < 0 || state.OfflineKpm > 30 || state.ReincarnationCount < 0 || state.CycleBossStage < 0)
                throw new InvalidDataException("Invalid economy state.");
            var equipment = state.Equipment.Concat(state.PendingEquipment).ToArray();
            if (state.LegacyEquipment == null || state.LegacyEquipment.Any(x => x == null || x.Count <= 0 || x.Level < 0 || x.Level > 15) ||
                state.LegacyEquipment.GroupBy(x => (x.Code, x.Level)).Any(g => g.Count() > 1))
                throw new InvalidDataException("Invalid legacy inventory reserve.");
            if (state.AutoDismantleMask < 0 || state.AutoDismantleMask > 7 || state.EquipmentRarityFilter < -1 || state.EquipmentRarityFilter > 2 || state.EquipmentSort < 0 || state.EquipmentSort > 3 || state.Equipment.Count > EquipmentManager.Capacity || state.PendingEquipment.Count > EquipmentManager.PendingCapacity ||
                equipment.Any(x => string.IsNullOrEmpty(x.Id) || x.Level < 0 || x.Level > 15 || x.EnhancementStonesSpent < 0 || (x.Player.HasValue && (x.Player < 0 || x.Player > 2))) ||
                equipment.Select(x => x.Id).Distinct().Count() != equipment.Length ||
                state.Equipment.Where(x => x.Player.HasValue).GroupBy(x => x.Player).Any(g => g.Count() > 1)) throw new InvalidDataException("Invalid inventory snapshot.");
            if (state.MageSlots.Length != 5 || state.MageSlots.Any(x => x < -1 || x >= KingdomIdle.MageTower.MageSkillRules.IdCapacity || (x >= 0 && !state.MageSkills.ContainsKey(x))) ||
                state.MageSlots.Where(x => x >= 0).Distinct().Count() != state.MageSlots.Count(x => x >= 0) ||
                state.MageSkills.Any(x => x.Key < 0 || x.Key >= KingdomIdle.MageTower.MageSkillRules.IdCapacity || x.Value == null || x.Value.Enhance < 0 || x.Value.Enhance > 100 || x.Value.Awaken < 0 || x.Value.Awaken > 10 || x.Value.Fragments < 0 || x.Value.Spent < 0 || (x.Value.BloomEnabled && x.Value.Awaken < 10)))
                throw new InvalidDataException("Invalid mage snapshot.");
            if (state.AccountLevel == 200 ? state.Experience != 0 : state.Experience >= BalanceMath.NextExp(state.AccountLevel).Value)
                throw new InvalidDataException("Experience must be normalized.");
        }
        public static string GetModule(string key) => State.Modules.TryGetValue(key, out string value) ? value : "";
        public static bool SetModule(string key, string value) => Execute("save-" + key, s => { s.Modules[key] = value; return true; });
    }
}
