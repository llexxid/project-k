using System;
using System.Collections;
using UnityEngine;

namespace Scripts.Core.Utils
{
    /// <summary>
    /// 카메라 앞에 검은 SpriteRenderer를 배치해 게임 화면만 페이드한다.
    /// UI Toolkit 요소는 별도 렌더링 파이프라인이므로 영향받지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFade : MonoBehaviour
    {
        public static CameraFade Instance { get; private set; }

        private SpriteRenderer _fadeRenderer;
        private float _alpha;
        private bool _fading;
        private Camera _cam;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _cam = GetComponent<Camera>();
            CreateFadeSprite();
        }

        private void CreateFadeSprite()
        {
            var go = new GameObject("__FadeOverlay");
            go.transform.SetParent(transform);

            _fadeRenderer = go.AddComponent<SpriteRenderer>();
            _fadeRenderer.sortingOrder = 30000;

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fadeRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _fadeRenderer.color = new Color(0, 0, 0, 0);

            UpdateScale();
        }

        private void LateUpdate()
        {
            if (_fadeRenderer != null)
                UpdateScale();
        }

        private void UpdateScale()
        {
            if (_cam == null || _fadeRenderer == null) return;

            float camDist = Mathf.Abs(_cam.transform.position.z);
            float height = _cam.orthographic
                ? _cam.orthographicSize * 2f
                : 2f * camDist * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * _cam.aspect;

            // 여유분 추가
            _fadeRenderer.transform.localPosition = new Vector3(0, 0, camDist + 0.1f);
            _fadeRenderer.transform.localScale = new Vector3(width + 2f, height + 2f, 1f);
        }

        private void ApplyAlpha()
        {
            if (_fadeRenderer != null)
                _fadeRenderer.color = new Color(0, 0, 0, _alpha);
        }

        public bool IsFading => _fading;
        public bool IsDark => _alpha >= 1f;

        public void SetDark()
        {
            _alpha = 1f;
            ApplyAlpha();
        }

        public void SetClear()
        {
            _alpha = 0f;
            ApplyAlpha();
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
                ApplyAlpha();
                yield return null;
            }
            _alpha = to;
            ApplyAlpha();
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
                ApplyAlpha();
                yield return null;
            }
            _alpha = 1f;
            ApplyAlpha();

            onDark?.Invoke();

            t = 0f;
            while (t < inDur)
            {
                t += Time.unscaledDeltaTime;
                _alpha = Mathf.Lerp(1f, 0f, t / inDur);
                ApplyAlpha();
                yield return null;
            }
            _alpha = 0f;
            ApplyAlpha();

            _fading = false;
            onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
