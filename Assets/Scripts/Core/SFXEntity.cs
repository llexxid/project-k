using Cysharp.Threading.Tasks;
using Scripts.Core.inteface;
using System;
using System.Collections;
using System.Collections.Generic;
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
            _source = gameObject.GetComponent<AudioSource>();
        }

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

        public void SetClip(AudioClip clip)
        {
            _source.clip = clip;
        }
        /// <summary>
        /// n초후(ms) 효과음 발생
        /// </summary>
        /// <param name="duration"></param>
        public void PlaySFX(float duration)
        {
            AudioClip clip = _source.clip;
            Delay(duration).Forget();
            _source.Play();
            AutoRelease(clip.length * 1000.0f);
        }

        //효과음 길이만큼 발생
        public void PlaySFX()
        {
            AudioClip clip = _source.clip;
            _source.Play();
            AutoRelease(clip.length * 1000.0f);
        }

        private async UniTaskVoid Delay(float duration)
        {
            await UniTask.Delay(TimeSpan.FromMilliseconds(duration), cancellationToken: _token.Token);
        }

        private void AutoRelease(float duration)
        {
            Delay(duration).Forget();
            SFXManager.Instance.DestroySFX(this);
        }

        public void OnAlloc()
        {
            return;
        }

        public void OnRelease()
        {
            _source.loop = false;
            _source.clip = null;
            _source.volume = 0;
            return;
        }
    }
}

