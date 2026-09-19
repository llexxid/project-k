using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;
using KingdomIdle.Balance;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.Editor
{
    /// <summary>
    /// 베타 기획 엑셀을 런타임 JSON으로 변환한다. 모든 원문 ID를 보존하고 전체 검증 후에만 교체한다.
    /// 에디터 메뉴에서 실행하며 게임 빌드에는 엑셀 의존성이 포함되지 않는다.
    /// </summary>
    public static class QuestCatalogImporter
    {
        public const string SourcePath = "AI/planning/beta-20260913/왕국군키우기_가이드_퀘스트_업적_카탈로그_베타개정.xlsx";
        private const string OutputPath = "Assets/_Project/Resources/Balance/catalog.json";
        private static readonly string[] SheetNames =
        {
            "00_개요", "01_연계맵", "02_가이드", "03_퀘스트", "04_업적", "05_보상그룹", "06_목표타입"
        };

        [MenuItem("KingdomIdle/Quest/Import Beta Quest Catalog")]
        public static void Import()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string json = BuildJson(Path.Combine(projectRoot, SourcePath));
                string destination = Path.Combine(projectRoot, OutputPath);
                if (File.Exists(destination) && JToken.DeepEquals(JToken.Parse(File.ReadAllText(destination)), JToken.Parse(json)))
                {
                    Debug.Log("[QuestCatalogImporter] 카탈로그 내용이 같아 기존 파일을 유지했습니다.");
                    return;
                }

                // 같은 디렉터리의 임시 파일을 원자 교체한다. 검증 실패나 쓰기 실패는 기존 JSON을 남긴다.
                string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.WriteAllText(temporary, json, new UTF8Encoding(false));
                    if (File.Exists(destination)) File.Replace(temporary, destination, null);
                    else File.Move(temporary, destination);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[QuestCatalogImporter] 검증된 카탈로그를 저장했습니다. 실행 중인 게임은 다시 시작해 새 정의를 읽습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[QuestCatalogImporter] 기존 카탈로그를 교체하지 못했습니다.\n" + exception);
            }
        }

        /// <summary>엑셀을 읽고 검증된 JSON만 반환한다. 테스트에서 호출해도 파일을 쓰지 않는다.</summary>
        public static string BuildJson(string workbookPath)
        {
            using (var stream = new FileStream(workbookPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
            using (DataSet workbook = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
            }))
            {
                foreach (string name in SheetNames)
                    if (!workbook.Tables.Contains(name)) throw new InvalidDataException("필수 시트가 없습니다: " + name);

                var source = new JObject
                {
                    ["guide"] = ReadGuide(workbook.Tables["02_가이드"]),
                    ["quests"] = ReadPeriodic(workbook.Tables["03_퀘스트"]),
                    ["achievements"] = ReadAchievements(workbook.Tables["04_업적"]),
                    ["rewards"] = ReadRewards(workbook.Tables["05_보상그룹"])
                };
                string json = source.ToString(Formatting.Indented) + "\n";
                QuestCatalog.Parse(json);
                return json;
            }
        }

        private static JArray ReadGuide(DataTable sheet)
        {
            Headers(sheet, "QuestId", "코드", "제목", "목표", "ObjectiveType", "ProgressMode", "TargetId", "Required", "NextQuestId", "RewardGroupId", "Presentation", "비고");
            var result = new JArray();
            foreach (DataRow row in DataRows(sheet, 0))
            {
                result.Add(new JObject
                {
                    ["id"] = Number(row, 0), ["code"] = Cell(row, 1), ["title"] = Cell(row, 2),
                    ["target"] = Cell(row, 3), ["type"] = Cell(row, 4), ["mode"] = Cell(row, 5),
                    ["targetid"] = Number(row, 6), ["required"] = Number(row, 7), ["next"] = Number(row, 8),
                    ["reward"] = Number(row, 9), ["presentation"] = Cell(row, 10), ["status"] = Status(row, 11)
                });
            }
            return result;
        }

        private static JArray ReadPeriodic(DataTable sheet)
        {
            Headers(sheet, "구분", "QuestId", "코드", "목표", "ObjectiveType", "ProgressMode", "TargetId", "Required", "RewardGroupId", "비고");
            var result = new JArray();
            foreach (DataRow row in DataRows(sheet, 1))
            {
                result.Add(new JObject
                {
                    ["category"] = Cell(row, 0), ["id"] = Number(row, 1), ["code"] = Cell(row, 2),
                    ["target"] = Cell(row, 3), ["type"] = Cell(row, 4), ["mode"] = Cell(row, 5),
                    ["targetid"] = Number(row, 6), ["required"] = Number(row, 7),
                    ["reward"] = Number(row, 8), ["status"] = Status(row, 9)
                });
            }
            return result;
        }

        private static JArray ReadAchievements(DataTable sheet)
        {
            Headers(sheet, "계열", "QuestId", "단계", "목표", "ObjectiveType", "ProgressMode", "TargetId", "Required", "NextQuestId", "RewardGroupId", "비고");
            var result = new JArray();
            foreach (DataRow row in DataRows(sheet, 1))
            {
                result.Add(new JObject
                {
                    ["series"] = Cell(row, 0), ["id"] = Number(row, 1), ["tier"] = Cell(row, 2),
                    ["target"] = Cell(row, 3), ["type"] = Cell(row, 4), ["mode"] = Cell(row, 5),
                    ["targetid"] = Number(row, 6), ["required"] = Number(row, 7), ["next"] = Number(row, 8),
                    ["reward"] = Number(row, 9), ["status"] = Status(row, 10)
                });
            }
            return result;
        }

        private static JArray ReadRewards(DataTable sheet)
        {
            Headers(sheet, "RewardGroupId", "용도", "재화 1", "수량 1", "재화 2", "수량 2", "비고");
            var result = new JArray();
            foreach (DataRow row in DataRows(sheet, 0))
            {
                long id = Number(row, 0);
                var reward = new JObject
                {
                    ["id"] = id, ["usage"] = Cell(row, 1), ["currency1"] = Cell(row, 2),
                    ["amount1"] = Amount(row, 3), ["currency2"] = Cell(row, 4), ["amount2"] = Amount(row, 5)
                };
                if (id == 2001)
                {
                    // 현 JSON의 명시적 동적 규칙을 유지한다. 다른 0 수량을 동적으로 추측하지 않는다.
                    string note = Cell(row, 6);
                    if (!note.Contains("× 2분") || !note.Contains("KPM≤30"))
                        throw Error(row, 6, "동적 골드의 2분/KPM≤30 규칙 변경은 계산 코드와 함께 반영해야 합니다.");
                    reward["calculation"] = "DynamicGold";
                    reward["minutes"] = 2;
                    reward["amount1Meaning"] = "동적 지급 표식";
                    reward["kpmCap"] = 30;
                }
                result.Add(reward);
            }
            return result;
        }

        private static void Headers(DataTable sheet, params string[] expected)
        {
            if (sheet.Rows.Count < 4 || sheet.Columns.Count < expected.Length)
                throw new InvalidDataException(sheet.TableName + ": 4행 헤더 또는 열이 누락되었습니다.");
            for (int column = 0; column < expected.Length; column++)
                if (Cell(sheet.Rows[3], column) != expected[column])
                    throw Error(sheet.Rows[3], column, "헤더가 '" + expected[column] + "'이어야 합니다.");
        }

        private static IEnumerable<DataRow> DataRows(DataTable sheet, int idColumn)
        {
            // 제목 1~2행·빈 3행·헤더 4행 뒤의 데이터만 읽는다. 한 셀짜리 하단 설명은 데이터가 아니다.
            for (int index = 4; index < sheet.Rows.Count; index++)
            {
                DataRow row = sheet.Rows[index];
                int filled = 0;
                for (int column = 0; column < sheet.Columns.Count; column++)
                    if (Cell(row, column).Length != 0) filled++;
                if (filled == 0) continue;
                string firstCell = Cell(row, 0);
                if (filled == 1 && (firstCell.StartsWith("일일 최대:", StringComparison.Ordinal) ||
                    firstCell.StartsWith("주간 최대:", StringComparison.Ordinal) ||
                    firstCell.StartsWith("루비:", StringComparison.Ordinal) ||
                    firstCell.StartsWith("무료 마탑 해금", StringComparison.Ordinal))) continue;
                Number(row, idColumn);
                yield return row;
            }
        }

        private static string Status(DataRow row, int column)
        {
            string note = Cell(row, column);
            if (note.StartsWith("폐기:", StringComparison.Ordinal)) return "폐기";
            if (note.StartsWith("보류:", StringComparison.Ordinal)) return "보류";
            return "활성";
        }

        private static string Cell(DataRow row, int column) =>
            row[column] == DBNull.Value ? "" : Convert.ToString(row[column], CultureInfo.InvariantCulture).Trim();

        private static JToken Amount(DataRow row, int column) =>
            Cell(row, column).Length == 0 ? new JValue("") : new JValue(Number(row, column));

        private static long Number(DataRow row, int column)
        {
            string text = Cell(row, column);
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ||
                value != decimal.Truncate(value) || value < long.MinValue || value > long.MaxValue)
                throw Error(row, column, "정수가 필요합니다: '" + text + "'");
            return (long)value;
        }

        private static InvalidDataException Error(DataRow row, int column, string message) =>
            new InvalidDataException(row.Table.TableName + " " + (row.Table.Rows.IndexOf(row) + 1) + "행 " +
                (column + 1) + "열: " + message);
    }
}
