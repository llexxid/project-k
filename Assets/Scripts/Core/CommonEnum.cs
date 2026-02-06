using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core
{
    public enum eSceneType
    { 
        TITLE,
        INGAME,      
        DUNGEON,
    }

    //풀링이 되어야하는 VFXId는 최상위 비트가 1이다.
    //풀링이 되지 않아야하는 VFXId는 최상위 비트가 0이다.
    enum AssetId : ulong
    {
        Metor_VFX = 0,

        //추후 Sprite..이런것도
        //최상위 비트가 2이면 SFX
        SFX_MASK = 0x2000000000000000,

        //최상위 비트가 1이면 VFX. 
        VFX_Pooling_MASK = 0x1000000000000000, //풀링하는 개체는 10000
        VFX_NotPooling_MASK = 0x1100000000000000, //풀링하지않는 개체는 11....
        HIT_VFX = 1 | VFX_Pooling_MASK,
    }

}