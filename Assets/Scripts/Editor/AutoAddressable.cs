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
using Scripts.Core;



namespace Scripts.Core.Parser
{

    // 엑셀 파일에서 읽어온 데이터를 기반으로
    // 파일이름 -> GUID -> 프리펩 탐색 -> addressable ID 자동등록
    // 프리펩들을 Addressable ID를 수정해야함. 
    
    public class AutoAddressable
    {
		static readonly string OPEN_BRACE = $"{{\n";
		static readonly string CLOSE_BRACE = $"}}\n";
        static readonly string VFX_STRING = $"VFX";
        static readonly string MONSTER_STRING = $"Monster";
        static readonly string SFX_STRING = $"SFX";
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
            auto.LoadGuIDFromUnity("t:Prefab", new[] { ConstPath.VFX_PREFEB_PATH });
            auto.ReadXlsxFile(ConstPath.VFX_EXCEL_PATH);
            auto.SettingAddressable(VFX_STRING);
        }
        [MenuItem("MyTools/GenerateMetaSO")]
        private static void GenerateMetaSO()
        {
            AutoAddressable auto = new AutoAddressable();
            auto.Init();
            auto.GenerateStageMetaSO();
            auto.GenerateMonsterMetaSO();
            auto.GenerateSoundMetaSO();
			auto.GenerateDropTableMetaSO();

		}
        [MenuItem("MyTools/SetMonsterAddress")]
        private static void SetMonsterAddress()
        {
            //몬스터 Prefab들을 Addressable로 등록하는 과정
            AutoAddressable auto = new AutoAddressable();
            auto.Init();
            auto.LoadGuIDFromUnity("t:Prefab", new[] { ConstPath.MONSTER_PREFEB_PATH });
            auto.ReadXlsxFile(ConstPath.MONSTER_EXCEL_PATH);
            auto.SettingAddressable(MONSTER_STRING);
        }

        [MenuItem("MyTools/SetSoundAddress")]
        private static void SetSoundAddress()
        {
            //몬스터 Prefab들을 Addressable로 등록하는 과정
            AutoAddressable auto = new AutoAddressable();
            auto.Init();
            auto.LoadGuIDFromUnity("t: AudioClip", new[] { ConstPath.SFX_AUDIOCLIP_PATH });
            auto.ReadXlsxFile(ConstPath.SFX_EXCEL_PATH);
            auto.SettingAddressable(SFX_STRING);
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

            ReadXlsxFile(ConstPath.VFX_EXCEL_PATH);
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, $"eVFXType"));

            ReadXlsxFile(ConstPath.MONSTER_EXCEL_PATH);
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, $"eMonsterType"));

            //Todo SFX
            ReadXlsxFile(ConstPath.SFX_EXCEL_PATH);
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, $"eSFXType"));
            GenerateEnumFile(_ReadFromXlsx);
            GenerateStageEnumFile();
        }

        private void GenerateStageEnumFile()
        {
            string FilePath = Path.Combine(Application.dataPath, ConstPath.STAGE_EXCEL_PATH);

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

            string enumPath = Path.Combine(Application.dataPath, ConstPath.STAGE_ENUM_PATH);
            WriteToFIle(enumPath, sb);
        }

        private void GenerateStageMetaSO()
        {
            string FilePath = Path.Combine(Application.dataPath, ConstPath.STAGE_EXCEL_PATH);

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

				CreateMetaSOHeader(sb);
                OpenBrace(sb);

				sb.Append($"[CreateAssetMenu(fileName = \"StageMetaDataSO\", menuName = \"ScriptableObjects/StageMetaDataSO\")]");
                sb.Append($"public class StageMetaDataSO : ScriptableObject\n");
				OpenBrace(sb);
                CreateStageInfo_Struct(sb);
				sb.Append($"Dictionary<eStage, List<StageInfo_v>> _dic;\n");
                sb.Append($"public void Init()");
				OpenBrace(sb);
				sb.Append($"_dic = new Dictionary<eStage, List<StageInfo_v>>();\n");
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    DataRow dataRow = table.Rows[row];
                    int stage = Convert.ToInt32(dataRow[$"Stage"]);
                    string fileName = dataRow[$"MonsterName"].ToString();
                    int wave = Convert.ToInt32(dataRow[$"Wave"]);
                    int count = Convert.ToInt32(dataRow[$"Count"]);
                    int key = stage << 16 | wave;

                    sb.Append($"if(!_dic.ContainsKey(eStage.Stage{stage}_{wave})){{\n");
                    sb.Append($"List<StageInfo_v> list = new List<StageInfo_v>();\n");
                    sb.Append($"StageInfo_v info = new StageInfo_v(eMonsterType.{fileName}, {count});\n");
                    sb.Append($"list.Add(info);\n");
                    sb.Append($"_dic.Add(eStage.Stage{stage}_{wave},list);\n");
                    sb.Append($"}}\n else{{\n");
                    sb.Append($"StageInfo_v info = new StageInfo_v(eMonsterType.{fileName}, {count});");
                    sb.Append($"_dic[eStage.Stage{stage}_{wave}].Add(info);");
                    sb.Append($"}}\n");
                }
				CloseBrace(sb);
				CreateTryStageInfo(sb);
				//namespace,function,class 괄호
				CloseBrace(sb);
				CloseBrace(sb);
			}
            fs.Close();

            string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_STAGEMETA_PATH);
            WriteToFIle(storePath, sb);
        }

        private void GenerateMonsterMetaSO()
        {
            string FilePath = Path.Combine(Application.dataPath, ConstPath.MONSTER_EXCEL_PATH);
            FileStream fstream = File.Open(FilePath, FileMode.Open, FileAccess.Read);

            IExcelDataReader reader = ExcelReaderFactory.CreateReader(fstream);
            StringBuilder sb = new StringBuilder();
            //Header제외 옵션
            var conf = new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            };
            DataSet result = reader.AsDataSet(conf);

			CreateMetaSOHeader(sb);
            OpenBrace(sb);
			sb.Append($"using Scripts.Monster;\n");
            sb.Append($"[CreateAssetMenu(fileName = \"MonsterMetaDataSO\", menuName = \"ScriptableObjects/MonsterMetaDataSO\")]");
            sb.Append($"public class MonsterMetaSO : ScriptableObject");
            OpenBrace(sb);
            sb.Append($"Dictionary<eMonsterType, List<eVFXType>> _dic;\n");
            sb.Append($"Dictionary<eMonsterType, MonsterInfo> _mInfodic;\n");
            sb.Append($"public void Init()");
			OpenBrace(sb);
			sb.Append($"_dic = new Dictionary<eMonsterType, List<eVFXType>>();\n");
            sb.Append($"_mInfodic = new Dictionary<eMonsterType, MonsterInfo>();\n");
            var tables = result.Tables;
            for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
            {
                DataTable sheet = tables[sheetIndex];
                int ArrayLength = sheet.Rows.Count;         
                for (int row = 0; row < sheet.Rows.Count; row++)
                {
                    sb.Append($"List<eVFXType> list_{row} = new List<eVFXType>();\n");
                    DataRow data = sheet.Rows[row];
                    string name = data["fileName"].ToString();

					string _monName = data["Name"].ToString();
					int hp = Convert.ToInt32(data["Hp"]);
					int atk = Convert.ToInt32(data["Atk"]);
					int exp = Convert.ToInt32(data["Atk"]);

					double movespeed = Convert.ToDouble(data["MoveSpeed"]);
					double atkspeed = Convert.ToDouble(data["AttackSpeed"]);

					long dropTableNum = Convert.ToInt64(data["DropTable"]);

					ulong maskedId = Convert.ToUInt64(data["MaskedId"]);

                    string vfx = data["VFX"].ToString();
                    string[] vfxs = vfx.Split(new char[] { ',', ' ' });

                    for (int k = 0; k < vfxs.Length; k++)
                    {
                        sb.Append($"list_{row}.Add(eVFXType.{vfxs[k]});\n");
                    }
                    
                    //SFX도 지원
                    sb.Append($"_dic.Add(eMonsterType.{name},list_{row});\n");

                    sb.Append($"MonsterInfo monInfo_{name} = new MonsterInfo(\"{_monName}\",{exp},{hp},{atk}, {movespeed},{atkspeed},{dropTableNum});\n");
                    sb.Append($"_mInfodic.Add(eMonsterType.{name},monInfo_{name});\n");
                }
            }
            CloseBrace(sb);
            CreateTryGetVFXList(sb);
            CreateTryGetMonsterInfo(sb);
			//
			CloseBrace(sb);
			CloseBrace(sb);
			//AssetDatabase.StopAssetEditing();
			reader.Close();
            fstream.Close();

            string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_MONSTERMETA_PATH);
            WriteToFIle(storePath, sb);
        }

        private void GenerateSoundMetaSO()
        {
            string FilePath = Path.Combine(Application.dataPath, ConstPath.SFX_EXCEL_PATH);
            FileStream fstream = File.Open(FilePath, FileMode.Open, FileAccess.Read);

            IExcelDataReader reader = ExcelReaderFactory.CreateReader(fstream);
            StringBuilder sb = new StringBuilder();
            //Header제외 옵션
            var conf = new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            };
            DataSet result = reader.AsDataSet(conf);

			CreateMetaSOHeader(sb);
            OpenBrace(sb);

			sb.Append($"[CreateAssetMenu(fileName = \"SoundMetaDataSO\", menuName = \"ScriptableObjects/SoundMetaDataSO\")]");
            sb.Append($"public class SoundMetaSO : ScriptableObject");
			OpenBrace(sb);
			sb.Append($"Dictionary<eSceneType, List<eSFXType>> _dic;\n");
            sb.Append($"public void Init()");
			OpenBrace(sb);
			sb.Append($"_dic = new Dictionary<eSceneType, List<eSFXType>>();\n");
            var tables = result.Tables;
            for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
            {
                DataTable sheet = tables[sheetIndex];
                int ArrayLength = sheet.Rows.Count;


                for (int row = 0; row < sheet.Rows.Count; row++)
                {

                    DataRow data = sheet.Rows[row];
                    string name = data["fileName"].ToString();
                    ulong maskedId = Convert.ToUInt64(data["MaskedId"]);

                    string Scene = data["Scene"].ToString();
                    sb.Append($"if(_dic.ContainsKey(eSceneType.{Scene}))");
                    OpenBrace(sb);
                    sb.Append($"_dic[eSceneType.{Scene}].Add(eSFXType.{name});");
					CloseBrace(sb);
                    sb.Append($"else");
                    OpenBrace(sb);
                        sb.Append($"List<eSFXType> list = new List<eSFXType>();\n");
                        sb.Append($"list.Add(eSFXType.{name});\n");
                        sb.Append($"_dic.Add(eSceneType.{Scene},list);\n");
                    CloseBrace(sb);
                }
            }
			CloseBrace(sb);
            CreateTryGetSFX(sb);
			CloseBrace(sb);
			CloseBrace(sb);
			//AssetDatabase.StopAssetEditing();
			reader.Close();
            fstream.Close();

            string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_SFX_PATH);
            WriteToFIle(storePath, sb);
        }

        private void GenerateDropTableMetaSO()
        {
			string FilePath = Path.Combine(Application.dataPath, ConstPath.DROPTABLE_EXCEL_PATH);
			FileStream fstream = File.Open(FilePath, FileMode.Open, FileAccess.Read);

			IExcelDataReader reader = ExcelReaderFactory.CreateReader(fstream);
			StringBuilder sb = new StringBuilder();
			//Header제외 옵션
			var conf = new ExcelDataSetConfiguration
			{
				ConfigureDataTable = _ => new ExcelDataTableConfiguration
				{
					UseHeaderRow = true
				}
			};
			DataSet result = reader.AsDataSet(conf);

            CreateMetaSOHeader(sb);
            OpenBrace(sb);
			CreateDropTable_DropTableInfo_Struct(sb);

			sb.Append($"[CreateAssetMenu(fileName = \"DropTableMetaSO\", menuName = \"ScriptableObjects/DropTableMetaSO\")]");
            sb.Append($"public class DropTableMetaSO : ScriptableObject");
            OpenBrace(sb);
			sb.Append($"Dictionary<long, DropInfo> _dic;\n");
			sb.Append($"public void Init()");
            OpenBrace(sb);
			sb.Append($"_dic = new Dictionary<long, DropInfo>();\n");

			var tables = result.Tables;
			for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
			{
				DataTable sheet = tables[sheetIndex];
				int ArrayLength = sheet.Rows.Count;


				for (int row = 0; row < sheet.Rows.Count; row++)
				{

					DataRow data = sheet.Rows[row];
					long Id = Convert.ToInt64(data["Id"]);
					int gold = Convert.ToInt32(data["Gold"]);
					int AncientCoin = Convert.ToInt32(data["AncientCoin"]);

                    sb.Append($"_dic.Add({Id},new DropInfo({gold},{AncientCoin}));\n");
				}
			}
            CloseBrace(sb);
            
            CreateTryGetDropInfo(sb);

			CloseBrace(sb);
			CloseBrace(sb);
			//AssetDatabase.StopAssetEditing();
			reader.Close();
			fstream.Close();

			string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_DROPTABLE_META_PATH);
			WriteToFIle(storePath, sb);
		}

        private void GenerateEnumFile(List<ReadFromXlsx> _ReadFromXlsx)
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

            string enumPath = Path.Combine(Application.dataPath, ConstPath.GENERATE_ENUM_PATH);
            string helperPath = Path.Combine(Application.dataPath, ConstPath.GENERATE_ENUMHELPER_PATH);

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

        private void CreateMetaSOHeader(StringBuilder sb)
        {
			sb.Append($"using UnityEngine;\n");
			sb.Append($"using System.Collections.Generic;\n");
			sb.Append($"namespace Scripts.Core.SO\n");
		}
        private void CreateTryGetVFXList(StringBuilder sb)
        {
			sb.Append($"public bool TryGetVFXList(eMonsterType type, out List<eVFXType> vfxDatas){{\n");
			sb.Append($"List<eVFXType> ret;\n");
			sb.Append($"if (_dic.TryGetValue(type, out ret)){{\n");
			sb.Append($"vfxDatas = ret;\n");
			sb.Append($"return true;\n");
			sb.Append($"}}\n");
			sb.Append("vfxDatas = default;\n return false;\n");
			sb.Append($"}}\n");
		}
        private void CreateTryGetMonsterInfo(StringBuilder sb)
        {
			sb.Append($"public bool TryGetMonsterInfo(eMonsterType type, out MonsterInfo mon){{\n");
			sb.Append($"MonsterInfo ret;\n");
			sb.Append($"if (_mInfodic.TryGetValue(type, out ret)){{\n");
			sb.Append($"mon = ret;\n");
			sb.Append($"return true;\n");
			sb.Append($"}}\n");
			sb.Append($"mon = default;\n return false;\n");
			sb.Append($"}}\n");
		}

        private void CreateTryStageInfo(StringBuilder sb)
        {
			sb.Append($"public bool TryGetStageInfo(eStage key, out List<StageInfo_v> stageList){{\n");
			sb.Append($"List<StageInfo_v> ret;");
			sb.Append($"if(_dic.TryGetValue(key, out ret)) {{\n");
			sb.Append($"stageList = ret;\n return true;\n");
			sb.Append($"}}\n");
			sb.Append($"stageList = default;\n return false;\n");
			sb.Append($"}}\n");
		}

        private void CreateTryGetDropInfo(StringBuilder sb)
        {
            sb.Append($"public bool TryGetDropInfo(long id, out DropInfo info){{\n");
			sb.Append($"DropInfo ret;\n");
            sb.Append($"if(_dic.TryGetValue(id, out ret)){{\n");
            sb.Append($"info = ret;\n return true;\n");
			sb.Append($"}}\n");
            sb.Append($"info = default;\n return false;\n");
			sb.Append($"}}\n");
		}
        private void CreateTryGetSFX(StringBuilder sb)
        {
			sb.Append($"public bool TryGetSFXList(eSceneType scene, out List<eSFXType> sfxList)");
			OpenBrace(sb);
			sb.Append($"List<eSFXType> ret;\n");
			sb.Append($"if(_dic.TryGetValue(scene, out ret))");
			OpenBrace(sb);
			sb.Append($"sfxList = ret;\n return true;\n");
			CloseBrace(sb);
			sb.Append($"sfxList = default;\n return false;\n");
			CloseBrace(sb);
		}
        private void CreateDropTable_DropTableInfo_Struct(StringBuilder sb)
        {
			sb.Append($"public struct DropInfo{{\n");
			sb.Append($"public DropInfo(int gold, int ancientCoin){{\n");
			sb.Append($"_incomeGold = gold;\n");
			sb.Append($"_incomeAncientCoin = ancientCoin;\n");
			sb.Append($"}}\n");
			sb.Append($"public int _incomeGold;\n");
			sb.Append($"public int _incomeAncientCoin;\n");
			sb.Append($"}}\n");
		}

        private void CreateStageInfo_Struct(StringBuilder sb)
        {
			sb.Append($"public struct StageInfo_v");
			OpenBrace(sb);
			sb.Append($"public StageInfo_v(eMonsterType type, int count)");
			OpenBrace(sb);

			sb.Append($"_type = type; _count = count;\n");
			CloseBrace(sb);
			sb.Append($"public eMonsterType _type;\n");
			sb.Append($"public int _count;\n");
			CloseBrace(sb);
		}

        private void OpenBrace(StringBuilder sb)
        {
            sb.Append(OPEN_BRACE);
        }
        private void CloseBrace(StringBuilder sb)
        {
            sb.Append(CLOSE_BRACE);
        }
    }
}

