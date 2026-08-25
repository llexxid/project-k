using System;
using System.Collections;
using UnityEngine;
using KingdomIdle.Divine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 궁극기(신성 스킬) 컷인 연출기.
    ///
    /// 등록 지점: UGUI_UIRoot(DontDestroyOnLoad) 프리팹에 붙은 컴포넌트로, HUD 컨트롤러들과 동일하게
    /// Awake에서 DivinePresentation.CutInHandler에 자신을 걸고 OnDestroy에서 내린다.
    /// 전투 코드(DivineSkillManager.CastManual)는 핸들러 등록 여부만 보고, 컷인이 끝나는 시점에
    /// 넘겨받은 완료 콜백으로 실제 스킬을 발동한다. 핸들러가 없거나 false를 돌려주면 즉시 발동한다 —
    /// 즉 이 컴포넌트가 없어도, 프리팹이 비어 있어도 게임플레이는 절대 막히지 않는다.
    ///
    /// 시간축: 모든 보간은 unscaledDeltaTime 기반이다. 이 프로젝트의 UI 트윈(UITween, UIPulseGroup)이
    /// 전부 unscaled이고, 컷인은 게임 로직이 아니라 연출이기 때문이다. 게임은 컷인 중에도 계속 돌아간다
    /// (Time.timeScale은 건드리지 않는다) — 지금은 scaled와 결과가 같지만, 훗날 일시정지/슬로우가
    /// 들어오더라도 컷인 길이가 흔들리지 않게 unscaled로 고정한다.
    /// </summary>
    [DefaultExecutionOrder(-933)]
    public sealed class DivineCutInController : MonoBehaviour
    {
        public static DivineCutInController Instance { get; private set; }

        /// <summary>카드에 cutInDuration이 없을 때의 기본 길이(초).</summary>
        private const float DefaultDuration = 1.2f;

        // 총 길이 대비 구간 비율 — 합 1.0 (1.2초 기준: 0.168 / 0.264 / 0.192 / 0.360 / 0.216)
        private const float PhaseScrim = 0.14f;   // ① 암전
        private const float PhaseSlide = 0.22f;   // ② 일러스트 슬라이드 인
        private const float PhasePlate = 0.16f;   // ③ 등급 리본 + 이름 플레이트
        private const float PhaseHold = 0.30f;    // ④ 정지(읽는 시간)
        private const float PhaseFlash = 0.18f;   // ⑤ 섬광 (앞 35% 점등 → 콜백 → 뒤 65% 소멸)
        private const float FlashInPortion = 0.35f;

        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color FlashColor = new Color(1f, 0.96f, 0.86f, 0.88f);   // 따뜻한 백광

        private DivineCutInView _view;
        private Coroutine _routine;
        private Action _pending;      // 아직 호출하지 않은 완료 콜백 (정확히 1회 호출 보장)
        private bool _playing;
        private bool _registered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // 재생기는 앱 수명 동안 하나 — 이미 다른 재생기가 등록돼 있으면 덮어쓰지 않는다
            if (DivinePresentation.CutInHandler == null)
            {
                DivinePresentation.CutInHandler = HandleCutIn;
                _registered = true;
            }
        }

        private void OnDestroy()
        {
            // Awake에서 걸러진 중복 인스턴스는 아무것도 등록/재생하지 않았다
            if (Instance != this) return;

            if (_registered)
            {
                // 파괴된 MonoBehaviour를 가리키는 델리게이트가 남으면 시전이 조용히 먹힌다
                if (DivinePresentation.CutInHandler == (Func<DivineSkillSO, Action, bool>)HandleCutIn)
                    DivinePresentation.CutInHandler = null;
                _registered = false;
            }

            // 연출 도중 파괴돼도 콜백은 반드시 1회 호출한다(안 그러면 시전이 영원히 잠긴다)
            EndPlayback();

            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            // 비활성화되면 코루틴이 끊긴다 — 남은 연출을 접고 콜백만 확실히 넘긴다
            if (!_playing && _view == null) return;
            EndPlayback();
        }

        // ═══ DivinePresentation 진입점 ═══

        /// <summary>재생을 시작했으면 true. false면 호출측(DivineSkillManager)이 즉시 발동한다.</summary>
        private bool HandleCutIn(DivineSkillSO card, Action onComplete)
        {
            if (card == null) return false;
            if (_playing) return false;                // 중첩 재생 금지
            if (!isActiveAndEnabled) return false;     // 코루틴을 돌릴 수 없는 상태
            if (!BuildOverlay()) return false;         // 카탈로그/프리팹이 없으면 연출 없이 진행

            _pending = onComplete;
            _playing = true;
            DivinePresentation.CutInPlaying = true;
            _routine = StartCoroutine(PlayRoutine(card));
            return true;
        }

        private bool BuildOverlay()
        {
            DestroyOverlay();

            var ui = UIManager.Instance;
            if (ui == null || ui.LayerOverlays == null || ui.Catalog == null || ui.Catalog.overlayDivineCutIn == null)
                return false;

            var go = Instantiate(ui.Catalog.overlayDivineCutIn, ui.LayerOverlays, false);

            // 오버레이는 화면 전체를 덮는다 (HUD와 달리 스트레치한다)
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            _view = go.GetComponent<DivineCutInView>();
            if (_view == null)
            {
                Debug.LogError("[DivineCutIn] DivineCutInView 컴포넌트가 없습니다.");
                Destroy(go);
                return false;
            }

            go.transform.SetAsLastSibling();
            return true;
        }

        // ═══ 연출 ═══

        private IEnumerator PlayRoutine(DivineSkillSO card)
        {
            float total = card.cutInDuration > 0.05f ? card.cutInDuration : DefaultDuration;
            var gradeColor = DivineSkillSO.GetGradeColor(card.grade);

            // 컷씬 컷아웃 → 스탠딩 → 아이콘 순. 셋 다 없으면 이미지를 끄고 이름 플레이트만으로 진행한다
            var sprite = card.cutInIllustration != null ? card.cutInIllustration
                       : card.illustration != null ? card.illustration
                       : card.icon;

            // 알파 0은 컬링될 수 있어 입력 차단이 풀린다 — 첫 프레임부터 아주 옅게 깔아 둔다
            if (_view.scrim != null)
                _view.scrim.color = new Color(ScrimColor.r, ScrimColor.g, ScrimColor.b, 0.004f);

            if (_view.flash != null)
                _view.flash.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0f);

            if (_view.illust != null)
            {
                _view.illust.sprite = sprite;
                _view.illust.enabled = sprite != null;   // null이면 흰 박스가 되므로 렌더 자체를 끈다
            }
            if (_view.illustGroup != null) _view.illustGroup.alpha = 0f;
            if (_view.plateGroup != null) _view.plateGroup.alpha = 0f;

            if (_view.gradeRibbon != null) _view.gradeRibbon.color = gradeColor;
            if (_view.gradeLabel != null) _view.gradeLabel.text = DivineSkillSO.GetGradeName(card.grade);
            if (_view.nameLabel != null) _view.nameLabel.text = card.DisplayName;
            if (_view.skillLabel != null)
            {
                _view.skillLabel.text = string.IsNullOrEmpty(card.skillNameKor) ? card.DisplayName : card.skillNameKor;
                _view.skillLabel.color = gradeColor;
            }

            Vector2 illustHome = _view.illustHolder != null ? _view.illustHolder.anchoredPosition : Vector2.zero;
            Vector2 illustFrom = illustHome + new Vector2(UguiTheme.DivineCutInSlideX, 0f);
            if (_view.illustHolder != null) _view.illustHolder.anchoredPosition = illustFrom;

            // ① 암전
            yield return Sweep(total * PhaseScrim, k =>
            {
                if (_view.scrim != null)
                    _view.scrim.color = new Color(ScrimColor.r, ScrimColor.g, ScrimColor.b, ScrimColor.a * k);
            });

            // ② 일러스트가 옆에서 밀려 들어온다
            yield return Sweep(total * PhaseSlide, k =>
            {
                float e = EaseOutCubic(k);
                if (_view.illustHolder != null)
                    _view.illustHolder.anchoredPosition = Vector2.LerpUnclamped(illustFrom, illustHome, e);
                if (_view.illustGroup != null) _view.illustGroup.alpha = e;
            });

            // ③ 등급 리본 + 이름 플레이트
            if (_view != null && _view.plate != null && _view.plate.gameObject.activeInHierarchy)
                UITween.PopIn(_view.plate, total * PhasePlate, 0.86f);
            yield return Sweep(total * PhasePlate, k =>
            {
                if (_view.plateGroup != null) _view.plateGroup.alpha = k;
            });

            // ④ 정지 — 이름을 읽는 시간
            yield return Sweep(total * PhaseHold, null);

            // ⑤ 섬광 점등
            yield return Sweep(total * PhaseFlash * FlashInPortion, k =>
            {
                if (_view.flash != null)
                    _view.flash.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, FlashColor.a * k);
            });

            // 화면이 가장 하얀 순간에 실제 스킬을 발동시킨다 (연출 → 타격 전환점)
            Finish();

            // ⑤' 섬광 + 암전 소멸 — 밝아진 화면 밑에서 스킬 이펙트가 시작된다
            yield return Sweep(total * PhaseFlash * (1f - FlashInPortion), k =>
            {
                float inv = 1f - k;
                if (_view.flash != null)
                    _view.flash.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, FlashColor.a * inv);
                if (_view.scrim != null)
                    _view.scrim.color = new Color(ScrimColor.r, ScrimColor.g, ScrimColor.b, ScrimColor.a * inv);
                if (_view.illustGroup != null) _view.illustGroup.alpha = inv;
                if (_view.plateGroup != null) _view.plateGroup.alpha = inv;
            });

            // 조기 이탈로 위쪽 Finish를 건너뛴 경우까지 여기서 마감한다 (콜백은 여전히 1회)
            EndPlayback();
        }

        /// <summary>
        /// unscaled 기반 0→1 스윕. 오버레이가 도중에 사라지면 즉시 빠져나온다
        /// (남은 구간은 건너뛰고 루틴 끝의 Finish로 흘러간다).
        /// </summary>
        private IEnumerator Sweep(float duration, Action<float> apply)
        {
            if (_view == null) yield break;

            if (duration <= 0f)
            {
                apply?.Invoke(1f);
                yield break;
            }

            float e = 0f;
            while (e < duration)
            {
                if (_view == null) yield break;
                e += Time.unscaledDeltaTime;
                apply?.Invoke(Mathf.Clamp01(e / duration));
                yield return null;
            }

            if (_view != null) apply?.Invoke(1f);
        }

        /// <summary>
        /// 완료 콜백을 정확히 1회 호출한다. 여러 번 불러도 안전(멱등).
        /// _playing은 건드리지 않는다 — 잔여 페이드아웃이 도는 동안 새 컷인이 끼어들어
        /// 진행 중인 오버레이를 갈아치우는 것을 막아야 하기 때문.
        /// </summary>
        private void Finish()
        {
            var callback = _pending;
            _pending = null;
            DivinePresentation.CutInPlaying = false;

            // 앱 종료 중에는 게임플레이를 건드리지 않는다 — 이 콜백은 DivineSkillManager.Cast()
            // 로 이어져 물리 질의·Instantiate·코루틴을 돌리므로, 셧다운이 실제 시전으로 변한다.
            // (앱이 계속 도는 중의 파괴에서는 여전히 정확히 1회 호출해 _casting 잠금을 푼다)
            if (callback == null || _quitting) return;
            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private bool _quitting;

        // 실제 종료와 에디터 플레이 중단 모두에서 OnDisable/OnDestroy 보다 먼저 호출된다
        private void OnApplicationQuit() => _quitting = true;

        /// <summary>재생 종료 — 콜백 마감 + 오버레이 파괴 + 재진입 잠금 해제.</summary>
        private void EndPlayback()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _playing = false;
            Finish();
            DestroyOverlay();
        }

        private void DestroyOverlay()
        {
            if (_view != null) Destroy(_view.gameObject);
            _view = null;
        }

        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    }
}
