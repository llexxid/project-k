using UnityEngine;
using Scripts.Core.inteface;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;

namespace Scripts.Core
{
    public class VFXEntity : MonoBehaviour, IPoolable
    {
        private ulong _id;
        private Animator _am;
        private CancellationTokenSource _token;
        public bool IsActive { get; set; }

        private void OnEnable()
        {
            if (_token != null)
            {
                _token.Dispose();
            }
            _token = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            _token.Cancel();
        }

        private void OnDestroy()
        {
            _token.Cancel();
            _token.Dispose();
        }

        public void SetId(ulong id)
        {
            _id = id;
        }

        ///<summary>
        /// 단위는 밀리초입니다.
        /// </summary>
        /// <param name="durationMs"></param>
        public void ActiveEffect(float durationMs)
        {
            if (_token == null)
            {
                _token = new CancellationTokenSource();
            }
            UseEffect(durationMs).Forget();
        }

        private async UniTaskVoid UseEffect(float durationMs)
        {
            await UniTask.Delay(
                TimeSpan.FromMilliseconds(durationMs),
                cancellationToken: _token.Token
                );
            VFXManager.Instance.DestroyEffect(_id, this);
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

