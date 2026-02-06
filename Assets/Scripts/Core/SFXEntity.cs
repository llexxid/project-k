using Scripts.Core.inteface;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core
{
    public class SFXEntity : MonoBehaviour, IPoolable
    {
        private AudioSource _source;

        public bool IsActive { get; set; }

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

