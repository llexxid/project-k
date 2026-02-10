using ExcelDataReader;
using Scripts.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEngine.Networking.UnityWebRequest;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance;
    private Dictionary<int, List<StageInfo>> _StageCache;

    private Dictionary<eMonsterType, Monster> _monsterCache;
    private Dictionary<eMonsterType, ObjectPool<Monster>> _StageMonsterPool;
    
    //Asset
    private Dictionary<int, AsyncOperationHandle<IList<Monster>>> _Handles;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Init();
            DontDestroyOnLoad(this);
            return;
        }
        Destroy(this);
        return;
    }

    //각 Stage에 어떤 몬스터가 나오는지 PreLoading
    struct StageInfo
    {
        public StageInfo(eMonsterType type, int count)
        {
            _type = type;
            _count = count;
        }
        public eMonsterType _type;
        //스폰하는 적의 수
        public int _count;
    }
    private void Init()
    {
        _StageCache = new Dictionary<int, List<StageInfo>>();
        PreLoadStageFile();
    }

    /// <summary>
    /// 스테이지가 바뀔때, Stage에 필요한 정보들을 비동기적으로 Load하는 함수입니다.
    /// </summary>
    /// <param name="stage"></param>
    /// <returns></returns>
    public AsyncOperationHandle<IList<Monster>> LoadAssets(eStage stage)
    {
        //스테이지 정보에 있는 Monster Type들 Load
        List<StageInfo> stageInfoList;

        bool IsCached;
        IsCached = _StageCache.TryGetValue((int)stage, out stageInfoList);
        if (!IsCached)
        {
            //이럴일은 없긴함.
            CustomLogger.LogWarning("NoPreLoad_StageInfo. Do Preload Stage");
            return default;
        }

        //stage정보 가져옴.
        int SpawnMonsterCount = stageInfoList.Count;
        eMonsterType[] SpawnMonsterTypes = new eMonsterType[SpawnMonsterCount];
        for (int i = 0; i < SpawnMonsterCount; i++)
        {
            SpawnMonsterTypes[i] = stageInfoList[i]._type;
        }

        LoadAssetAsync(stage, SpawnMonsterTypes);
        return _Handles[(int)stage];
    }
    /// <summary>
    /// 씬 혹은 스테이지가 바뀔때, Manager의 리소스를 정리하는 함수입니다.
    /// </summary>
    public void OnEnterScene()
    {
        Clear();
    }
    
    private void PreLoadStageFile()
    {
        //어느 스테이지에 어떤걸 스폰 시켜야하는지는 읽어와야함.
        //근데, 여기서 Excel파일을 읽기 + Parsing하는거임.
        string path = @"Scripts\Core\Parser\Stage.xlsx";
        string FilePath = Path.Combine(Application.dataPath, path);

        FileStream fstream = File.Open(FilePath, FileMode.Open, FileAccess.Read);
        IExcelDataReader reader = ExcelReaderFactory.CreateReader(fstream);

        var conf = new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        };

        /*
          stage wave MonsterName Count
        */
        DataSet result = reader.AsDataSet(conf);
        var tables = result.Tables;
        DataTable sheet = tables[0];
        int ArrayLength = sheet.Rows.Count;

        int stage;
        int wave;
        int count;
        DataRow data;
        string name;
        int key;

        for (int row = 0; row < ArrayLength; row++)
        {
            data = sheet.Rows[row];

            stage = Convert.ToInt32(data["Stage"]);
            wave = Convert.ToInt32(data["Wave"]);
            count = Convert.ToInt32(data["Count"]);
            name = data["MonsterName"].ToString();

            key = GenerateKey(stage, wave);
            eMonsterType type = eMonsterTypeHelper.Parse(name);
            if (!_StageCache.ContainsKey(key))
            {
                _StageCache.Add(key, new List<StageInfo>());
            }
            _StageCache[key].Add(new StageInfo(type, count));
        }
        fstream.Close();
    }
    private int GenerateKey(int stage, int wave)
    {
        return ((stage << 16) | wave);
    }
    private void Clear()
    {
        _monsterCache.Clear();

        _StageMonsterPool.Clear();

        foreach (var Handle in _Handles)
        {
            Handle.Value.Release();
        }
        _Handles.Clear();

    }
    private async void LoadAssetAsync(eStage groupId, eMonsterType[] id)
    {
        IList<string> keys = Array.ConvertAll(id, (id) => id.ToString());
        var Handle = Addressables.LoadAssetsAsync<Monster>(keys, (loaded) => { });
        _Handles.Add((int)groupId, Handle);

        //Stage에 있는 Monster들 생성
        var result = await Handle.Task;
        int i = 0;
        foreach (Monster mon in result)
        {
            _monsterCache.Add(id[i], mon);
            ObjectPool<Monster> monPool = new ObjectPool<Monster>();
            monPool.Init(40, mon);
            _StageMonsterPool.Add(id[i], monPool);
            i++;
        }
        return;
    }
}
