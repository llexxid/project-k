using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Scripts.Core;
using Scripts.Core.SO;
using Scripts.Monster;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Scripts.Core.Parser
{
    public static partial class StageDataGenerator
    {
        private const string ResumeKey = "ProjectK.StageCatalog.Resume";
        internal const string MonsterHelperSource = "namespace Scripts.Core { public static class eMonsterTypeHelper { public static eMonsterType Parse(string id) => System.Enum.TryParse<eMonsterType>(id, out var value) ? value : default; } }\n";

        [InitializeOnLoadMethod]
        private static void ResumeAfterEnumImport()
        {
            if (!SessionState.GetBool(ResumeKey, false)) return;
            EditorApplication.delayCall += () => { SessionState.SetBool(ResumeKey, false); Generate(); };
        }

        internal static string MonsterEnumSource()
            => MonsterEnumSource(ReadWorkbook(Path.GetFullPath(ExcelRelativePath)));

        private static string MonsterEnumSource(DataSet workbook)
        {
            // Retired identifiers stay reserved. Existing save/server IDs must never be reassigned.
            var ids = Enum.GetNames(typeof(eMonsterType)).ToDictionary(n => n, n => Convert.ToUInt64(Enum.Parse(typeof(eMonsterType), n)), StringComparer.Ordinal);
            var catalogNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (row, number) in Rows(workbook, "MonsterCatalog"))
            {
                string name = ReadRequiredString(row, "MonsterName", "MonsterCatalog", number);
                if (!Regex.IsMatch(name, "^[A-Z][A-Z0-9_]*$") || !catalogNames.Add(name)) throw RowError("MonsterCatalog", number, "고유한 대문자 enum 이름이 필요합니다.");
                long raw = Whole(row, "MonsterId", "MonsterCatalog", number);
                if (raw <= 0x100000000L || raw >= 0x200000000L) throw RowError("MonsterCatalog", number, "몬스터 ID 마스크가 잘못되었습니다.");
                ulong id = (ulong)raw;
                if (ids.TryGetValue(name, out var old) && old != id) throw RowError("MonsterCatalog", number, "발급된 몬스터 ID는 변경할 수 없습니다.");
                if (ids.Any(x => x.Key != name && x.Value == id)) throw RowError("MonsterCatalog", number, "다른 몬스터가 사용 중인 ID입니다.");
                ids[name] = id;
            }
            return "namespace Scripts.Core {\n// Generated from Stage_Catalog.xlsx; retired identifiers remain reserved.\npublic enum eMonsterType : ulong\n{\n" +
                string.Join("\n", ids.OrderBy(x => x.Value).Select(x => $"    {x.Key} = {x.Value},")) + "\n}\n}";
        }

        private static bool PrepareMonsterEnum(DataSet workbook)
        {
            const string enumPath = "Assets/_Project/Scripts/Core/AutoGenEnum/GenerateEnum.cs";
            const string helperPath = "Assets/_Project/Scripts/Core/AutoGenEnum/EnumHelper.cs";
            string current = File.ReadAllText(enumPath), expected = MonsterEnumSource(workbook);
            const string pattern = @"namespace Scripts\.Core\s*\{\s*(?://[^\r\n]*\r?\n\s*)?public enum eMonsterType\s*:\s*ulong\s*\{[^}]*\}\s*\}";
            if (!Regex.IsMatch(current, pattern)) throw new InvalidDataException("eMonsterType 생성 영역을 찾을 수 없습니다.");
            string updated = Regex.Replace(current, pattern, _ => expected);
            if (updated == current) return false;
            File.WriteAllText(enumPath, updated, new UTF8Encoding(false));
            string helper = File.ReadAllText(helperPath);
            int start = helper.IndexOf("namespace Scripts.Core {\npublic static class eMonsterTypeHelper", StringComparison.Ordinal);
            if (start < 0) start = helper.IndexOf("namespace Scripts.Core {\r\npublic static class eMonsterTypeHelper", StringComparison.Ordinal);
            if (start >= 0)
            {
                int end = helper.IndexOf("namespace Scripts.Core", start + 1, StringComparison.Ordinal);
                File.WriteAllText(helperPath, helper.Substring(0, start) + MonsterHelperSource + (end < 0 ? "" : helper.Substring(end)), new UTF8Encoding(false));
            }
            SessionState.SetBool(ResumeKey, true);
            AssetDatabase.Refresh();
            Debug.Log("[StageCatalog] 몬스터 ID 컴파일 후 같은 카탈로그 생성을 자동으로 계속합니다.");
            return true;
        }

        private sealed class CatalogImport
        {
            public string Hash;
            public int Tickets;
            public readonly List<StageEnvironmentPreset> Environments = new();
            public readonly List<CatalogMonsterInfo> Monsters = new();
            public readonly List<(string path, string address, bool environment)> Addresses = new();
        }

        private static CatalogImport ReadCatalog(DataSet book, List<StageDraft> stages, string file)
        {
            var import = new CatalogImport();
            if (AddressableAssetSettingsDefaultObject.Settings == null) throw new InvalidOperationException("Addressable 설정이 없습니다.");
            using (var sha = SHA256.Create()) import.Hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
            var infos = new Dictionary<eMonsterType, CatalogMonsterInfo>();
            foreach (var (row, n) in Rows(book, "MonsterCatalog"))
            {
                var type = ReadEnum<eMonsterType>(row, "MonsterName", "MonsterCatalog", n);
                var info = new CatalogMonsterInfo(type, ReadRequiredString(row, "DisplayName", "MonsterCatalog", n),
                    ReadFloat(row, "MoveSpeed", "MonsterCatalog", n), ReadFloat(row, "AttackIntervalSec", "MonsterCatalog", n), ReadFloat(row, "AttackMultiplier", "MonsterCatalog", n));
                if (info.MoveSpeed <= 0 || info.AttackIntervalSec <= 0 || info.AttackMultiplier <= 0) throw RowError("MonsterCatalog", n, "이동 속도·공격 간격·타격 배율은 양수여야 합니다.");
                string path = WorkingPrefab(row, "MonsterCatalog", n);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<Scripts.Monster.Monster>() == null || prefab.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController == null)
                    throw RowError("MonsterCatalog", n, "Monster와 Animator Controller 연결이 필요합니다.");
                infos.Add(type, info); import.Monsters.Add(info);
                if (ReadRequiredString(row, "Content", "MonsterCatalog", n) != "Reserve") import.Addresses.Add((path, type.ToString(), false));
            }
            var pools = new HashSet<string>(StringComparer.Ordinal);
            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (row, n) in Rows(book, "EnvironmentPresets"))
            {
                if (!ReadBool(row, "Enabled", "EnvironmentPresets", n)) continue;
                string pool = ReadRequiredString(row, "PoolId", "EnvironmentPresets", n), id = ReadRequiredString(row, "PresetId", "EnvironmentPresets", n);
                int weight = ReadInt(row, "Weight", "EnvironmentPresets", n);
                string path = WorkingPrefab(row, "EnvironmentPresets", n);
                if (weight <= 0 || !presetIds.Add(id)) throw RowError("EnvironmentPresets", n, "고유 PresetId와 양수 가중치가 필요합니다.");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Grid>() == null) throw RowError("EnvironmentPresets", n, "Grid 환경 프리팹이 필요합니다.");
                pools.Add(pool); import.Environments.Add(new StageEnvironmentPreset(pool, id, weight)); import.Addresses.Add((path, id, true));
            }
            var first = new Dictionary<(int, int), StageFirstClearReward>();
            foreach (var (row, n) in Rows(book, "FirstClearRewards"))
            {
                var reward = new StageFirstClearReward(Whole(row, "AncientCoins", "FirstClearRewards", n), Whole(row, "ClassFragments", "FirstClearRewards", n),
                    ReadInt(row, "UnlockSkillId", "FirstClearRewards", n), ReadInt(row, "FragmentSkillId", "FirstClearRewards", n),
                    ReadInt(row, "SkillFragments", "FirstClearRewards", n), ReadOptionalString(row, "WeaponJob"), ReadInt(row, "WeaponATK", "FirstClearRewards", n), ReadOptionalString(row, "Unlocks"));
                if (reward.AncientCoins < 0 || reward.ClassFragments < 0 || reward.SkillFragments < 0 || reward.UnlockSkillId < -1 || reward.FragmentSkillId < -1 || reward.WeaponAttack < 0)
                    throw RowError("FirstClearRewards", n, "보상 수량 또는 스킬 ID가 잘못되었습니다.");
                first.Add((ReadInt(row, "Stage", "FirstClearRewards", n), ReadInt(row, "Wave", "FirstClearRewards", n)), reward);
            }
            var main = UniqueRows(book, "MainBalance", "Stage", "Wave");
            var gold = UniqueRows(book, "GoldBalance", "Difficulty");
            var ruby = UniqueRows(book, "RubyBalance", "Difficulty", "EnemyIndex");
            var rubyRewards = UniqueRows(book, "RubyRewards", "Difficulty");
            var definitions = Rows(book, "StageDefinitions").ToArray();
            var entries = Rows(book, "StageMonsters").ToArray();
            foreach (var stage in stages)
            {
                var (row, n) = definitions.Single(x => ReadEnum<eStageType>(x.row, "StageType", "StageDefinitions", x.n) == stage.StageType &&
                    ReadInt(x.row, "Stage", "StageDefinitions", x.n) == stage.StageNumber && ReadInt(x.row, "Wave", "StageDefinitions", x.n) == stage.WaveNumber);
                string pool = ReadRequiredString(row, "EnvironmentPoolId", "StageDefinitions", n);
                if (!pools.Contains(pool)) throw RowError("StageDefinitions", n, "사용 가능한 배경 풀이 없습니다: " + pool);
                float delay = ReadFloat(row, "BatchDelaySec", "StageDefinitions", n);
                bool reset = ReadBool(row, "ResetTimerPerEnemy", "StageDefinitions", n);
                if (stage.LoopSpawnAliveThreshold < 1 || stage.LoopSpawnAliveThreshold > 12 || delay < 0 || (reset && stage.TimeLimitSec <= 0))
                    throw RowError("StageDefinitions", n, "소환 한도·전환 시간·보스 타이머를 확인하세요.");
                if (stage.MonsterStatMultiplier != 1) throw RowError("StageDefinitions", n, "수치는 Balance 탭에서 편집합니다. 이중 배율을 막기 위해 MonsterStatMultiplier는 1입니다.");
                var stageRows = entries.Where(x => ReadEnum<eStageType>(x.row, "StageType", "StageMonsters", x.n) == stage.StageType &&
                    ReadInt(x.row, "Stage", "StageMonsters", x.n) == stage.StageNumber && ReadInt(x.row, "Wave", "StageMonsters", x.n) == stage.WaveNumber &&
                    ReadInt(x.row, "Count", "StageMonsters", x.n) > 0).ToArray();
                double drop = 0; long clearRuby = 0, firstRuby = 0;
                for (int i = 0; i < stage.Monsters.Count; i++)
                {
                    var entry = stage.Monsters[i]; var source = stageRows[i];
                    if (!infos.TryGetValue(entry.MonsterType, out var info)) throw RowError("StageMonsters", source.n, "MonsterCatalog에 없는 몬스터입니다.");
                    if (entry.SpawnPointGroupId != "Melee" && entry.SpawnPointGroupId != "Ranged" && entry.SpawnPointGroupId != "Boss") throw RowError("StageMonsters", source.n, "역할은 Melee/Ranged/Boss 중 하나입니다.");
                    int enemyIndex = ReadInt(source.row, "EnemyIndex", "StageMonsters", source.n);
                    string sheet = stage.StageType == eStageType.Main ? "MainBalance" : stage.StageType == eStageType.GoldDungeon ? "GoldBalance" : "RubyBalance";
                    var stat = stage.StageType == eStageType.Main ? main[(stage.StageNumber, stage.WaveNumber)] :
                        stage.StageType == eStageType.GoldDungeon ? gold[(stage.StageNumber, 0)] : ruby[(stage.StageNumber, enemyIndex)];
                    long hp = Whole(stat.row, "HP", sheet, stat.n), attack = Whole(stat.row, "ATK", sheet, stat.n);
                    attack = Math.Max(1, checked((long)Math.Floor(attack * (decimal)info.AttackMultiplier)));
                    long money = sheet == "RubyBalance" ? 0 : Whole(stat.row, "GoldEach", sheet, stat.n);
                    long exp = sheet == "RubyBalance" ? 0 : Whole(stat.row, "ExpEach", sheet, stat.n);
                    float interval = sheet == "RubyBalance" ? ReadFloat(stat.row, "AttackIntervalSec", sheet, stat.n) : info.AttackIntervalSec;
                    var combat = new StageEnemyData(hp, attack, money, exp, info.MoveSpeed, interval);
                    if (!combat.IsValid) throw RowError(sheet, stat.n, "HP·공격력·이동·공격 간격은 양수, 보상은 0 이상이어야 합니다.");
                    stage.Monsters[i] = entry.WithCombat(combat);
                    if (sheet == "RubyBalance" && (ReadRequiredString(stat.row, "MonsterName", sheet, stat.n) != entry.MonsterType.ToString() || !entry.IsBoss || entry.Count != 1))
                        throw RowError(sheet, stat.n, "루비 보스 순번과 StageMonsters가 일치해야 합니다.");
                    if (Math.Abs(ReadFloat(stat.row, "TimeLimitSec", sheet, stat.n) - stage.TimeLimitSec) > .001f) throw RowError(sheet, stat.n, "StageDefinitions 제한 시간과 불일치합니다.");
                    if (sheet != "RubyBalance")
                    {
                        if (stage.Monsters.Sum(x => x.Count) != Whole(stat.row, "Count", sheet, stat.n)) throw RowError(sheet, stat.n, "StageMonsters 합계와 총수가 다릅니다.");
                        drop = ReadDouble(stat.row, "EquipmentDropRate", sheet, stat.n);
                        if (drop < 0 || drop > 1) throw RowError(sheet, stat.n, "드롭률은 0~1입니다.");
                    }
                }
                if (stage.StageType == eStageType.RubyDungeon)
                {
                    var reward = rubyRewards[(stage.StageNumber, 0)];
                    clearRuby = Whole(reward.row, "ClearRuby", "RubyRewards", reward.n); firstRuby = Whole(reward.row, "FirstClearRuby", "RubyRewards", reward.n);
                    if (clearRuby < 0 || firstRuby < 0 || !reset || stage.LoopSpawnAliveThreshold != 1) throw RowError("RubyRewards", reward.n, "루비 순차 전투 설정을 확인하세요.");
                }
                first.TryGetValue((stage.StageNumber, stage.WaveNumber), out var firstReward);
                stage.Encounter = new StageEncounterData(pool, delay, reset, drop, clearRuby, firstRuby, stage.StageType == eStageType.Main ? firstReward : default);
            }
            foreach (var key in first.Keys) if (!stages.Any(s => s.StageType == eStageType.Main && s.StageNumber == key.Item1 && s.WaveNumber == key.Item2)) throw new InvalidDataException("최초 보상의 스테이지가 없습니다.");
            var tickets = Rows(book, "Inputs").Single(x => ReadRequiredString(x.row, "Key", "Inputs", x.n) == "dailyTickets");
            import.Tickets = ReadInt(tickets.row, "Value", "Inputs", tickets.n);
            if (import.Tickets < 1) throw new InvalidDataException("일일 입장권은 1 이상이어야 합니다.");
            return import;
        }

        private static IEnumerable<(DataRow row, int n)> Rows(DataSet book, string sheet)
        {
            var table = GetRequiredSheet(book, sheet);
            for (int i = 0; i < table.Rows.Count; i++) if (!IsEmptyRow(table.Rows[i])) yield return (table.Rows[i], i + 2);
        }
        private static Dictionary<(int, int), (DataRow row, int n)> UniqueRows(DataSet book, string sheet, string first, string second = null)
        {
            var result = new Dictionary<(int, int), (DataRow row, int n)>();
            foreach (var (row, n) in Rows(book, sheet))
                if (!result.TryAdd((ReadInt(row, first, sheet, n), second == null ? 0 : ReadInt(row, second, sheet, n)), (row, n))) throw RowError(sheet, n, "중복 키입니다.");
            return result;
        }
        private static long Whole(DataRow row, string column, string sheet, int number)
        {
            decimal value;
            if (!decimal.TryParse(Convert.ToString(row[column], CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value != decimal.Truncate(value) || value > long.MaxValue || value < long.MinValue)
                throw RowError(sheet, number, column + "에 정수 값이 필요합니다. Excel에서 수식을 계산하고 저장하세요.");
            return (long)value;
        }
        private static string WorkingPrefab(DataRow row, string sheet, int n)
        {
            string path = ReadRequiredString(row, "PrefabPath", sheet, n);
            if (!path.StartsWith("Assets/_Project/Prefabs/", StringComparison.Ordinal) || path.Contains("..") || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw RowError(sheet, n, "프로젝트 작업용 프리팹 경로가 필요합니다: " + path);
            return path;
        }
        private static void CommitCatalog(CatalogImport import)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressable 설정이 없습니다.");
            foreach (bool environment in new[] { false, true })
            {
                string groupName = environment ? "Stage Environments" : "Stage Monsters";
                var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                // Maps share hundreds of tiles. A shared bundle avoids duplicating those dependencies 90 times;
                // LoadAssetAsync still instantiates only the selected map and loads its referenced textures.
                schema.BundleMode = environment ? BundledAssetGroupSchema.BundlePackingMode.PackTogether : BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                schema.BuildPath.SetVariableByName(settings, "Local.BuildPath"); schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
                var wanted = new HashSet<string>(import.Addresses.Where(x => x.environment == environment).Select(x => AssetDatabase.AssetPathToGUID(x.path)));
                foreach (var obsolete in group.entries.Where(x => !wanted.Contains(x.guid)).ToArray()) settings.RemoveAssetEntry(obsolete.guid);
                foreach (var asset in import.Addresses.Where(x => x.environment == environment))
                {
                    var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(asset.path), group);
                    entry.address = asset.address;
                }
                EditorUtility.SetDirty(group); EditorUtility.SetDirty(schema);
            }
            var database = AssetDatabase.LoadAssetAtPath<StageDatabaseSO>(DatabaseAssetPath);
            database.SetCatalog(import.Hash, import.Environments, import.Monsters, import.Tickets);
            EditorUtility.SetDirty(database); EditorUtility.SetDirty(settings);
        }
    }
}
