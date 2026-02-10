using UnityEngine;

namespace Scripts.Core
{
    // 씬 이름과 1:1로 맞추기 위해 enum 멤버를 씬 이름(소문자)과 동일하게 통일
    // 숫자값은 기존과 동일하게 유지(0~3)해서 Unity 직렬화(Inspector 저장값) 깨짐 최소화
    public enum eSceneType
    {
        bootstrap = 0,
        title = 1,
        main = 2,
        dungeon = 3,
    }

    // NOTE:
    // - VFX/SFX Addressables key를 ulong 기반으로 관리할 때 사용.
    // - 다른 어셈블리(UI 등)에서도 참조할 가능성이 있어 public으로 둠.
    public enum AssetId : ulong
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
        
        HIT_VFX = 1 | VFX_Pooling_MASK,
    }

    public enum GroupId : ulong
    {
        Stage1 = 0,

    }
}
