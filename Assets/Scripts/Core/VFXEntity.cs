using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.inteface;

namespace Scripts.Core
{
    //풀링이 되어야하는 VFXId는 최상위 비트가 1이다.
    //풀링이 되지 않아야하는 VFXId는 최상위 비트가 0이다.
    enum VFXId : ulong
    {
        Metor_VFX = 0,

        VFX_Pooling_MASK = 0x1000000000000000,
        HIT_VFX = 1 | VFX_Pooling_MASK,
    }

    public class VFXEntity : MonoBehaviour, IPoolable
    {
        private ulong _id;
        private Animator _am;
#if UNITY_EDITOR
        public bool IsActive { get; set; }
#endif
        public void Init(ulong id)
        {
            _id = id;
        }
        public void ActiveEffect()
        { 
            
        }
        public void OnAlloc()
        {
            return;
        }

        public void OnRelease()
        {
            return;
        }
    }
}

