using ExcelDataReader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;


namespace Scripts.Core.Parser
{

    // 엑셀 파일에서 읽어온 데이터를 기반으로
    // 파일이름 -> GUID -> 프리펩 탐색 -> addressable ID 자동등록
    // 프리펩들을 Addressable ID를 수정해야함. 

    public class AutoAddressable : MonoBehaviour
    {
        class VFX_DATA
        {
            public VFX_DATA(string name, ulong maskedID)
            {
                fileName = name;
                _MaskedId = maskedID;
            }
            public string fileName;
            public ulong _MaskedId;
        }

        private VFX_DATA[][] vfxDatas;
        private Dictionary<string, string> FileNameToGuID;
        private int excelSheetCount;


        private void Awake()
        {
            FileNameToGuID = new Dictionary<string, string>();
        }

        private void Start()
        {



        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                LoadGuIDFromUnity();
                ReadXlsxFile();
                SettingAddressable();
            }
        }
        //엑셀 파일을 읽어와야함.
        private void ReadXlsxFile()
        {
            string FilePath = Path.Combine(Application.dataPath, @"Scripts\Core\Parser\vfx.xlsx");
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

            //Sheet가 1개라고 가정. 단일 시트
            var tables = result.Tables;
            vfxDatas = new VFX_DATA[tables.Count][];
            for (int sheetIndex = 0; sheetIndex < tables.Count; sheetIndex++)
            {
                DataTable sheet = tables[sheetIndex];
                int ArrayLength = sheet.Rows.Count;

                int index = 0;
                vfxDatas[sheetIndex] = new VFX_DATA[ArrayLength];
                for (int row = 0; row < sheet.Rows.Count; row++)
                {
                    DataRow data = sheet.Rows[row];
                    string name = data["fileName"].ToString();
                    ulong maskedId = Convert.ToUInt64(data["MaskedId"]);

                    VFX_DATA vfxData = new VFX_DATA(name, maskedId);
                    vfxDatas[sheetIndex][index++] = vfxData;
                }
            }
            //AssetDatabase.StopAssetEditing();
            reader.Close();
            fstream.Close();
        }
        private void LoadGuIDFromUnity()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Scripts/Core/TestResource/VFX" });

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
        private void SettingAddressable()
        {
            //FileName - ID - GUID 모두 로딩된 상태
            const string groupNames = "VFX";
            //Addressable 설정
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = settings.FindGroup(groupNames);
            if (group == null)
            {
                group = settings.CreateGroup(groupNames, false, false, true, null);
                Debug.Log($"새 그룹 생성됨: VFX");
            }
            //AssetDatabase.StartAssetEditing();
            //돌면서, 해당 fileName의 GUID 조회.
            for (int i = 0; i < vfxDatas.Length; i++)
            {
                for (int j = 0; j < vfxDatas[i].Length; j++)
                {
                    ulong maskedId = vfxDatas[i][j]._MaskedId;
                    FileNameToGuID.TryGetValue(vfxDatas[i][j].fileName, out string guid);
                    //이게 실제로 Addressable설정해주는 API
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);

                    if (entry != null)
                    {
                        entry.labels.Add(groupNames);
                        entry.address = maskedId.ToString();
                        CustomLogger.Log($"[등록 성공] 파일: {vfxDatas[i][j].fileName} -> 주소: {maskedId}");
                    }
                }
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Unity 버튼으로 만들기
    }
}

