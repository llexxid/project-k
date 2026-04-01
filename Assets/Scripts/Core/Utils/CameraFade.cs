using System;
using System.Collections;
using UnityEngine;

namespace Scripts.Core.Utils
{
    /// <summary>
    /// 카메라에 붙여서 사용하는 화면 페이드 (OnRenderImage 기반).
    /// UI는 가리지 않고 게임 화면만 어두워진다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFade : MonoBehaviour
    {
        public static CameraFade Instance { get; private set; }

        private float _alpha;
        private Texture2D _tex;
        private bool _fading;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _tex = new Texture2D(1, 1);
            _tex.SetPixel(0, 0, Color.black);
            _tex.Apply();
        }

        private void OnGUI()
        {
            if (_alpha <= 0f) return;
            Color prev = GUI.color;
            GUI.color = new Color(0, 0, 0, _alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _tex);
            GUI.color = prev;
        }

        public bool IsFading => _fading;
        public bool IsDark => _alpha >= 1f;

        // 즉시 암전
        public void SetDark()
        {
            _alpha = 1f;
        }

        // 즉시 밝게
        public void SetClear()
        {
            _alpha = 0f;
        }

        public void FadeOut(float duration, Action onComplete = null)
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(0f, 1f, duration, onComplete));
        }

        public void FadeIn(float duration, Action onComplete = null)
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(1f, 0f, duration, onComplete));
        }

        // 페이드아웃 → 콜백 → 페이드인
        public void FadeOutIn(float outDuration, float inDuration, Action onDark = null, Action onComplete = null)
        {
            StopAllCoroutines();
            StartCoroutine(FadeOutInRoutine(outDuration, inDuration, onDark, onComplete));
        }

        private IEnumerator FadeRoutine(float from, float to, float duration, Action onComplete)
        {
            _fading = true;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _alpha = to;
            _fading = false;
            onComplete?.Invoke();
        }

        private IEnumerator FadeOutInRoutine(float outDur, float inDur, Action onDark, Action onComplete)
        {
            _fading = true;

            float t = 0f;
            while (t < outDur)
            {
                t += Time.unscaledDeltaTime;
                _alpha = Mathf.Lerp(0f, 1f, t / outDur);
                yield return null;
            }
            _alpha = 1f;

            onDark?.Invoke();

            t = 0f;
            while (t < inDur)
            {
                t += Time.unscaledDeltaTime;
                _alpha = Mathf.Lerp(1f, 0f, t / inDur);
                yield return null;
            }
            _alpha = 0f;

            _fading = false;
            onComplete?.Invoke();
        }
    }
}
