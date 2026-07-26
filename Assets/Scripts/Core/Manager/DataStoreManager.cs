using Cysharp.Threading.Tasks;
using Scripts.Core;
using Scripts.Core.SO;
using Scripts.Monster;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static Scripts.Core.SO.StageMetaDataSO;

public class DataStoreManager : MonoBehaviour
{
	//MetaData Dictionary 
	private Dictionary<eStage, List<StageInfo_v>> _stageMetaData; //어떤 스테이지 어떤 몬스터가 몇마리 나오는지 저장함.
	private Dictionary<eStage, List<eMonsterType>> _stageMonsterListData; //어떤 스테이지에 어떤 종류가 있는지 저장함. 

	//몬스터 데이터(기본 정보)
	private Dictionary<eMonsterType, MonsterInfo> _monsterData;

	//스테이지마다 필요한 vfx, sfx 정보
	private Dictionary<eSceneType, List<eVFXType>> _vfxMetaData;
	private Dictionary<eSceneType, List<eSFXType>> _sfxMetaData;

	//드랍 테이블 정보
	private Dictionary<eDropTable, DropInfo> _dropTableData;

	public static DataStoreManager Instance;
	
	private 

    void Awake()
    {
		if (Instance == null)
		{
			Instance = this;
			Init();
			DontDestroyOnLoad(gameObject);
			return;
		}

		Destroy(gameObject);
	}

	void Init()
	{

	}

	//서버를 통해 데이터를 가져옴
	private async UniTaskVoid GetMetaData()
	{ 
		
	}
}
