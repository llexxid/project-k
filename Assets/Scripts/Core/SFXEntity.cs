using Cysharp.Threading.Tasks;
using Scripts.Core.inteface;
using System;
using System.Threading;
using UnityEngine;

namespace Scripts.Core
{
    public class SFXEntity : MonoBehaviour, IPoolable
    {
        private AudioSource _source;
        private CancellationTokenSource _token;

        public bool IsActive { get; set; }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null)
            {
                CustomLogger.LogError("[SFXEntity] AudioSource component is missing.");
            }
        }

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

        public void SetClip(AudioClip clip)
        {
            if (_source == null) return;
            _source.clip = clip;
        }

        /// <summary>
        /// 지정한 ms 이후(Delay) 재생
        /// </summary>
        public void PlaySFX(float delayMs)
        {
            PlayAfterDelay(delayMs).Forget();
        }

        /// <summary>
        /// 즉시 재생
        /// </summary>
        public void PlaySFX()
        {
            if (_source == null || _source.clip == null) return;

            _source.Play();
            AutoRelease(_source.clip.length * 1000.0f);
        }

        private async UniTaskVoid PlayAfterDelay(float delayMs)
        {
            if (_source == null || _source.clip == null) return;

            try
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken: _token.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _source.Play();
            AutoRelease(_source.clip.length * 1000.0f);
        }

        private void AutoRelease(float durationMs)
        {
            AutoReleaseAsync(durationMs).Forget();
        }

        private async UniTaskVoid AutoReleaseAsync(float durationMs)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(durationMs), cancellationToken: _token.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (SFXManager.Instance == null) return;
            SFXManager.Instance.DestroySFX(this);
        }

        public void OnAlloc()
        {
            return;
        }

        public void OnRelease()
        {
            if (_source == null) return;

            _source.loop = false;
            _source.clip = null;
            _source.volume = 0;
            return;
        }
    }
}
