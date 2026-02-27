using System;
using System.Security.Cryptography;
using UnityEngine;

namespace Scripts.Core
{
    public static class ConstPath
    {
        public static readonly string VFX_EXCEL_PATH = @"Scripts\Core\Parser\vfx.xlsx";
        public static readonly string SFX_EXCEL_PATH = @"Scripts\Core\Parser\sfx.xlsx";
        public static readonly string MONSTER_EXCEL_PATH = @"Scripts\Core\Parser\Monster.xlsx";
        public static readonly string STAGE_EXCEL_PATH = @"Scripts\Core\Parser\Stage.xlsx";
        public static readonly string DROPTABLE_EXCEL_PATH = @"Scripts\Core\Parser\DropTable.xlsx";

        //PrefebPath
        public static readonly string VFX_PREFEB_PATH = $"Assets/Scripts/Core/TestResource/VFX";
        public static readonly string MONSTER_PREFEB_PATH = $"Assets/Scripts/Core/TestResource/Monster";
        public static readonly string SFX_AUDIOCLIP_PATH = $"Assets/Scripts/Core/TestResource/SFX";

        //AutoGenerate Path
        public static readonly string STAGE_ENUM_PATH = @"Scripts\Core\AutoGenEnum\StageEnum.cs";
        public static readonly string DROPTABLE_ENUM_PATH = @"Scripts\Core\AutoGenEnum\DropTableEnum.cs";
        public static readonly string GENERATE_ENUM_PATH = @"Scripts\Core\AutoGenEnum\GenerateEnum.cs";
        public static readonly string GENERATE_ENUMHELPER_PATH = @"Scripts\Core\AutoGenEnum\EnumHelper.cs";

        public static readonly string GENERATE_STAGEMETA_PATH = @"Scripts\Core\SO\StageMetaDataSO.cs";
        public static readonly string GENERATE_MONSTERMETA_PATH = @"Scripts\Core\SO\MonsterMetaDataSO.cs";
		public static readonly string GENERATE_DROPTABLE_META_PATH = @"Scripts\Core\SO\DropTableMetaSO.cs";
		public static readonly string GENERATE_SFX_PATH = @"Scripts\Core\SO\SoundMetaDataSO.cs";

		public static readonly string GENERATE_SCENE_VFX_META_PATH = @"Scripts\Core\SO\SceneVFXMetaSO.cs";
		public static readonly string GENERATE_SCENE_SFX_META_PATH = @"Scripts\Core\SO\SceneSFXMetaSO.cs";
        
    }
    public enum eSceneType
    {
        bootstrap = 0,
        title = 1,
        main = 2,
        dungeon = 3,
        SCENE_COUNT,
    }

    public enum DEFAULT_VALUE
    { 
        PoolingSize = 32,
    }

    public enum CONSTANT_VALUE
    { 
        WAVE_END = 10,

        StageMask = 0x7FFF0000,
        WaveMask = 0x0000FFFF,
    }

    public enum AssetIdMask : ulong
    {
        //StageNumber는 최상위 30bit가 모두 0이어야함.
        STAGE_UPBITMASK = 0xFFFFFFFC00000000,
        STAGE_MASK = 0x0000000200000000,

        //Monster는 최상위 31bit가 모두 0이어야함.
        MONSTER_UPBITMASK   = 0xFFFFFFFE00000000,
        MONSTER_MASK        = 0x0000000100000000,

        //SFX는 최상위 40bit가 모두 0이어야함.
        SFX_UPBITMASK   = 0xFFFFFFFFFF000000,
        SFX_MASK        = 0x0000000000800000,

        //VFX는 최상위 32bit가 모두 0이어야함.
        VFX_Pooling_UPBITMASK = 0xFFFFFFFF00000000,
        //VFX_NotPooling MASK로 Masking했을 때, PoolingMask가 나오면 Pooling. 아니면 NotPoolingMask.
        VFX_Pooling_MASK =   0x0000000080000000,
        VFX_NotPooling_MASK =   0x00000000C0000000,
    }
     
    public enum GroupId : ulong
    {
        Stage1 = 0,

    }

    public enum eMonsterAction
    { 
        Idle,
        Walk,
        Attack,
        Dead,
        Hurt,
        ACTION_END,
    }

    public enum ePlayerAction
    {
        Idle,
        Walk,
        Attack,
        Dead,
        Hurt,
        ACTION_END,
    }

	public struct UserData
	{
		public UserData(long Id, string nickname, eStage stage, int level, int coin, int ancientcoin)
		{
			_Id = Id;
			_nickname = nickname;
			_currentStage = stage;

			_level = level;
			_coin = coin;
			_ancientCoin = ancientcoin;
		}

		long _Id;
		public string _nickname;
		public eStage _currentStage;

		public int _level;
		public int _coin;//Wallet정보들은 직렬화 되서 올테니
		public int _ancientCoin;

		//Todo : Token을 저장하고 있어야함.
	}

    //PlayerData
    public struct PlayerData
    {
        public PlayerData(long id, string jobname, int enforce, ulong atk, long hp)
        {
            _Id = id;
			_jobName = jobname;
            _enforce = enforce;
			_atk = atk;
            _Hp = hp;

            _extraAtk = 0;
            _extraHp = 0;
		}

        long _Id;
        //직업이름, 직업 코드
        public string _jobName;

        //Todo : 강화기능 추가시, 추가될 데이터
        public int _enforce;

        public ulong _atk;
        public long _Hp;
		public uint _extraAtk;
		public int _extraHp;


    }

}
