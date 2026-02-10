using ExcelDataReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;



namespace Scripts.Core.Parser
{

    // 엑셀 파일에서 읽어온 데이터를 기반으로
    // 파일이름 -> GUID -> 프리펩 탐색 -> addressable ID 자동등록
    // 프리펩들을 Addressable ID를 수정해야함. 
    public class AutoAddressable
    {
        class AssetData
        {
            public AssetData(string name, ulong maskedID)
            {
                fileName = name;
                _MaskedId = maskedID;
            }
            public string fileName;
            public ulong _MaskedId;
        }

        class ReadFromXlsx
        {
            public ReadFromXlsx(AssetData[][] data, string fileName)
            {
                _fileName = fileName;
                _AssetDatas = data;
            }
            public string _fileName;
            public AssetData[][] _AssetDatas;
        }

        private AssetData[][] AssetDatas;
        private Dictionary<string, string> FileNameToGuID;
        private int excelSheetCount;
        public void Init()
        {
            FileNameToGuID = new Dictionary<string, string>();
        }

        [MenuItem("MyTools/SetVFXAddress")]
        private static void SetVFXAddress()
        {
            AutoAddressable auto = new AutoAddressable();
            auto.Init();
            auto.LoadGuIDFromUnity("t:Prefab", new[] { "Assets/Scripts/Core/TestResource/VFX" });
            auto.ReadXlsxFile(@"Scripts\Core\Parser\vfx.xlsx");
            auto.SettingAddressable("VFX");
        }
        [MenuItem("MyTools/SetMonsterAddress")]
        private static void SetMonsterAddress()
        {
            //몬스터 Prefab들을 Addressable로 등록하는 과정
            AutoAddressable auto = new AutoAddressable();
            auto.Init();
            auto.LoadGuIDFromUnity("t:Prefab", new[] { "Assets/Scripts/Core/TestResource/Monster" });
            auto.ReadXlsxFile(@"Scripts\Core\Parser\Monster.xlsx");
            auto.SettingAddressable("Monster");
        }
        [MenuItem("MyTools/GenerateEnum")]
        private static void GenerateEnum()
        {
            AutoAddressable auto = new AutoAddressable();
            auto.GenerateEnumCode();
        }

        private void GenerateEnumCode()
        {
            //VFX,SFX,MONSTER의 ID와 ENUM을 자동생성하는 코드.
            List<ReadFromXlsx> _ReadFromXlsx = new List<ReadFromXlsx>();

            ReadXlsxFile(@"Scripts\Core\Parser\vfx.xlsx");
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, "eVFXType"));

            ReadXlsxFile(@"Scripts\Core\Parser\Monster.xlsx");
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, "eMonsterType"));

            GenerateVFXSFXEnum(_ReadFromXlsx);
            ReadXlsxStageFile();
        }

        private void ReadXlsxStageFile()
        {
            const string path = @"Scripts\Core\Parser\Stage.xlsx";
            string FilePath = Path.Combine(Application.dataPath, path);

            FileStream fs = File.Open(FilePath, FileMode.Open, FileAccess.Read);
            IExcelDataReader reader = ExcelReaderFactory.CreateReader(fs);

            var config = new ExcelDataSetConfiguration
            {
                ConfigureDataTable = (reader) => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            };

            DataSet data = reader.AsDataSet(config);
            //StageEnum
            // stageEnum  = key 
            StringBuilder sb = new StringBuilder();

            //중복검사
            HashSet<int> duplicate = new HashSet<int>();

            var ExcelTable = data.Tables;
            //Sheet순회
            for (int i = 0; i < ExcelTable.Count; i++)
            {
                DataTable table = ExcelTable[i];
                sb.Append($"namespace Scripts.Core {{\n");
                sb.Append($"public enum eStage : int\n{{");
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    DataRow dataRow = table.Rows[row];
                    int stage = Convert.ToInt32(dataRow["Stage"]);
                    int wave = Convert.ToInt32(dataRow["Wave"]);
                    int key = stage << 16 | wave;

                    if (duplicate.Add(key) == true)
                    {
                        sb.Append($"Stage{stage}_{wave} = {key},\n");
                    }
                }
                sb.Append($"}}\n}}");
            }
            fs.Close();

            string enumPath = Path.Combine(Application.dataPath, @"Scripts\Core\StageEnum.cs");
            WriteToFIle(enumPath, sb);

        }

        private void GenerateVFXSFXEnum(List<ReadFromXlsx> _ReadFromXlsx)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder HelperFuncSb = new StringBuilder();

            for (int i = 0; i < _ReadFromXlsx.Count; i++)
            {
                AssetData[][] data = _ReadFromXlsx[i]._AssetDatas;
                sb.Append($"namespace Scripts.Core {{\n");
                sb.Append($"public enum {_ReadFromXlsx[i]._fileName} : ulong\n{{");

                HelperFuncSb.Append($"namespace Scripts.Core {{\n");
                HelperFuncSb.Append($"public static class {_ReadFromXlsx[i]._fileName}Helper {{\n");
                HelperFuncSb.Append($"public static {_ReadFromXlsx[i]._fileName} Parse(string id){{\n");
                HelperFuncSb.Append($"switch (id) {{\n");

                for (int j = 0; j < data.Length; j++)
                {
                    AssetData[] SheetData = data[j];

                    for (int k = 0; k < SheetData.Length; k++)
                    {
                        AssetData rowData = SheetData[k];
                        /**/
                        sb.Append($"{rowData.fileName} = {rowData._MaskedId},\n");
                        HelperFuncSb.Append($"case \"{rowData.fileName}\" : return {_ReadFromXlsx[i]._fileName}.{rowData.fileName};\n");
                    }

                }
                HelperFuncSb.Append($"default : return default;");
                sb.Append($"}}\n}}");
                HelperFuncSb.Append($"}}\n}}\n}}\n}}");
            }
            //FileStream Open.
            string enumPath = Path.Combine(Application.dataPath, @"Scripts\Core\GenerateEnum.cs");
            string helperPath = Path.Combine(Application.dataPath, @"Scripts\Core\EnumHelper.cs");

            WriteToFIle(enumPath, sb);
            WriteToFIle(helperPath, HelperFuncSb);
        }
        //엑셀 파일을 읽어와야함.
        private void ReadXlsxFile(string path)
        {
            string FilePath = Path.Combine(Application.dataPath, path);
            FileStream fstream = File.Open(FilePath, FileMode.Open, FileAccess.Read);
            IExcelDataReader reader = ExcelReaderFactory.CreateReader(fstream);

            //Header제외 옵션
            var conf = new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            };
            DataSet result = reader.AsDataSet(conf);

            var tables = result.Tables;
            AssetDatas = new AssetData[tables.Count][];
            for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
            {
                DataTable sheet = tables[sheetIndex];
                int ArrayLength = sheet.Rows.Count;

                int index = 0;
                AssetDatas[sheetIndex] = new AssetData[ArrayLength];
                for (int row = 0; row < sheet.Rows.Count; row++)
                {
                    DataRow data = sheet.Rows[row];
                    string name = data["fileName"].ToString();
                    ulong maskedId = Convert.ToUInt64(data["MaskedId"]);

                    AssetData vfxData = new AssetData(name, maskedId);
                    AssetDatas[sheetIndex][index++] = vfxData;
                }
            }
            //AssetDatabase.StopAssetEditing();
            reader.Close();
            fstream.Close();
        }
        private void LoadGuIDFromUnity(string filter, string[] searchFolders)
        {
            string[] guids = AssetDatabase.FindAssets(filter, searchFolders);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(assetPath);

                if (!FileNameToGuID.ContainsKey(fileName))
                {
                    FileNameToGuID.Add(fileName, guid);
                }
            }
        }
        private void SettingAddressable(string groupName)
        {
            //Addressable 설정
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null);
                Debug.Log($"새 그룹 생성됨");
            }
            //AssetDatabase.StartAssetEditing();
            //돌면서, 해당 fileName의 GUID 조회.
            for (int i = 0; i < AssetDatas.Length; i++)
            {
                for (int j = 0; j < AssetDatas[i].Length; j++)
                {
                    bool flag = FileNameToGuID.TryGetValue(AssetDatas[i][j].fileName, out string guid);
                    if (flag == false)
                    {
                        Debug.Log("FileName is not found");
                    }
                    //이게 실제로 Addressable설정해주는 API
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);

                    if (entry != null)
                    {
                        entry.labels.Add(groupName);
                        //entry.address = maskedId.ToString();
                        entry.address = AssetDatas[i][j].fileName;
                        CustomLogger.Log($"[등록 성공] 파일: {AssetDatas[i][j].fileName} -> 주소: {AssetDatas[i][j]._MaskedId}");
                    }
                }
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
        private void WriteToFIle(string Path, StringBuilder sb)
        {
            FileStream fs = File.Open(Path, FileMode.Create, FileAccess.ReadWrite);
            StreamWriter sw = new StreamWriter(fs, Encoding.Unicode, 4096);

            char[] buffer = new char[2048];
            //실질적으로 쓰는 부분
            int length = sb.Length;
            int offset = 0;

            while (offset < length)
            {
                int count = Math.Min(length - offset, buffer.Length);
                sb.CopyTo(offset, buffer, 0, count);

                sw.Write(buffer, 0, count);
                offset += count;
            }

            sw.Close();
            fs.Close();
        }

    }
}

