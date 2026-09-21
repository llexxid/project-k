using System;
using Direction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomIdle.UGUI
{
    /// <summary>표시와 터치만 담당하는 프리팹 View다. 실제 버튼을 옮기지 않고 같은 위치에 강조 테두리를 그린다.</summary>
    public sealed class FeatureGuideView : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup inputGroup;
        [SerializeField] internal RectTransform[] dimPanels;
        [SerializeField] internal RectTransform highlight;
        [SerializeField] internal RectTransform card;
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text descriptionLabel;
        [SerializeField] internal TMP_Text progressLabel;
        [SerializeField] internal TMP_Text nextLabel;
        [SerializeField] internal UnityEngine.UI.Button nextButton;
        [SerializeField] internal UnityEngine.UI.Button skipButton;
        [SerializeField] internal FeatureGuideInputGate inputGate;
        private readonly Vector3[] _corners = new Vector3[4];
        private UnityEngine.UI.RectMask2D[] _clipMasks;
        private Rect _focusRegion;
        private RectTransform _target;
        private GameDirectStep _step;
        private Func<bool> _canDisplay;
        private Rect _previousTarget, _previousRoot;
        private bool _layoutDirty;
        private bool _responded;
        private GameObject _previousSelection;
        public event Action<GameDirectResult> Responded;
        public bool IsVisible => gameObject.activeInHierarchy && inputGroup != null && inputGroup.blocksRaycasts;
        public Rect HighlightRect => _previousTarget;

        /// <summary>프리팹 생성 후 한 번만 버튼을 연결한다. 재표시할 때 리스너가 누적되지 않는다.</summary>
        private void Awake()
        {
            nextButton.onClick.AddListener(Confirm);
            skipButton.onClick.AddListener(Skip);
            if (inputGate != null) inputGate.Owner = this;
        }

        /// <summary>Player가 준비를 확인한 단계를 표시한다. 표시 범위가 아직 없으면 입력·참조를 정리하고 반환해 Player가 같은 단계를 재시도하게 한다.</summary>
        public void Show(GameDirectStep step, RectTransform target, Func<bool> canDisplay)
        {
            _step = step; _target = target; _canDisplay = canDisplay;
            // 표시할 때만 마스크를 수집한다. 스크롤·창 크기가 바뀌면 보이는 영역의 교집합을 다시 구한다.
            _clipMasks = target.GetComponentsInParent<UnityEngine.UI.RectMask2D>();
            _focusRegion = FeatureGuideAnchor.FocusRegion(target, step.Target);
            _responded = false; _layoutDirty = true;
            titleLabel.text = step.Title;
            descriptionLabel.text = step.Description.Replace("{dailyTickets}", Scripts.Core.StageCatalogRules.Database.DailyTickets.ToString());
            if (GameDirectInteraction.AtAttackCap) descriptionLabel.text = "공격력이 이미 최대 강화 상태입니다. 추가 소비 없이 안내를 완료합니다.";
            if (GameDirectInteraction.Succeeded) descriptionLabel.text = "뽑기가 완료되었습니다. 획득 결과를 확인하고 결과 창의 확인 버튼을 눌러 주세요.";
            progressLabel.text = $"{step.Number} / {step.Count}";
            nextLabel.text = step.Number == step.Count ? "확인" : "다음";
            nextButton.interactable = skipButton.interactable = true;
            if (step.Completion == GuideCompletion.Click) { nextLabel.text = "대상을 눌러주세요"; nextButton.interactable = false; }
            else if (step.Completion == GuideCompletion.Action && !GameDirectInteraction.AtAttackCap) nextLabel.text = "나중에 계속";
            inputGroup.alpha = 1; inputGroup.interactable = true; inputGroup.blocksRaycasts = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            FocusTarget();
            if (EventSystem.current != null)
            {
                _previousSelection = EventSystem.current.currentSelectedGameObject;
                EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
            }
            // 패널 복원 직후에는 대상이 살아 있어도 아직 마스크 밖일 수 있다. 레이아웃 실패는 완료가 아닌 표시 대기다.
            // Hide는 _clipMasks와 _step까지 비우므로 실패 뒤 장식 초기화로 진행해서는 안 된다.
            if (!TryRefreshLayout()) { Hide(); return; }
            UITween.PopIn(card, .18f, .96f);
            if (_clipMasks.Length == 0) UITween.BreathScale(highlight, .018f, 1.8f);
        }

        /// <summary>현재 단계 확인을 전달한다. 연타는 Respond의 한 번 응답 장치에서 차단한다.</summary>
        public void Confirm()
        {
            if (_step == null || _step.Completion == GuideCompletion.Click) return;
            Respond(_step.Completion == GuideCompletion.Action && !GameDirectInteraction.AtAttackCap ? GameDirectResult.Deferred : GameDirectResult.Confirmed);
        }

        /// <summary>단계를 처음 표시할 때 스크롤 밖의 대상을 뷰포트 안으로 옮긴다. 사용자의 이후 스크롤을 매 프레임 덮지 않는다.</summary>
        private void FocusTarget()
        {
            var scroll = _target != null ? _target.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            if (_target == scroll.viewport || !_target.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            if (scroll.content.rect.height <= scroll.viewport.rect.height) return;
            Vector3 center = scroll.viewport.InverseTransformPoint(_target.TransformPoint(_target.rect.center));
            float delta = scroll.viewport.rect.center.y - center.y;
            if (Mathf.Abs(delta) < 20) return;
            scroll.StopMovement();
            scroll.content.anchoredPosition += new Vector2(0, delta);
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
        }

        /// <summary>화면 버튼과 Android 뒤로가기가 공유하는 사용자 건너뛰기 동작이다.</summary>
        public void Skip() => Respond(GameDirectResult.Skipped);

        /// <summary>가려진 화면의 입력은 무시하고 유효한 응답 한 번만 전달한다. 저장 확정은 Manager의 책임이다.</summary>
        private void Respond(GameDirectResult result)
        {
            if (_responded || !IsVisible || _canDisplay == null || !_canDisplay()) return;
            _responded = true;
            nextButton.interactable = skipButton.interactable = false;
            Responded?.Invoke(result);
        }

        /// <summary>UIManager의 뒤로가기 우선순위에서 호출한다. 모달에 가려진 안내는 뒤로가기를 소비하지 않는다.</summary>
        public bool HandleBack()
        {
            if (!IsVisible || _canDisplay == null || !_canDisplay()) return false;
            Skip();
            return true;
        }

        /// <summary>프레임의 마지막에 가림 상태와 좌표 변화를 검사해 모달 위에 잘못 그려지는 한 프레임도 줄인다.</summary>
        private void LateUpdate()
        {
            if (_target == null || _canDisplay == null || !_canDisplay()) { Hide(); return; }
            if (!TryRefreshLayout()) Hide();
        }

        /// <summary>
        /// 대상 월드 좌표를 SafeArea 아래 오버레이 좌표로 변환한다. 네 암전 사각형은 밝은 구멍을 만들고,
        /// 투명 입력 막은 단계가 허용한 실제 버튼·스크롤만 통과시킨다. 값이 같으면 레이아웃을 다시 쓰지 않는다.
        /// 표시 가능한 교집합이 없으면 false를 반환한다. 숨김·참조 정리는 호출자가 맡아 계산 중 수명 상태가 바뀌지 않는다.
        /// </summary>
        private bool TryRefreshLayout()
        {
            if (_target == null || _step == null) return false;
            var root = (RectTransform)transform;
            Rect bounds = root.rect;
            var targetRect = _target.rect;
            Vector3 low = root.InverseTransformPoint(_target.TransformPoint(targetRect.min + Vector2.Scale(targetRect.size, _focusRegion.min)));
            Vector3 high = root.InverseTransformPoint(_target.TransformPoint(targetRect.min + Vector2.Scale(targetRect.size, _focusRegion.max)));
            Rect hole = Rect.MinMaxRect(Mathf.Max(bounds.xMin, low.x - 10), Mathf.Max(bounds.yMin, low.y - 10),
                Mathf.Min(bounds.xMax, high.x + 10), Mathf.Min(bounds.yMax, high.y + 10));
            // SafeArea만으로 자르면 긴 콘텐츠가 전장을 모두 강조한다. 활성 마스크 안의 부분만 밝게 남긴다.
            if (_clipMasks != null)
                foreach (var mask in _clipMasks)
                {
                    if (mask == null || !mask.isActiveAndEnabled) continue;
                    mask.rectTransform.GetWorldCorners(_corners);
                    low = root.InverseTransformPoint(_corners[0]); high = root.InverseTransformPoint(_corners[2]);
                    hole = Rect.MinMaxRect(Mathf.Max(hole.xMin, low.x), Mathf.Max(hole.yMin, low.y),
                        Mathf.Min(hole.xMax, high.x), Mathf.Min(hole.yMax, high.y));
                }
            if (hole.width <= 0 || hole.height <= 0) return false;
            if (!_layoutDirty && hole == _previousTarget && bounds == _previousRoot) return true;
            _layoutDirty = false; _previousTarget = hole; _previousRoot = bounds;
            Place(highlight, hole);
            Place(dimPanels[0], Rect.MinMaxRect(bounds.xMin, hole.yMax, bounds.xMax, bounds.yMax));
            Place(dimPanels[1], Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, hole.yMin));
            Place(dimPanels[2], Rect.MinMaxRect(bounds.xMin, hole.yMin, hole.xMin, hole.yMax));
            Place(dimPanels[3], Rect.MinMaxRect(hole.xMax, hole.yMin, bounds.xMax, hole.yMax));
            float width = Mathf.Min(860, bounds.width - 48);
            float textHeight = descriptionLabel.GetPreferredValues(descriptionLabel.text, width - 72, 0).y;
            // 위쪽 제목 116 + 아래 버튼 영역 176에 여유를 더한다. 본문 높이를 측정한 뒤 실제 가용 높이도 보장한다.
            float height = Mathf.Max(400, textHeight + 310);
            height = Mathf.Min(height, bounds.height - 48);
            float above = bounds.yMax - hole.yMax, below = hole.yMin - bounds.yMin;
            bool useAbove = _step.Placement == GuideCardPlacement.Above ||
                (_step.Placement == GuideCardPlacement.Auto && above >= below);
            if ((useAbove ? above : below) < height + 24) useAbove = above >= below;
            float x = Mathf.Clamp(hole.center.x - width / 2, bounds.xMin + 24, bounds.xMax - width - 24);
            float y = useAbove ? hole.yMax + 24 : hole.yMin - height - 24;
            y = Mathf.Clamp(y, bounds.yMin + 24, bounds.yMax - height - 24);
            Place(card, new Rect(x, y, width, height));
            return true;
        }

        /// <summary>부모 중앙 기준 사각형을 설정한다. 루트의 기존 CanvasScaler와 SafeArea 설정을 그대로 사용한다.</summary>
        private static void Place(RectTransform rect, Rect bounds)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = bounds.center;
            rect.sizeDelta = bounds.size;
        }

        /// <summary>표시 중단 시 입력을 먼저 풀고 참조를 해제한다. timeScale은 원래 전투 시스템이 계속 소유한다.</summary>
        public void Hide()
        {
            GameDirectInteraction.Suspend();
            if (inputGroup != null) { inputGroup.blocksRaycasts = false; inputGroup.interactable = false; }
            if (highlight != null) UITween.StopBreathScale(highlight);
            if (EventSystem.current != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(transform))
                    EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            }
            _previousSelection = null;
            _target = null; _step = null; _canDisplay = null;
            _clipMasks = null;
            gameObject.SetActive(false);
        }
    }
}
