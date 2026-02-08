using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Scripts.Core.inteface
{
    public interface IPoolable
    {
        public bool IsActive { get; set; }
        void OnAlloc();
        void OnRelease();
    }
}

