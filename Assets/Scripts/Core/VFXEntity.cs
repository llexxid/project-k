using Cysharp.Threading.Tasks;
using Scripts.Core.inteface;
using System;
using System.Threading;
using UnityEngine;

namespace Scripts.Core
{
    public class VFXEntity : MonoBehaviour, IPoolable
    {
        private ulong _id;
        private CancellationTokenSource _token;

        public bool IsActive { get; set; }

        private void OnEnable()
        {
            _token?.Dispose();
            _token = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            if (_token == null) return;
            if (!_token.IsCancellationRequested) _token.Cancel();
        }

        private void OnDestroy()
        {
            if (_token == null) return;
            if (!_token.IsCancellationRequested) _token.Cancel();
            _token.Dispose();
            _token = null;
        }

        public void SetId(ulong id)
        {
            _id = id;
        }

        /// <summary>
        /// 단위는 밀리초(ms)
        /// </summary>
        public void ActiveEffect(float durationMs)
        {
            if (_token == null) _token = new CancellationTokenSource();
            UseEffect(durationMs).Forget();
        }

        private async UniTaskVoid UseEffect(float durationMs)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(durationMs), cancellationToken: _token.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (VFXManager.Instance == null) return;
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
