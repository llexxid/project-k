using System;
using UnityEngine;

namespace Scripts.Core
{
    public static class ConstPath
    {
        public static readonly string VFX_EXCEL_PATH = @"Scripts\Core\Parser\vfx.xlsx\";
        //static readonly string sfxPath = @"Scripts\\Core\\Parser\\sfx.xlsx\";
        public static readonly string MONSTER_EXCEL_PATH = @"Scripts\Core\Parser\Monster.xlsx";
        public static readonly string STAGE_EXCEL_PATH = @"Scripts\Core\Parser\Stage.xlsx";

        //PrefebPath
        public static readonly string VFX_PREFEB_PATH = $"Assets/Scripts/Core/TestResource/VFX";
        public static readonly string MONSTER_PREFEB_PATH = $"Assets/Scripts/Core/TestResource/Monster";


        public static readonly string STAGE_ENUM_PATH = @"Scripts\Core\StageEnum.cs";
        public static readonly string GENERATE_ENUM_PATH = @"Scripts\Core\GenerateEnum.cs";
        public static readonly string GENERATE_ENUMHELPER_PATH = @"Scripts\Core\EnumHelper.cs";
        public static readonly string GENERATE_STAGEMETA_PATH = @"Scripts\Core\SO\StageMetaDataSO.cs";
        public static readonly string GENERATE_MONSTERMETA_PATH = @"Scripts\Core\SO\MonsterMetaDataSO.cs";
    }
    public enum eSceneType
    {
        bootstrap = 0,
        title = 1,
        main = 2,
        dungeon = 3,
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


}
