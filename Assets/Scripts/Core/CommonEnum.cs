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
        Metor_VFX = 0,

        // 최상위 비트가 2면 SFX
        SFX_MASK = 0x2000000000000000,

        // 최상위 비트가 1이면 VFX
        VFX_Pooling_MASK = 0x1000000000000000,
        VFX_NotPooling_MASK = 0x1100000000000000,

        HIT_VFX = 1 | VFX_Pooling_MASK,
    }

    public enum GroupId : ulong
    {
        Character,
        Monster,
        VFX,
        SFX,
        GameLobbyScene,
        TitleScene,
    }
}
