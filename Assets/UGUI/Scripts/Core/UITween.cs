using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 의존성 없는 경량 UI 트윈 헬퍼. 모든 트윈은 unscaledDeltaTime 기반(타임스케일 영향 없음),
    /// 대상 컴포넌트당 트윈 종류별 1개만 활성(재호출 시 이전 것 중단)한다.
    /// 코루틴 기반이라 별도 업데이트 매니저가 필요 없고, 활성 GameObject에서만 구동된다.
    /// (팀 확장용) 새 연출이 필요하면 여기 static 진입점을 추가하면 프로젝트 전역에서 재사용된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITween : MonoBehaviour
    {
        private Coroutine _scaleCo;
        private Coroutine _fadeCo;
        private Coroutine _moveCo;

        private static UITween Get(Component c)
        {
            var t = c.GetComponent<UITween>();
            if (t == null) t = c.gameObject.AddComponent<UITween>();
            return t;
        }

        // ── 스케일 등장(팝) ──────────────────────────────────────────
        /// <summary>OutBack 스케일 등장. 팝업/카드/토스트 진입 연출.</summary>
        public static void PopIn(RectTransform rt, float duration = 0.24f, float from = 0.82f)
        {
            if (rt == null) return;
            if (!rt.gameObject.activeInHierarchy) { rt.localScale = Vector3.one; return; }
            var t = Get(rt);
            if (t._scaleCo != null) t.StopCoroutine(t._scaleCo);
            t._scaleCo = t.StartCoroutine(t.ScaleRoutine(rt, from * Vector3.one, Vector3.one, duration, EaseOutBack));
        }

        /// <summary>눌림 펀치(살짝 커졌다 복귀). 성공/획득 강조.</summary>
        public static void Punch(RectTransform rt, float duration = 0.22f, float strength = 0.12f)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return;
            var t = Get(rt);
            if (t._scaleCo != null) t.StopCoroutine(t._scaleCo);
            t._scaleCo = t.StartCoroutine(t.PunchRoutine(rt, strength, duration));
        }

        /// <summary>즉시 목표 스케일로 트윈(버튼 press 피드백 등).</summary>
        public static void ScaleTo(RectTransform rt, float target, float duration = 0.08f)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) { if (rt != null) rt.localScale = target * Vector3.one; return; }
            var t = Get(rt);
            if (t._scaleCo != null) t.StopCoroutine(t._scaleCo);
            t._scaleCo = t.StartCoroutine(t.ScaleRoutine(rt, rt.localScale, target * Vector3.one, duration, EaseOutCubic));
        }

        // ── 페이드 ───────────────────────────────────────────────────
        /// <summary>CanvasGroup 알파 페이드 인.</summary>
        public static void FadeIn(CanvasGroup cg, float duration = 0.18f)
        {
            if (cg == null) return;
            if (!cg.gameObject.activeInHierarchy) { cg.alpha = 1f; return; }
            var t = Get(cg);
            if (t._fadeCo != null) t.StopCoroutine(t._fadeCo);
            t._fadeCo = t.StartCoroutine(t.FadeRoutine(cg, cg.alpha, 1f, duration));
        }

        // ── 이동(슬라이드) ───────────────────────────────────────────
        /// <summary>아래에서 위로 슬라이드 인(바텀시트 진입). from은 시작 y 오프셋(px).</summary>
        public static void SlideUp(RectTransform rt, float fromOffsetY = 140f, float duration = 0.26f)
        {
            if (rt == null) return;
            var target = rt.anchoredPosition;
            if (!rt.gameObject.activeInHierarchy) { return; }
            var t = Get(rt);
            if (t._moveCo != null) t.StopCoroutine(t._moveCo);
            var start = target + new Vector2(0f, -fromOffsetY);
            t._moveCo = t.StartCoroutine(t.MoveRoutine(rt, start, target, duration, EaseOutCubic));
        }

        // ── 코루틴 ───────────────────────────────────────────────────
        private IEnumerator ScaleRoutine(RectTransform rt, Vector3 from, Vector3 to, float dur, Func<float, float> ease)
        {
            float e = 0f;
            rt.localScale = from;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float k = ease(Mathf.Clamp01(e / dur));
                rt.localScale = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }
            rt.localScale = to;
            _scaleCo = null;
        }

        private IEnumerator PunchRoutine(RectTransform rt, float strength, float dur)
        {
            float e = 0f;
            var baseScale = Vector3.one;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / dur);
                float s = 1f + strength * Mathf.Sin(k * Mathf.PI);   // 0→peak→0
                rt.localScale = baseScale * s;
                yield return null;
            }
            rt.localScale = baseScale;
            _scaleCo = null;
        }

        private IEnumerator FadeRoutine(CanvasGroup cg, float from, float to, float dur)
        {
            float e = 0f;
            cg.alpha = from;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(e / dur)));
                yield return null;
            }
            cg.alpha = to;
            _fadeCo = null;
        }

        private IEnumerator MoveRoutine(RectTransform rt, Vector2 from, Vector2 to, float dur, Func<float, float> ease)
        {
            float e = 0f;
            rt.anchoredPosition = from;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, ease(Mathf.Clamp01(e / dur)));
                yield return null;
            }
            rt.anchoredPosition = to;
            _moveCo = null;
        }

        // ── 이징 ─────────────────────────────────────────────────────
        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
