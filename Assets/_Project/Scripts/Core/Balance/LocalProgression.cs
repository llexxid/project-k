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
        public static string AccountKey { get; private set; }
        public static ProgressionState State { get { Ensure(); return _state; } }
        private static ProgressionState _state;
        private static string _path;
        private static bool _busy;
        public static string LastError { get; private set; }
        public static long UtcNow => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static string KstDay => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");

        #if UNITY_EDITOR || LOBBY_DEVICE_QA
        private static string _testAccount;
        public static void OpenTestAccount(string id) { _testAccount = "balance-qa-" + id; Open(_testAccount); }
        public static string SnapshotPath => _path;
        public static bool TestFailedWrite(Func<ProgressionState,bool> action)
        { string previous = _path; _path += "/cannot-write/snapshot.json"; try { return Execute("expected-storage-failure",action); } finally { _path = previous; } }
#endif
        public static void Ensure()
        {
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
            if (_state != null && !FlushSkillCounters()) throw new IOException("Pending skill counters could not be saved before changing account.");
            using var sha = SHA256.Create();
            string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(account))).Replace("-", "").ToLowerInvariant();
            string directory = Path.Combine(Application.persistentDataPath, "progression-local-v1");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, key + ".json");
            ProgressionState state;
            if (File.Exists(path))
            {
                // A corrupt primary is not silently replaced by a fresh account or stale backup.
                state = JsonConvert.DeserializeObject<ProgressionState>(File.ReadAllText(path));
                Validate(state);
            }
            else state = new ProgressionState { CycleStartedUtc = UtcNow };
            bool retiredEquipped = false;
            for (int i = 0; i < state.MageSlots.Length; i++)
                if (state.MageSlots[i] == 6) { state.MageSlots[i] = -1; retiredEquipped = true; }
            bool mergedMeteor = KingdomIdle.MageTower.MageCatalogMigration.Apply(state);
            if (retiredEquipped || mergedMeteor) { Validate(state); state.Revision = checked(state.Revision + 1); WriteSnapshot(path, state); }
            _path = path; _state = state; AccountKey = account; LastError = null;
        }
        public static bool Execute(string operation, Func<ProgressionState, bool> mutate, string claimId = null)
        {
            Ensure();
            if (_busy || mutate == null || string.IsNullOrWhiteSpace(operation)) return false;
            CompleteCounterSave(true);
            if (claimId != null && _state.Claims.Contains(claimId)) return false;
            _busy = true;
            try
            {
                var draft = _state.DeepClone();
                QuestEconomy.Before(draft);
                if (!mutate(draft)) return false;
                QuestEconomy.After(draft);
                if (claimId != null) draft.Claims.Add(claimId);
                draft.Revision = checked(_state.Revision + 1);
                Validate(draft);
                WriteSnapshot(_path, draft);
                _state = draft; LastError = null;
                _countersDirty = false;
            }
            catch (Exception exception)
            {
                LastError = operation + ": " + exception.Message;
                Debug.LogWarning("[Progression] Transaction rejected: " + LastError);
                return false;
            }
            finally { _busy = false; }
            // Observers cannot roll back a committed transaction or prevent other observers.
            NotifyObservers();
            return true;
        }
        private static void NotifyObservers()
        {
            if (Changed != null) foreach (Action observer in Changed.GetInvocationList())
                try { observer(); } catch (Exception ex) { Debug.LogException(ex); }
        }
        private static void WriteSnapshot(string path, ProgressionState snapshot)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot));
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
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
