using ExcelDataReader;
using Scripts.Core;
using Scripts.Core.Utils;
using System;
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

	// ���� ���Ͽ��� �о�� �����͸� �������
	// �����̸� -> GUID -> ������ Ž�� -> addressable ID �ڵ����
	// ��������� Addressable ID�� �����ؾ���. 
	struct stageInfo
	{
        public stageInfo(string name, int count)
        {
            _monName = name;
            _count = count;
		}
		public string _monName;
		public int _count;
	};

	public class AutoAddressable
    {
		const string OPEN_BRACE = "{\n";
		const string CLOSE_BRACE = "}\n";
        const string VFX_STRING = "VFX";
        const string MONSTER_STRING = "Monster";
        const string SFX_STRING = "SFX";
        const long stageIDMask = 0x0000000200000000;
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
            auto.GenerateMonsterMetaSO();
			auto.GenerateMonsterInfoSO();
			auto.GenerateDropTableMetaSO();
            auto.GenerateSceneVFXMetaSO();
			auto.GenerateSceneSFXMetaSO();
		}
        [MenuItem("MyTools/SetMonsterAddress")]
        private static void SetMonsterAddress()
        {
            //���� Prefab���� Addressable�� ����ϴ� ����
            AutoAddressable auto = new AutoAddressable();
            auto.Init();
            auto.LoadGuIDFromUnity("t:Prefab", new[] { ConstPath.MONSTER_PREFEB_PATH });
            auto.ReadXlsxFile(ConstPath.MONSTER_EXCEL_PATH);
            auto.SettingAddressable(MONSTER_STRING);
        }

        [MenuItem("MyTools/SetSoundAddress")]
        private static void SetSoundAddress()
        {
            //���� Prefab���� Addressable�� ����ϴ� ����
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
            //VFX,SFX,MONSTER�� ID�� ENUM�� �ڵ������ϴ� �ڵ�.
            List<ReadFromXlsx> _ReadFromXlsx = new List<ReadFromXlsx>();

            ReadXlsxFile(ConstPath.VFX_EXCEL_PATH);
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, $"eVFXType"));

            ReadXlsxFile(ConstPath.MONSTER_EXCEL_PATH);
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, $"eMonsterType"));

            //Todo SFX
            ReadXlsxFile(ConstPath.SFX_EXCEL_PATH);
            _ReadFromXlsx.Add(new ReadFromXlsx(AssetDatas, $"eSFXType"));
            GenerateEnumFile(_ReadFromXlsx);
            GenerateDropTableEnumFile();
		}

        private void GenerateStageEnumFile()
        {
            string FilePath = Path.Combine(Application.dataPath, ConstPath.STAGE_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.STAGE_ENUM_PATH);
            StringBuilder sb;
            ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);
            reader.ReadExcelFile(ReadStageLogic_for_CreateEnum, out sb);
            reader.WriteToFile(sb);
        }
		private void GenerateDropTableEnumFile()
		{
			string FilePath = Path.Combine(Application.dataPath, ConstPath.DROPTABLE_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.DROPTABLE_ENUM_PATH);
			ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);
			StringBuilder sb;
			reader.ReadExcelFile(ReadDropTableLogic_for_CreateEnum, out sb);
			reader.WriteToFile(sb);
		}
        private void GenerateStageMetaSO()
        {
            string FilePath = Path.Combine(Application.dataPath, ConstPath.STAGE_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_STAGEMETA_PATH);
            ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);
			StringBuilder sb;
			reader.ReadExcelFile(ReadStageLogic_for_CreateMetaSO, out sb);
			reader.WriteToFile(sb);
        }
        private void GenerateMonsterMetaSO()
        {
            string FilePath = Path.Combine(Application.dataPath, ConstPath.MONSTER_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_MONSTERMETA_PATH);
			ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);

			StringBuilder sb;
			reader.ReadExcelFile(ReadMonsterLogic_for_CreateMonsterMetaSO, out sb);
			reader.WriteToFile(sb);
        }

		private void GenerateMonsterInfoSO()
		{
			string FilePath = Path.Combine(Application.dataPath, ConstPath.MONSTER_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_MONSTERINFO_PATH);
			ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);

			StringBuilder sb;
			reader.ReadExcelFile(ReadMonsterLogic_for_CreateMonsterInfoSO, out sb);
			reader.WriteToFile(sb);
		}

        private void GenerateDropTableMetaSO()
        {
			string FilePath = Path.Combine(Application.dataPath, ConstPath.DROPTABLE_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_DROPTABLE_META_PATH);
			ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);
			StringBuilder sb;
			reader.ReadExcelFile(ReadDropTableLogic_for_CreateDropTableMetaSO, out sb);
			reader.WriteToFile(sb);
		}
		private void GenerateSceneVFXMetaSO()
		{
			//VFX dic����
			string FilePath = Path.Combine(Application.dataPath, ConstPath.VFX_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_SCENE_VFX_META_PATH);
			StringBuilder sb;
			ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);
			reader.ReadExcelFile(ReadLogic_for_SceneMetaVFXSO, out sb);
			reader.WriteToFile(sb);
		}
		private void GenerateSceneSFXMetaSO()
		{
			string FilePath = Path.Combine(Application.dataPath, ConstPath.SFX_EXCEL_PATH);
			string storePath = Path.Combine(Application.dataPath, ConstPath.GENERATE_SCENE_SFX_META_PATH);
			StringBuilder sb;

			ExcelFileReader reader = new ExcelFileReader(FilePath, storePath);
			reader.ReadExcelFile(ReadLogic_for_SceneMetaSFXSO, out sb);
			reader.WriteToFile(sb);
		}
		private void ReadLogic_for_SceneMetaVFXSO(StringBuilder sb, DataSet data)
		{
			sb.Append($"using UnityEngine;\n");
			sb.Append($"using System.Collections.Generic;\n");
			sb.Append($"namespace Scripts.Core.SO");
			OpenBrace(sb);
			sb.Append($"[CreateAssetMenu(fileName = \"SceneVFXMetaSO\", menuName = \"ScriptableObjects/SceneVFXMetaSO\")]");
			sb.Append($"public class SceneVFXMetaSO : ScriptableObject");
			OpenBrace(sb);
			sb.Append($"Dictionary<eSceneType, List<eVFXType>> _dic;\n");
			sb.Append($"public void Init()");
			OpenBrace(sb);
			sb.Append($"_dic = new Dictionary<eSceneType, List<eVFXType>>();\n");

			//�� Ÿ�� - VFX[]�� ����
			Dictionary<string, List<string>> _dics = new Dictionary<string, List<string>>();

			var tables = data.Tables;
			for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
			{
				DataTable sheet = tables[sheetIndex];

				for (int row = 0; row < sheet.Rows.Count; row++)
				{
					DataRow rowData = sheet.Rows[row];

					string SceneName = rowData["Scene"].ToString();
					string eVFXTypeName = rowData["fileName"].ToString();

					if (SceneName == "exclusive")
					{
						continue;
					}
					if (_dics.ContainsKey(SceneName))
					{
						_dics[SceneName].Add(eVFXTypeName);
					}
					else
					{
						List<string> list = new List<string>();
						list.Add(eVFXTypeName);
						_dics.Add(SceneName, list);
					}
				}
			}
			//�� �̸� - eVFXList�ϼ� 
			foreach (var Item in _dics)
			{
				List<string> value = Item.Value;
				OpenBrace(sb);
				sb.Append($"List<eVFXType> list = new List<eVFXType>();\n");
				foreach (var eVFXTypeName in value)
				{
					sb.Append($"list.Add(eVFXType.{eVFXTypeName});\n");
				}
				sb.Append($"_dic.Add(eSceneType.{Item.Key}, list);\n");
				CloseBrace(sb);
			}

			CloseBrace(sb);
			sb.Append($"public bool TryGetVFXTypeList(eSceneType type, out List<eVFXType> outList)");
			OpenBrace(sb);
			sb.Append($"List<eVFXType> ret;\n");
			sb.Append($"if(_dic.TryGetValue(type, out ret))");
			OpenBrace(sb);
			sb.Append($"outList = ret;\n");
			sb.Append($"return true;\n");
			CloseBrace(sb);
			sb.Append($"outList = default;\n");
			sb.Append($"return false;\n");
			CloseBrace(sb);
			CloseBrace(sb);
			CloseBrace(sb);
		}
		private void ReadLogic_for_SceneMetaSFXSO(StringBuilder sb, DataSet data)
		{
			sb.Append($"using UnityEngine;\n");
			sb.Append($"using System.Collections.Generic;\n");
			sb.Append($"namespace Scripts.Core.SO");
			OpenBrace(sb);
			sb.Append($"[CreateAssetMenu(fileName = \"SceneSFXMetaSO\", menuName = \"ScriptableObjects/SceneSFXMetaSO\")]");
			sb.Append($"public class SceneSFXMetaSO : ScriptableObject");
			OpenBrace(sb);
			sb.Append($"Dictionary<eSceneType, List<eSFXType>> _dic;\n");
			sb.Append($"public void Init()");
			OpenBrace(sb);
			sb.Append($"_dic = new Dictionary<eSceneType, List<eSFXType>>();\n");

			//�� Ÿ�� - VFX[]�� ����
			Dictionary<string, List<string>> _dics = new Dictionary<string, List<string>>();

			var tables = data.Tables;
			for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
			{
				DataTable sheet = tables[sheetIndex];

				for (int row = 0; row < sheet.Rows.Count; row++)
				{
					DataRow rowData = sheet.Rows[row];

					string SceneName = rowData["Scene"].ToString();
					string eSFXTypeName = rowData["fileName"].ToString();

					if (SceneName == "exclusive")
					{
						continue;
					}
					if (_dics.ContainsKey(SceneName))
					{
						_dics[SceneName].Add(eSFXTypeName);
					}
					else
					{
						List<string> list = new List<string>();
						list.Add(eSFXTypeName);
						_dics.Add(SceneName, list);
					}
				}
			}
			//�� �̸� - eVFXList�ϼ� 
			foreach (var Item in _dics)
			{
				List<string> value = Item.Value;
				OpenBrace(sb);
				sb.Append($"List<eSFXType> list = new List<eSFXType>();\n");
				foreach (var eSFXTypeName in value)
				{
					sb.Append($"list.Add(eSFXType.{eSFXTypeName});\n");
				}
				sb.Append($"_dic.Add(eSceneType.{Item.Key}, list);\n");
				CloseBrace(sb);
			}

			CloseBrace(sb);
			sb.Append($"public bool TryGetSFXTypeList(eSceneType type, out List<eSFXType> outList)");
			OpenBrace(sb);
			sb.Append($"List<eSFXType> ret;\n");
			sb.Append($"if(_dic.TryGetValue(type, out ret))");
			OpenBrace(sb);
			sb.Append($"outList = ret;\n");
			sb.Append($"return true;\n");
			CloseBrace(sb);
			sb.Append($"outList = default;\n");
			sb.Append($"return false;\n");
			CloseBrace(sb);
			CloseBrace(sb);
			CloseBrace(sb);
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
        //���� ������ �о�;���.
        private void ReadXlsxFile(string path)
        {
            string FilePath = Path.Combine(Application.dataPath, path);
            FileStream fstream = File.Open(FilePath, FileMode.Open, FileAccess.Read);
            IExcelDataReader reader = ExcelReaderFactory.CreateReader(fstream);

            //Header���� �ɼ�
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
            //Addressable ����
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null);
            }
            //AssetDatabase.StartAssetEditing();
            //���鼭, �ش� fileName�� GUID ��ȸ.
            for (int i = 0; i < AssetDatas.Length; i++)
            {
                for (int j = 0; j < AssetDatas[i].Length; j++)
                {
                    bool flag = FileNameToGuID.TryGetValue(AssetDatas[i][j].fileName, out string guid);
                    if (flag == false)
                    {
                        Debug.Log("FileName is not found");
                    }
                    //�̰� ������ Addressable�������ִ� API
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);

                    if (entry != null)
                    {
                        entry.labels.Add(groupName);
                        //entry.address = maskedId.ToString();
                        entry.address = AssetDatas[i][j].fileName;
                        CustomLogger.Log($"[SetAddressable] filename: {AssetDatas[i][j].fileName} -> SetId: {AssetDatas[i][j]._MaskedId}");
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
            //���������� ���� �κ�
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

		private void ReadStageLogic_for_CreateEnum(StringBuilder sb, DataSet data)
		{
			//�ߺ��˻�
			HashSet<long> duplicateKey = new HashSet<long>();
			HashSet<long> duplicateStage = new HashSet<long>();

			var ExcelTable = data.Tables;

			//Sheet��ȸ
			for (int i = 0; i < ExcelTable.Count; i++)
			{
				DataTable table = ExcelTable[i];
				sb.Append($"namespace Scripts.Core {{\n");
				sb.Append($"public enum eStage : long\n{{");
				for (int row = 0; row < table.Rows.Count; row++)
				{
					DataRow dataRow = table.Rows[row];
					short stage = Convert.ToInt16(dataRow["Stage"]);
					short wave = Convert.ToInt16(dataRow["Wave"]);

					int maskedStage = stage << 16;
					long key = maskedStage | (int)wave;

					long maskedStageKey = maskedStage;
					maskedStageKey |= stageIDMask;
					key |= stageIDMask;

					if (duplicateStage.Add(maskedStage) == true)
					{
						sb.Append($"Stage{stage} = {maskedStageKey},\n");
					}
					if (duplicateKey.Add(key) == true)
					{
						sb.Append($"Stage{stage}_{wave} = {key},\n");
					}
				}
				sb.Append($"}}\n}}");
			}
		}
		private void ReadDropTableLogic_for_CreateEnum(StringBuilder sb, DataSet data)
		{
			sb.Append($"namespace Scripts.Core");
			OpenBrace(sb);
			sb.Append($"public enum eDropTable : long");
			OpenBrace(sb);

			var tables = data.Tables;
			for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
			{
				DataTable sheet = tables[sheetIndex];
				int ArrayLength = sheet.Rows.Count;
				for (int row = 0; row < sheet.Rows.Count; row++)
				{
					DataRow rowData = sheet.Rows[row];
					long Id = Convert.ToInt64(rowData["Id"]);
					string tableName = rowData["TableName"].ToString();
					sb.Append($"{tableName} = {Id},\n");
				}
			}
			CloseBrace(sb);
			CloseBrace(sb);
		}
		private void ReadStageLogic_for_CreateMetaSO(StringBuilder sb, DataSet data)
		{
			//�ߺ��˻�
			Dictionary<long, HashSet<string>> _monDics = new Dictionary<long, HashSet<string>>();
			Dictionary<long, List<stageInfo>> _stageDics = new Dictionary<long, List<stageInfo>>();

			var ExcelTable = data.Tables;
			//Sheet��ȸ
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
				sb.Append($"Dictionary<eStage, List<eMonsterType>> _monDic;\n");
				sb.Append($"public void Init()");
				OpenBrace(sb);
				sb.Append($"_dic = new Dictionary<eStage, List<StageInfo_v>>();\n");
				sb.Append($"_monDic = new Dictionary<eStage, List<eMonsterType>>();\n");
				for (int row = 0; row < table.Rows.Count; row++)
				{
					DataRow dataRow = table.Rows[row];
					long key;

					short stage = Convert.ToInt16(dataRow[$"Stage"]);
					string fileName = dataRow[$"MonsterName"].ToString();

					short wave = Convert.ToInt16(dataRow[$"Wave"]);
					int count = Convert.ToInt32(dataRow[$"Count"]);

					int tmp = stage << 16;
					key = tmp | (int)wave;
					key |= stageIDMask;

					if (!_stageDics.ContainsKey(key))
					{
						List<stageInfo> list = new List<stageInfo>();
						list.Add(new stageInfo(fileName, count));
						_stageDics.Add(key, list);
					}
					else
					{
						_stageDics[key].Add(new stageInfo(fileName, count));
					}
					if (!_monDics.ContainsKey(stage))
					{
						HashSet<string> strList = new HashSet<string>();
						strList.Add(fileName);
						_monDics.Add(stage, strList);
					}
					else
					{
						_monDics[stage].Add(fileName);
					}
				}

				foreach (var sinfo in _stageDics)
				{
					List<stageInfo> value = sinfo.Value;
					long key = sinfo.Key;
					key &= ~(stageIDMask);

					OpenBrace(sb);
					sb.Append($"List<StageInfo_v> list = new List<StageInfo_v>();\n");
					sb.Append($"StageInfo_v info;\n");
					for (int j = 0; j < value.Count; j++)
					{
						sb.Append($"info = new StageInfo_v(eMonsterType.{value[j]._monName}, {value[j]._count});\n");
						sb.Append($"list.Add(info);\n");
					}
					int stage = (int)((key & (0x00000000FFFF0000)) >> 16);
					int wave = (int)(key & (0x000000000000FFFF));
					sb.Append($"_dic.Add(eStage.Stage{stage}_{wave},list);\n");
					CloseBrace(sb);
				}
				foreach (var Item in _monDics)
				{
					HashSet<string> strList = Item.Value;
					sb.Append($"List<eMonsterType> list_{Item.Key} = new List<eMonsterType>();\n");
					foreach (var str in strList)
					{
						sb.Append($"list_{Item.Key}.Add(eMonsterType.{str});\n");
					}
					sb.Append($"_monDic.Add(eStage.Stage{Item.Key}, list_{Item.Key});\n");
				}

				CloseBrace(sb);
				CreateTryStageInfo(sb);
				CreateTryGetStageMonsterInfo(sb);
				//namespace,function,class ��ȣ
				CloseBrace(sb);
				CloseBrace(sb);
			}
		}

		private void ReadMonsterLogic_for_CreateMonsterInfoSO(StringBuilder sb, DataSet data)
		{
			CreateMetaSOHeader(sb);
			OpenBrace(sb);
			sb.Append($"using Scripts.Monster;\n");
			sb.Append($"[CreateAssetMenu(fileName = \"MonsterInfoSO\", menuName = \"ScriptableObjects/Monster/MonsterInfoSO\")]");
			sb.Append($"public class MonsterInfoSO : ScriptableObject");
			OpenBrace(sb);
			sb.Append($"Dictionary<eMonsterType, MonsterInfo> _mInfodic;\n");
			sb.Append($"public void Init()");
			OpenBrace(sb);
			sb.Append($"_mInfodic = new Dictionary<eMonsterType, MonsterInfo>();\n");

			var tables = data.Tables;
			for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
			{
				DataTable sheet = tables[sheetIndex];
				int ArrayLength = sheet.Rows.Count;
				for (int row = 0; row < sheet.Rows.Count; row++)
				{
					DataRow rowData = sheet.Rows[row];
					string name = rowData["fileName"].ToString();

					string _monName = rowData["Name"].ToString();
					int hp = Convert.ToInt32(rowData["Hp"]);
					int atk = Convert.ToInt32(rowData["Atk"]);
					int exp = Convert.ToInt32(rowData["Atk"]);

					double movespeed = Convert.ToDouble(rowData["MoveSpeed"]);
					double atkspeed = Convert.ToDouble(rowData["AttackSpeed"]);
					long dropTableNum = Convert.ToInt64(rowData["DropTable"]);

					//MonsterInfo
					sb.Append($"MonsterInfo monInfo_{name} = new MonsterInfo(\"{_monName}\",{exp},{hp},{atk}, {movespeed},{atkspeed},{dropTableNum});\n");
					sb.Append($"_mInfodic.Add(eMonsterType.{name},monInfo_{name});\n");
				}
			}
			CloseBrace(sb);
			CreateTryGetMonsterInfo(sb);
			//
			CloseBrace(sb);
			CloseBrace(sb);
		}

		private void ReadMonsterLogic_for_CreateMonsterMetaSO(StringBuilder sb, DataSet data)
		{
			CreateMetaSOHeader(sb);
			OpenBrace(sb);
			sb.Append($"using Scripts.Monster;\n");
			sb.Append($"[CreateAssetMenu(fileName = \"MonsterMetaDataSO\", menuName = \"ScriptableObjects/MonsterMetaDataSO\")]");
			sb.Append($"public class MonsterMetaSO : ScriptableObject");
			OpenBrace(sb);
			sb.Append($"Dictionary<eMonsterType, List<eVFXType>> _dic;\n");
			sb.Append($"Dictionary<eMonsterType, List<eSFXType>> _dic2;\n");
			sb.Append($"public void Init()");
			OpenBrace(sb);
			sb.Append($"_dic = new Dictionary<eMonsterType, List<eVFXType>>();\n");
			sb.Append($"_dic2 = new Dictionary<eMonsterType, List<eSFXType>>();\n");

			var tables = data.Tables;
			for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
			{
				DataTable sheet = tables[sheetIndex];
				int ArrayLength = sheet.Rows.Count;
				for (int row = 0; row < sheet.Rows.Count; row++)
				{
					DataRow rowData = sheet.Rows[row];
					string name = rowData["fileName"].ToString();

					ulong maskedId = Convert.ToUInt64(rowData["MaskedId"]);

					string vfx = rowData["VFX"].ToString();
					string sfx = rowData["SFX"].ToString();

					if (!string.IsNullOrEmpty(vfx))
					{
						sb.Append($"List<eVFXType> list_vfx{row} = new List<eVFXType>();\n");
						string[] vfxs = vfx.Split(new char[] { ',', ' ' });
						for (int k = 0; k < vfxs.Length; k++)
						{
							sb.Append($"list_vfx{row}.Add(eVFXType.{vfxs[k]});\n");
						}
						sb.Append($"_dic.Add(eMonsterType.{name},list_vfx{row});\n");
					}
					if (!string.IsNullOrEmpty(sfx))
					{
						sb.Append($"List<eSFXType> list_sfx{row} = new List<eSFXType>();\n");
						string[] sfxs = sfx.Split(new char[] { ',', ' ' });
						for (int k = 0; k < sfxs.Length; k++)
						{
							sb.Append($"list_sfx{row}.Add(eSFXType.{sfxs[k]});\n");
						}
						sb.Append($"_dic2.Add(eMonsterType.{name},list_sfx{row});\n");
					}
		
				}
			}
			CloseBrace(sb);
			CreateTryGetVFXList(sb);
			CreateTryGetSFXList(sb);
			//
			CloseBrace(sb);
			CloseBrace(sb);
			//AssetDatabase.StopAssetEditing();
		}
		private void ReadDropTableLogic_for_CreateDropTableMetaSO(StringBuilder sb, DataSet data)
		{
			CreateMetaSOHeader(sb);
			OpenBrace(sb);
			CreateDropTable_DropTableInfo_Struct(sb);

			sb.Append($"[CreateAssetMenu(fileName = \"DropTableMetaSO\", menuName = \"ScriptableObjects/DropTableMetaSO\")]");
			sb.Append($"public class DropTableMetaSO : ScriptableObject");
			OpenBrace(sb);
			sb.Append($"Dictionary<eDropTable, DropInfo> _dic;\n");
			sb.Append($"public void Init()");
			OpenBrace(sb);
			sb.Append($"_dic = new Dictionary<eDropTable, DropInfo>();\n");

			var tables = data.Tables;
			for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
			{
				DataTable sheet = tables[sheetIndex];
				int ArrayLength = sheet.Rows.Count;


				for (int row = 0; row < sheet.Rows.Count; row++)
				{

					DataRow rowData = sheet.Rows[row];
					int gold = Convert.ToInt32(rowData["Gold"]);
					int AncientCoin = Convert.ToInt32(rowData["AncientCoin"]);
					string name = rowData["TableName"].ToString();

					sb.Append($"_dic.Add(eDropTable.{name}, new DropInfo({gold},{AncientCoin}));\n");
				}
			}
			CloseBrace(sb);

			CreateTryGetDropInfo(sb);

			CloseBrace(sb);
			CloseBrace(sb);
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

        private void CreateTryGetSFXList(StringBuilder sb)
        {
			sb.Append($"public bool TryGetSFXList(eMonsterType type, out List<eSFXType> sfxDatas){{\n");
			sb.Append($"List<eSFXType> ret;\n");
			sb.Append($"if (_dic2.TryGetValue(type, out ret)){{\n");
			sb.Append($"sfxDatas = ret;\n");
			sb.Append($"return true;\n");
			sb.Append($"}}\n");
			sb.Append("sfxDatas = default;\n return false;\n");
			sb.Append($"}}\n");
		}
        private void CreateTryGetStageMonsterInfo(StringBuilder sb)
        {
			sb.Append($"public bool TryGetMonsterList(eStage type, out List<eMonsterType> monsterDatas){{\n");
			sb.Append($"List<eMonsterType> ret;\n");
			sb.Append($"if (_monDic.TryGetValue(type, out ret)){{\n");
			sb.Append($"monsterDatas = ret;\n");
			sb.Append($"return true;\n");
			sb.Append($"}}\n");
			sb.Append("monsterDatas = default;\n return false;\n");
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
            sb.Append($"public bool TryGetDropInfo(eDropTable id, out DropInfo info){{\n");
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
    class ExcelFileReader
    {
        private readonly string _filePath;
        private StringBuilder _sb;
        private readonly string _savePath;

        public ExcelFileReader(string filePath, string savePath)
        {
            _filePath = filePath;
            _savePath = savePath;
		}

        public void ReadExcelFile(Action<StringBuilder, DataSet> readLogic, out StringBuilder sb)
        {
			FileStream fs = File.Open(_filePath, FileMode.Open, FileAccess.Read);
			IExcelDataReader reader = ExcelReaderFactory.CreateReader(fs);

			var config = new ExcelDataSetConfiguration
			{
				ConfigureDataTable = (reader) => new ExcelDataTableConfiguration
				{
					UseHeaderRow = true
				}
			};
            //���� ������ ���
			DataSet data = reader.AsDataSet(config); 
            _sb = new StringBuilder();
            readLogic.Invoke(_sb, data);
			fs.Close();

            sb = _sb;
            return;
		}

        public void WriteToFile(StringBuilder sb)
        {
			FileStream fs = File.Open(_savePath, FileMode.Create, FileAccess.ReadWrite);
			StreamWriter sw = new StreamWriter(fs, Encoding.Unicode, 4096);

			char[] buffer = new char[2048];
			//���������� ���� �κ�
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

