using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Scripts.Core.inteface
{
    public interface IPoolable
    {
#if UNITY_EDITOR
        public bool IsActive { get; set; }
#endif
        void OnAlloc();
        void OnRelease();
    }
}

