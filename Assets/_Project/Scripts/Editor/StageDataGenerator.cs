using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using Core.Stage;
using ExcelDataReader;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.SO;
using UnityEditor;
using UnityEngine;

namespace Scripts.Core.Parser
{
    /// <summary>
    /// personalDocs/Stage_Revised.xlsx를 StageDatabaseSO와 eStage enum으로 변환한다.
    /// 런타임에서는 엑셀을 읽지 않으며, 기획 데이터가 바뀔 때 이 메뉴를 다시 실행하면 된다.
    /// </summary>
    public static class StageDataGenerator
    {
        private const string ExcelRelativePath = "personalDocs/Stage_Revised.xlsx";
        private const string DatabaseAssetPath = "Assets/_Project/ScriptableObjects/StageDatabaseSO.asset";
        private const string StageEnumAssetPath = "Assets/_Project/Scripts/Core/AutoGenEnum/StageEnum.cs";

        // 기존 eStage ID의 상위 분류 값이다. StageParser의 비트 마스크와 반드시 같은 배치를 사용해야 한다.
        private const long StageCategoryMask = 0x0000000200000000;
        private const long GoldDungeonMask = 0x0000000010000000;
        private const long RubyDungeonMask = 0x0000000020000000;

        [MenuItem("MyTools/Stage/Generate Stage Data")]
        public static void Generate()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string excelPath = Path.Combine(projectRoot, ExcelRelativePath);

                if (!File.Exists(excelPath))
                    throw new FileNotFoundException("스테이지 엑셀 파일을 찾을 수 없습니다.", excelPath);

                DataSet workbook = ReadWorkbook(excelPath);
                List<StageDraft> stages = ReadStageDefinitions(workbook);

                // StageMonsters는 StageDefinitions를 참조하므로 Definition을 먼저 읽은 뒤 합친다.
                ReadStageMonsters(workbook, stages);
                List<BossFlowRecord> bossFlows = ReadBossFlows(workbook);
                List<KillCountFlowRecord> killCountFlows = ReadKillCountFlows(workbook);

                ValidateReferences(stages, bossFlows, killCountFlows);

                var records = new List<StageDatabaseRecord>(stages.Count);
                foreach (StageDraft stage in stages)
                    records.Add(stage.ToRecord());

                CreateOrUpdateDatabase(records, bossFlows, killCountFlows);
                WriteStageEnum(stages, projectRoot);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[StageDataGenerator] 생성 완료 - " +
                    $"Stage: {records.Count}, BossFlow: {bossFlows.Count}, " +
                    $"KillCountFlow: {killCountFlows.Count}");
            }
            catch (Exception exception)
            {
                // 예외 메시지에는 시트명과 실제 Excel 행 번호가 포함되도록 각 파서에서 감싸서 전달한다.
                Debug.LogError($"[StageDataGenerator] 생성 실패\n{exception}");
            }
        }

        private static DataSet ReadWorkbook(string excelPath)
        {
            // Excel이 열려 있어도 읽을 수 있게 FileShare.ReadWrite를 사용한다.
            using (var stream = new FileStream(
                       excelPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            {
                var configuration = new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                };

                return reader.AsDataSet(configuration);
            }
        }

        private static List<StageDraft> ReadStageDefinitions(DataSet workbook)
        {
            const string sheetName = "StageDefinitions";
            DataTable sheet = GetRequiredSheet(workbook, sheetName);
            var result = new List<StageDraft>();
            var ids = new HashSet<long>();

            for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                DataRow row = sheet.Rows[rowIndex];
                if (IsEmptyRow(row))
                    continue;

                int excelRow = rowIndex + 2;
                eStageType stageType = ReadEnum<eStageType>(row, "StageType", sheetName, excelRow);
                int stageNumber = ReadInt(row, "Stage", sheetName, excelRow);
                int waveNumber = ReadInt(row, "Wave", sheetName, excelRow);
                eStage stageId = CreateStageId(stageType, stageNumber, waveNumber, sheetName, excelRow);

                if (!ids.Add((long)stageId))
                    throw RowError(sheetName, excelRow, $"중복 스테이지입니다: {stageType} {stageNumber}-{waveNumber}");

                string bgmText = ReadOptionalString(row, "BgmType");
                bool hasBgm = !string.IsNullOrEmpty(bgmText);
                eSFXType bgmType = default;
                if (hasBgm && !Enum.TryParse(bgmText, true, out bgmType))
                    throw RowError(sheetName, excelRow, $"BgmType '{bgmText}'은 eSFXType에 없습니다.");

                float loopSpawnIntervalSec =
                    ReadFloat(row, "LoopSpawnIntervalSec", sheetName, excelRow);
                int loopSpawnAliveThreshold =
                    ReadInt(row, "LoopSpawnAliveThreshold", sheetName, excelRow);

                if (loopSpawnIntervalSec < 0f)
                    throw RowError(sheetName, excelRow, "LoopSpawnIntervalSec는 0 이상이어야 합니다.");
                if (loopSpawnAliveThreshold < 0)
                    throw RowError(sheetName, excelRow, "LoopSpawnAliveThreshold는 0 이상이어야 합니다.");

                result.Add(new StageDraft(
                    stageId,
                    stageType,
                    stageNumber,
                    waveNumber,
                    ReadEnum<eStageFlowType>(row, "FlowType", sheetName, excelRow),
                    ReadEnum<eEnvironmentId>(row, "EnvironmentId", sheetName, excelRow),
                    ReadDouble(row, "MonsterStatMultiplier", sheetName, excelRow),
                    ReadRequiredString(row, "SpawnPointSetId", sheetName, excelRow),
                    ReadOptionalString(row, "FlowConfigId"),
                    ReadOptionalString(row, "RewardGroupId"),
                    ReadFloat(row, "TimeLimitSec", sheetName, excelRow),
                    loopSpawnIntervalSec,
                    loopSpawnAliveThreshold,
                    hasBgm,
                    bgmType,
                    ReadBool(row, "Enabled", sheetName, excelRow)));
            }

            if (result.Count == 0)
                throw new InvalidDataException($"{sheetName} 시트에 데이터가 없습니다.");

            return result;
        }

        private static void ReadStageMonsters(DataSet workbook, List<StageDraft> stages)
        {
            const string sheetName = "StageMonsters";
            DataTable sheet = GetRequiredSheet(workbook, sheetName);
            var stageByKey = new Dictionary<StageKey, StageDraft>();

            foreach (StageDraft stage in stages)
                stageByKey.Add(new StageKey(stage.StageType, stage.StageNumber, stage.WaveNumber), stage);

            for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                DataRow row = sheet.Rows[rowIndex];
                if (IsEmptyRow(row))
                    continue;

                int excelRow = rowIndex + 2;
                var key = new StageKey(
                    ReadEnum<eStageType>(row, "StageType", sheetName, excelRow),
                    ReadInt(row, "Stage", sheetName, excelRow),
                    ReadInt(row, "Wave", sheetName, excelRow));

                if (!stageByKey.TryGetValue(key, out StageDraft stage))
                {
                    throw RowError(
                        sheetName,
                        excelRow,
                        $"대응하는 StageDefinitions 행이 없습니다: {key.Type} {key.Stage}-{key.Wave}");
                }

                var entry = new StageMonsterEntry(
                    ReadEnum<eMonsterType>(row, "MonsterName", sheetName, excelRow),
                    ReadInt(row, "Count", sheetName, excelRow),
                    ReadInt(row, "SpawnWeight", sheetName, excelRow),
                    ReadEnum<eMonsterSpawnPhase>(row, "SpawnPhase", sheetName, excelRow),
                    ReadOptionalString(row, "SpawnPointGroupId"),
                    ReadFloat(row, "SpawnDelaySec", sheetName, excelRow));

                if (entry.Count <= 0)
                    throw RowError(sheetName, excelRow, "Count는 1 이상이어야 합니다.");
                if (entry.SpawnWeight < 0)
                    throw RowError(sheetName, excelRow, "SpawnWeight는 0 이상이어야 합니다.");
                if (entry.SpawnDelaySec < 0f)
                    throw RowError(sheetName, excelRow, "SpawnDelaySec는 0 이상이어야 합니다.");

                stage.Monsters.Add(entry);
            }
        }

        private static List<BossFlowRecord> ReadBossFlows(DataSet workbook)
        {
            const string sheetName = "BossFlow";
            DataTable sheet = GetRequiredSheet(workbook, sheetName);
            var result = new List<BossFlowRecord>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                DataRow row = sheet.Rows[rowIndex];
                if (IsEmptyRow(row))
                    continue;

                int excelRow = rowIndex + 2;
                string id = ReadRequiredString(row, "FlowConfigId", sheetName, excelRow);
                if (!ids.Add(id))
                    throw RowError(sheetName, excelRow, $"중복 FlowConfigId입니다: {id}");

                result.Add(new BossFlowRecord(
                    id,
                    ReadEnum<eMonsterType>(row, "BossMonsterType", sheetName, excelRow),
                    ReadBool(row, "ClearRemainingMonsters", sheetName, excelRow),
                    ReadDefeatAction(row, sheetName, excelRow)));
            }

            return result;
        }

        private static List<KillCountFlowRecord> ReadKillCountFlows(DataSet workbook)
        {
            const string sheetName = "KillCountFlow";
            DataTable sheet = GetRequiredSheet(workbook, sheetName);
            var result = new List<KillCountFlowRecord>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                DataRow row = sheet.Rows[rowIndex];
                if (IsEmptyRow(row))
                    continue;

                int excelRow = rowIndex + 2;
                string id = ReadRequiredString(row, "FlowConfigId", sheetName, excelRow);
                if (!ids.Add(id))
                    throw RowError(sheetName, excelRow, $"중복 FlowConfigId입니다: {id}");

                string targetText = ReadOptionalString(row, "TargetMonsterType");
                bool targetsAnyMonster = string.IsNullOrEmpty(targetText) ||
                                         string.Equals(targetText, "Any", StringComparison.OrdinalIgnoreCase);
                eMonsterType targetMonsterType = default;
                if (!targetsAnyMonster && !Enum.TryParse(targetText, true, out targetMonsterType))
                {
                    throw RowError(
                        sheetName,
                        excelRow,
                        $"TargetMonsterType '{targetText}'은 eMonsterType 또는 Any여야 합니다.");
                }

                int requiredKillCount = ReadInt(row, "RequiredKillCount", sheetName, excelRow);

                if (requiredKillCount <= 0)
                    throw RowError(sheetName, excelRow, "RequiredKillCount는 1 이상이어야 합니다.");

                result.Add(new KillCountFlowRecord(
                    id,
                    requiredKillCount,
                    targetsAnyMonster,
                    targetMonsterType,
                    ReadDefeatAction(row, sheetName, excelRow)));
            }

            return result;
        }

        private static void ValidateReferences(
            List<StageDraft> stages,
            List<BossFlowRecord> bossFlows,
            List<KillCountFlowRecord> killCountFlows)
        {
            var bossFlowById = new Dictionary<string, BossFlowRecord>(StringComparer.Ordinal);
            foreach (BossFlowRecord flow in bossFlows)
                bossFlowById.Add(flow.ConfigId, flow);

            var killFlowById = new Dictionary<string, KillCountFlowRecord>(StringComparer.Ordinal);
            foreach (KillCountFlowRecord flow in killCountFlows)
                killFlowById.Add(flow.ConfigId, flow);

            foreach (StageDraft stage in stages)
            {
                if (stage.Monsters.Count == 0)
                    throw new InvalidDataException($"{stage.DisplayName}에 StageMonsters 데이터가 없습니다.");

                bool hasLoopPool = false;
                foreach (StageMonsterEntry monster in stage.Monsters)
                {
                    if (monster.SpawnPhase == eMonsterSpawnPhase.LoopPool)
                    {
                        hasLoopPool = true;
                        break;
                    }
                }

                if (hasLoopPool)
                {
                    if (stage.LoopSpawnIntervalSec <= 0f)
                    {
                        throw new InvalidDataException(
                            $"{stage.DisplayName}의 LoopSpawnIntervalSec는 0보다 커야 합니다.");
                    }

                    if (stage.LoopSpawnAliveThreshold <= 0)
                    {
                        throw new InvalidDataException(
                            $"{stage.DisplayName}의 LoopSpawnAliveThreshold는 1 이상이어야 합니다.");
                    }
                }

                switch (stage.FlowType)
                {
                    case eStageFlowType.MainProgression:
                        // 메인은 MainStageRule이 진행을 계산하므로 FlowConfigId가 없어야 자연스럽다.
                        break;

                    case eStageFlowType.BossChallenge:
                        if (!bossFlowById.TryGetValue(stage.FlowConfigId, out BossFlowRecord bossFlow))
                        {
                            throw new InvalidDataException(
                                $"{stage.DisplayName}의 BossFlow '{stage.FlowConfigId}'를 찾을 수 없습니다.");
                        }

                        bool containsBoss = false;
                        foreach (StageMonsterEntry monster in stage.Monsters)
                        {
                            if (monster.MonsterType == bossFlow.BossMonsterType)
                            {
                                containsBoss = true;
                                break;
                            }
                        }

                        if (!containsBoss)
                        {
                            throw new InvalidDataException(
                                $"{stage.DisplayName}의 몬스터 목록에 BossFlow 보스 " +
                                $"'{bossFlow.BossMonsterType}'가 없습니다.");
                        }
                        break;

                    case eStageFlowType.KillCountChallenge:
                        if (!killFlowById.ContainsKey(stage.FlowConfigId))
                        {
                            throw new InvalidDataException(
                                $"{stage.DisplayName}의 KillCountFlow '{stage.FlowConfigId}'를 찾을 수 없습니다.");
                        }

                        break;

                    default:
                        throw new InvalidDataException(
                            $"{stage.DisplayName}의 FlowType '{stage.FlowType}'은 현재 생성기가 지원하지 않습니다.");
                }
            }
        }

        private static void CreateOrUpdateDatabase(
            List<StageDatabaseRecord> stages,
            List<BossFlowRecord> bossFlows,
            List<KillCountFlowRecord> killCountFlows)
        {
            StageDatabaseSO database = AssetDatabase.LoadAssetAtPath<StageDatabaseSO>(DatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<StageDatabaseSO>();
                AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            }

            Undo.RecordObject(database, "Generate Stage Database");
            database.ReplaceData(stages, bossFlows, killCountFlows);
            EditorUtility.SetDirty(database);
        }

        private static void WriteStageEnum(List<StageDraft> stages, string projectRoot)
        {
            // 이름을 key로 사용해 같은 스테이지의 그룹 ID가 여러 웨이브에서 중복 추가되는 것을 막는다.
            var valuesByName = new Dictionary<string, long>(StringComparer.Ordinal);
            valuesByName["GoldDungeon"] = StageCategoryMask | GoldDungeonMask;
            valuesByName["RubyDungeon"] = StageCategoryMask | RubyDungeonMask;

            foreach (StageDraft stage in stages)
            {
                long groupId = CreateRawStageId(stage.StageType, stage.StageNumber, 0);
                string groupName = GetStageGroupName(stage.StageType, stage.StageNumber);
                string stageName = GetStageName(stage.StageType, stage.StageNumber, stage.WaveNumber);

                AddEnumValue(valuesByName, groupName, groupId);
                AddEnumValue(valuesByName, stageName, (long)stage.Id);
            }

            var values = new List<KeyValuePair<string, long>>(valuesByName);
            values.Sort((left, right) =>
            {
                int valueCompare = left.Value.CompareTo(right.Value);
                return valueCompare != 0
                    ? valueCompare
                    : string.CompareOrdinal(left.Key, right.Key);
            });

            var source = new StringBuilder();
            source.AppendLine("// <auto-generated>");
            source.AppendLine("// Stage_Revised.xlsx에서 생성됩니다. 직접 수정하지 마세요.");
            source.AppendLine("// </auto-generated>");
            source.AppendLine("namespace Scripts.Core");
            source.AppendLine("{");
            source.AppendLine("    public enum eStage : long");
            source.AppendLine("    {");

            foreach (KeyValuePair<string, long> value in values)
                source.AppendLine($"        {value.Key} = {value.Value}, // 0x{value.Value:X16}");

            source.AppendLine("    }");
            source.AppendLine("}");

            string outputPath = Path.Combine(projectRoot, StageEnumAssetPath);
            File.WriteAllText(outputPath, source.ToString(), new UTF8Encoding(false));
        }

        private static void AddEnumValue(Dictionary<string, long> values, string name, long value)
        {
            if (values.TryGetValue(name, out long oldValue) && oldValue != value)
                throw new InvalidDataException($"eStage 이름이 서로 다른 값을 가리킵니다: {name}");

            values[name] = value;
        }

        private static eStage CreateStageId(
            eStageType type,
            int stage,
            int wave,
            string sheetName,
            int excelRow)
        {
            if (stage < 1 || stage > 0xFFF)
                throw RowError(sheetName, excelRow, "Stage는 1~4095 범위여야 합니다.");
            if (wave < 1 || wave > 0xFFFF)
                throw RowError(sheetName, excelRow, "Wave는 1~65535 범위여야 합니다.");

            return (eStage)CreateRawStageId(type, stage, wave);
        }

        private static long CreateRawStageId(eStageType type, int stage, int wave)
        {
            long contentMask;
            switch (type)
            {
                case eStageType.Main:
                    contentMask = 0;
                    break;
                case eStageType.GoldDungeon:
                    contentMask = GoldDungeonMask;
                    break;
                case eStageType.RubyDungeon:
                    contentMask = RubyDungeonMask;
                    break;
                default:
                    throw new InvalidDataException($"eStage ID를 만들 수 없는 StageType입니다: {type}");
            }

            return StageCategoryMask |
                   contentMask |
                   ((long)stage << StageParser.WaveBitSize) |
                   (uint)wave;
        }

        private static string GetStageGroupName(eStageType type, int stage)
        {
            switch (type)
            {
                case eStageType.Main:
                    return $"Stage{stage}";
                case eStageType.GoldDungeon:
                    return $"GoldDungeon{stage}";
                case eStageType.RubyDungeon:
                    return $"RubyDungeon{stage}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static string GetStageName(eStageType type, int stage, int wave)
        {
            return $"{GetStageGroupName(type, stage)}_{wave}";
        }

        private static DataTable GetRequiredSheet(DataSet workbook, string sheetName)
        {
            DataTable sheet = workbook.Tables[sheetName];
            if (sheet == null)
                throw new InvalidDataException($"필수 시트가 없습니다: {sheetName}");
            return sheet;
        }

        private static bool IsEmptyRow(DataRow row)
        {
            foreach (object value in row.ItemArray)
            {
                if (value != null && value != DBNull.Value &&
                    !string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture)))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ReadRequiredString(
            DataRow row,
            string column,
            string sheetName,
            int excelRow)
        {
            string value = ReadOptionalString(row, column);
            if (string.IsNullOrEmpty(value))
                throw RowError(sheetName, excelRow, $"{column} 값이 비어 있습니다.");
            return value;
        }

        private static string ReadOptionalString(DataRow row, string column)
        {
            EnsureColumn(row.Table, column);
            object value = row[column];
            return value == null || value == DBNull.Value
                ? string.Empty
                : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
        }

        private static int ReadInt(DataRow row, string column, string sheetName, int excelRow)
        {
            double value = ReadDouble(row, column, sheetName, excelRow);
            if (Math.Abs(value % 1d) > 0.000001d)
                throw RowError(sheetName, excelRow, $"{column}은 정수여야 합니다: {value}");
            return checked((int)value);
        }

        private static float ReadFloat(DataRow row, string column, string sheetName, int excelRow)
        {
            return (float)ReadDouble(row, column, sheetName, excelRow);
        }

        private static double ReadDouble(DataRow row, string column, string sheetName, int excelRow)
        {
            string text = ReadRequiredString(row, column, sheetName, excelRow);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw RowError(sheetName, excelRow, $"{column}은 숫자여야 합니다: {text}");
            return value;
        }

        private static bool ReadBool(DataRow row, string column, string sheetName, int excelRow)
        {
            string text = ReadRequiredString(row, column, sheetName, excelRow);
            if (bool.TryParse(text, out bool value))
                return value;
            if (text == "1")
                return true;
            if (text == "0")
                return false;

            throw RowError(sheetName, excelRow, $"{column}은 TRUE/FALSE 또는 1/0이어야 합니다: {text}");
        }

        private static T ReadEnum<T>(DataRow row, string column, string sheetName, int excelRow)
            where T : struct
        {
            string text = ReadRequiredString(row, column, sheetName, excelRow);
            if (!Enum.TryParse(text, true, out T value) || !Enum.IsDefined(typeof(T), value))
                throw RowError(sheetName, excelRow, $"{column} 값 '{text}'은 {typeof(T).Name}에 없습니다.");
            return value;
        }

        private static eStageFlowAction ReadDefeatAction(
            DataRow row,
            string sheetName,
            int excelRow)
        {
            eStageFlowAction action =
                ReadEnum<eStageFlowAction>(row, "DefeatAction", sheetName, excelRow);

            if (action is eStageFlowAction.RestartStage or
                eStageFlowAction.AwaitDefeatChoice or
                eStageFlowAction.ReturnToMainStage)
            {
                return action;
            }

            throw RowError(
                sheetName,
                excelRow,
                $"DefeatAction '{action}' is not allowed. " +
                $"Use {eStageFlowAction.RestartStage}, " +
                $"{eStageFlowAction.AwaitDefeatChoice}, or " +
                $"{eStageFlowAction.ReturnToMainStage}.");
        }

        private static void EnsureColumn(DataTable table, string column)
        {
            if (!table.Columns.Contains(column))
                throw new InvalidDataException($"{table.TableName} 시트에 필수 열이 없습니다: {column}");
        }

        private static InvalidDataException RowError(string sheetName, int excelRow, string message)
        {
            return new InvalidDataException($"[{sheetName}!{excelRow}행] {message}");
        }

        private readonly struct StageKey : IEquatable<StageKey>
        {
            public readonly eStageType Type;
            public readonly int Stage;
            public readonly int Wave;

            public StageKey(eStageType type, int stage, int wave)
            {
                Type = type;
                Stage = stage;
                Wave = wave;
            }

            public bool Equals(StageKey other)
            {
                return Type == other.Type && Stage == other.Stage && Wave == other.Wave;
            }

            public override bool Equals(object obj)
            {
                return obj is StageKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (int)Type;
                    hashCode = (hashCode * 397) ^ Stage;
                    hashCode = (hashCode * 397) ^ Wave;
                    return hashCode;
                }
            }
        }

        private sealed class StageDraft
        {
            public readonly eStage Id;
            public readonly eStageType StageType;
            public readonly int StageNumber;
            public readonly int WaveNumber;
            public readonly eStageFlowType FlowType;
            public readonly eEnvironmentId EnvironmentId;
            public readonly double MonsterStatMultiplier;
            public readonly string SpawnPointSetId;
            public readonly string FlowConfigId;
            public readonly string RewardGroupId;
            public readonly float TimeLimitSec;
            public readonly float LoopSpawnIntervalSec;
            public readonly int LoopSpawnAliveThreshold;
            public readonly bool HasBgm;
            public readonly eSFXType BgmType;
            public readonly bool Enabled;
            public readonly List<StageMonsterEntry> Monsters = new List<StageMonsterEntry>();

            public string DisplayName => $"{StageType} {StageNumber}-{WaveNumber}";

            public StageDraft(
                eStage id,
                eStageType stageType,
                int stageNumber,
                int waveNumber,
                eStageFlowType flowType,
                eEnvironmentId environmentId,
                double monsterStatMultiplier,
                string spawnPointSetId,
                string flowConfigId,
                string rewardGroupId,
                float timeLimitSec,
                float loopSpawnIntervalSec,
                int loopSpawnAliveThreshold,
                bool hasBgm,
                eSFXType bgmType,
                bool enabled)
            {
                Id = id;
                StageType = stageType;
                StageNumber = stageNumber;
                WaveNumber = waveNumber;
                FlowType = flowType;
                EnvironmentId = environmentId;
                MonsterStatMultiplier = monsterStatMultiplier;
                SpawnPointSetId = spawnPointSetId;
                FlowConfigId = flowConfigId;
                RewardGroupId = rewardGroupId;
                TimeLimitSec = timeLimitSec;
                LoopSpawnIntervalSec = loopSpawnIntervalSec;
                LoopSpawnAliveThreshold = loopSpawnAliveThreshold;
                HasBgm = hasBgm;
                BgmType = bgmType;
                Enabled = enabled;
            }

            public StageDatabaseRecord ToRecord()
            {
                return new StageDatabaseRecord(
                    Id,
                    FlowType,
                    EnvironmentId,
                    MonsterStatMultiplier,
                    SpawnPointSetId,
                    FlowConfigId,
                    RewardGroupId,
                    TimeLimitSec,
                    LoopSpawnIntervalSec,
                    LoopSpawnAliveThreshold,
                    HasBgm,
                    BgmType,
                    Enabled,
                    new List<StageMonsterEntry>(Monsters));
            }
        }
    }
}
