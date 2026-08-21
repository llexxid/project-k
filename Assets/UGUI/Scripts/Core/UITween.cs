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
        private Coroutine _breathCo;
        private Coroutine _rotateCo;
        private Coroutine _flashCo;
        private Color _flashBaseColor;      // FlashRing 원래 색 (연출 도중 재호출/중단돼도 복원 기준)
        private bool _flashBaseCaptured;

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

        /// <summary>
        /// 진행 중인 슬라이드(이동) 트윈 중단. 위치는 건드리지 않는다 —
        /// 다른 주체(퇴장 연출 등)가 같은 anchoredPosition 을 이어서 쓸 때, 두 코루틴이
        /// 매 프레임 같은 값을 덮어쓰며 싸우는 것을 막기 위한 것이다.
        /// </summary>
        public static void StopMove(RectTransform rt)
        {
            if (rt == null) return;
            var t = rt.GetComponent<UITween>();
            if (t != null && t._moveCo != null) { t.StopCoroutine(t._moveCo); t._moveCo = null; }
        }

        // ── 살아있는 UI: 호흡/회전/플래시 (신 스킬 버튼·마탑 환경 연출 공용) ──────
        /// <summary>1 ↔ 1+amplitude 사이를 부드럽게 오가는 호흡 스케일 루프. StopBreathScale로 중단.</summary>
        public static void BreathScale(RectTransform rt, float amplitude = 0.05f, float period = 2.4f)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return;
            var t = Get(rt);
            if (t._breathCo != null) t.StopCoroutine(t._breathCo);
            t._breathCo = t.StartCoroutine(t.BreathRoutine(rt, amplitude, Mathf.Max(0.1f, period)));
        }

        /// <summary>호흡 스케일 중단 + 스케일 원복. 대상이 비활성/파괴 상태여도 안전.</summary>
        public static void StopBreathScale(RectTransform rt)
        {
            if (rt == null) return;
            var t = rt.GetComponent<UITween>();
            if (t != null && t._breathCo != null) { t.StopCoroutine(t._breathCo); t._breathCo = null; }
            rt.localScale = Vector3.one;
        }

        /// <summary>z축 연속 회전 루프(도/초, 양수 = 시계방향). StopRotateLoop로 중단.
        /// 재호출 시 기존 루프를 교체하므로 재표시 후 재시작 호출이 안전하다.</summary>
        public static void RotateLoop(RectTransform rt, float degPerSec)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return;
            var t = Get(rt);
            if (t._rotateCo != null) t.StopCoroutine(t._rotateCo);
            t._rotateCo = t.StartCoroutine(t.RotateRoutine(rt, degPerSec));
        }

        /// <summary>회전 루프 중단 + 회전 원복.</summary>
        public static void StopRotateLoop(RectTransform rt)
        {
            if (rt == null) return;
            var t = rt.GetComponent<UITween>();
            if (t != null && t._rotateCo != null) { t.StopCoroutine(t._rotateCo); t._rotateCo = null; }
            rt.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// FlashRing 기준색 교체 (컨셉 스킨 등). FlashRing 은 최초 1회 원색을 캡처해 복원 기준으로
        /// 쓰므로, 색을 바꿀 땐 이 메서드로 캡처값까지 갱신해야 다음 플래시부터 새 색으로 재생·복원된다.
        /// </summary>
        public static void SetFlashRingBaseColor(Image ring, Color color)
        {
            if (ring == null) return;
            ring.color = color;
            var t = ring.GetComponent<UITween>();
            if (t != null)
            {
                t._flashBaseColor = color;
                t._flashBaseCaptured = true;
            }
        }

        /// <summary>
        /// 시전 플래시: 비활성 링 Image를 켜서 1→endScale로 커지며 페이드 아웃, 끝나면 다시 끈다.
        /// 캐시된 1개 인스턴스를 재사용하는 전제(Instantiate 없음). 재호출 시 처음부터 다시 재생.
        /// </summary>
        public static void FlashRing(Image ring, float duration = 0.35f, float endScale = 1.6f)
        {
            if (ring == null) return;
            var t = Get(ring);
            if (t._flashCo != null) { t.StopCoroutine(t._flashCo); t._flashCo = null; }
            if (!t._flashBaseCaptured)
            {
                t._flashBaseColor = ring.color;   // 최초 1회 원색 캡처 (중단된 연출의 중간색 오염 방지)
                t._flashBaseCaptured = true;
            }
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            if (!ring.gameObject.activeInHierarchy) { ring.gameObject.SetActive(false); return; }
            t._flashCo = t.StartCoroutine(t.FlashRingRoutine(ring, duration, endScale));
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

        private IEnumerator BreathRoutine(RectTransform rt, float amplitude, float period)
        {
            float e = 0f;
            while (true)
            {
                e += Time.unscaledDeltaTime;
                // 코사인 기반 — 스케일 1에서 시작해 1+amplitude까지 갔다가 되돌아온다 (튐 없음)
                float s = 1f + amplitude * (0.5f - 0.5f * Mathf.Cos(e / period * 2f * Mathf.PI));
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private IEnumerator RotateRoutine(RectTransform rt, float degPerSec)
        {
            while (true)
            {
                rt.Rotate(0f, 0f, -degPerSec * Time.unscaledDeltaTime);   // UGUI z회전은 반시계 양수 → 부호 반전
                yield return null;
            }
        }

        private IEnumerator FlashRingRoutine(Image ring, float dur, float endScale)
        {
            var rt = ring.rectTransform;
            Color c0 = _flashBaseColor;
            float e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(e / dur));
                float s = Mathf.LerpUnclamped(1f, endScale, k);
                rt.localScale = new Vector3(s, s, 1f);
                ring.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - k));
                yield return null;
            }
            // SetActive(false)가 이 코루틴을 죽이기 전에 원상 복구를 먼저 끝낸다
            ring.color = c0;
            rt.localScale = Vector3.one;
            _flashCo = null;
            ring.gameObject.SetActive(false);
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
