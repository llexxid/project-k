using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    // Panel_Guide 셸에 TutorialManager 데이터를 채우고 인터랙션 처리
    // (UITKGuidePanelController 이식 — UGUI 동적 행은 UguiRuntimeFactory 로 생성)
    public static class GuidePanelController
    {
        // ── GameUI.uss guide-* 토큰 ──
        private static readonly Color TitleColor = new Color(1f, 1f, 1f, 0.95f);        // .guide-step-title
        private static readonly Color TitleDoneColor = new Color(1f, 1f, 1f, 0.45f);    // .guide-step-title-done
        private static readonly Color DescColor = new Color(1f, 1f, 1f, 0.80f);         // .guide-step-desc
        private static readonly Color CheckColor = new Color(100f / 255f, 210f / 255f, 130f / 255f, 0.95f); // .guide-check-btn color
        private static readonly Color CheckBorderColor = new Color(1f, 1f, 1f, 0.25f);  // .guide-check-btn border
        private static readonly Color CheckBgColor = new Color(1f, 1f, 1f, 0.10f);      // .guide-check-btn background
        private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.08f);      // .guide-step-row border-bottom
        private static readonly Color EmptyHintColor = new Color(1f, 1f, 1f, 0.5f);     // .guide-empty-hint

        public static void Populate(GuidePanelView view, System.Action onProgressChanged = null)
        {
            if (view == null) return;

            var manager = TutorialManager.Instance;
            if (view.listContent == null) return;

            // 원본은 이벤트를 구독하지 않으므로 OnClosed 에서 해제할 것도 없다.
            // (진행 변경 통지는 onProgressChanged 콜백으로만 전달 — 원본과 동일)

            UguiRuntimeFactory.Clear(view.listContent);

            if (manager == null)
            {
                AddEmptyHint(view.listContent, "TutorialManager를 씬에 배치해주세요.");
                return;
            }

            var steps = manager.GetSteps();

            if (steps.Count == 0)
            {
                AddEmptyHint(view.listContent, "등록된 가이드 단계가 없습니다.");
                RefreshProgress(manager, view);
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) continue;
                BuildStepRow(view, step, manager, onProgressChanged);
            }

            RefreshProgress(manager, view);
        }

        private static void BuildStepRow(
            GuidePanelView view,
            TutorialStepDataSO step,
            TutorialManager manager,
            Action onProgressChanged)
        {
            bool done = manager.IsStepCompleted(step.id);

            // 각 단계를 카드 박스로 (구분선 대신 픽셀 카드 배경 — 목록이 깔끔하게 구분된다)
            var row = UguiRuntimeFactory.PixelCard(view.listContent, "GuideStepCard");
            UguiRuntimeFactory.VerticalLayout(row.gameObject, 0f);

            // 완료된 단계는 흐리게
            var rowGroup = row.gameObject.AddComponent<CanvasGroup>();
            rowGroup.alpha = done ? 0.55f : 1f;

            // 본문: padding, gap 16, align-items flex-start
            var inner = UguiRuntimeFactory.Container(row.transform, "RowInner");
            UguiRuntimeFactory.HorizontalLayout(
                inner.gameObject, 16f, new RectOffset(16, 16, 16, 16), TextAnchor.UpperLeft);

            // ── .guide-check-btn: 52x52 원형, 흰색 10% 배경 + 2px 테두리(외곽 원 + 내부 원) ──
            var checkBorder = UguiRuntimeFactory.Box(inner, "CheckBtn", CheckBorderColor, rounded: false, raycastTarget: true);
            ApplyCircleSprite(checkBorder);
            var checkLe = UguiRuntimeFactory.Preferred(checkBorder, 52f, 52f);
            checkLe.minWidth = 52f;   // flex-shrink: 0
            checkLe.minHeight = 52f;

            var checkBg = UguiRuntimeFactory.Box(checkBorder.transform, "CheckBg", CheckBgColor, rounded: false);
            ApplyCircleSprite(checkBg);
            UguiRuntimeFactory.Stretch(checkBg.rectTransform);
            checkBg.rectTransform.offsetMin = new Vector2(2f, 2f);
            checkBg.rectTransform.offsetMax = new Vector2(-2f, -2f);

            var checkBtn = checkBorder.gameObject.AddComponent<Button>();
            checkBtn.targetGraphic = checkBorder;
            checkBtn.transition = Selectable.Transition.ColorTint;
            checkBtn.colors = UguiTheme.MakeColorBlock();
            checkBorder.gameObject.AddComponent<PlayClickSfxOnClick>();

            // 체크 표시 — 픽셀 키트 아이콘 (✓ 글리프는 Galmuri11에 없음), 없으면 "V" 폴백
            var catalogForIcon = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            Image checkIcon = null;
            TMP_Text checkLabel = null;
            if (catalogForIcon != null && catalogForIcon.iconCheck != null)
            {
                checkIcon = UguiRuntimeFactory.Icon(checkBorder.transform, catalogForIcon.iconCheck, 30f, 30f);
                var iconRt = checkIcon.rectTransform;
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                checkIcon.gameObject.SetActive(done);
            }
            else
            {
                checkLabel = UguiRuntimeFactory.Label(
                    checkBorder.transform, done ? "V" : "", 26f, CheckColor, TextAlignmentOptions.Center, bold: true);
                UguiRuntimeFactory.Stretch(checkLabel.rectTransform);
            }

            // ── .guide-text-col ──
            var textCol = UguiRuntimeFactory.Container(inner, "TextCol");
            UguiRuntimeFactory.VerticalLayout(textCol.gameObject, 6f);
            UguiRuntimeFactory.Flexible(textCol, 1f);

            var titleLabel = UguiRuntimeFactory.Label(
                textCol, step.title, 28f, done ? TitleDoneColor : TitleColor,
                TextAlignmentOptions.Left, bold: true, wrap: true);
            UguiRuntimeFactory.Preferred(titleLabel, height: 36f);

            var descLabel = UguiRuntimeFactory.Label(
                textCol, step.description, 23f, DescColor,
                TextAlignmentOptions.Left, bold: false, wrap: true);
            UguiRuntimeFactory.Preferred(descLabel, height: 32f);

            // 미완료 단계에만 힌트 노출 (원본과 동일 — 토글 시 갱신하지 않음)
            if (!string.IsNullOrEmpty(step.completionHint) && !done)
            {
                var hintLabel = UguiRuntimeFactory.Label(
                    textCol, step.completionHint, 21f, UguiTheme.GuideHintBlue,
                    TextAlignmentOptions.Left, bold: false, wrap: true);
                UguiRuntimeFactory.Preferred(hintLabel, height: 28f);
            }

            checkBtn.onClick.AddListener(() =>
            {
                if (manager.IsStepCompleted(step.id))
                    manager.UncompleteStep(step.id);
                else
                    manager.CompleteStep(step.id);

                bool nowDone = manager.IsStepCompleted(step.id);
                if (checkIcon != null) checkIcon.gameObject.SetActive(nowDone);
                if (checkLabel != null) checkLabel.text = nowDone ? "V" : "";

                rowGroup.alpha = nowDone ? 0.55f : 1f;
                titleLabel.color = nowDone ? TitleDoneColor : TitleColor;

                RefreshProgress(manager, view);
                onProgressChanged?.Invoke();
            });
        }

        private static void RefreshProgress(TutorialManager manager, GuidePanelView view)
        {
            if (manager == null) return;

            int total = manager.GetSteps().Count;
            int done = manager.GetCompletedCount();

            if (view.progressLabel != null)
                view.progressLabel.text = $"{done} / {total} 완료";

            if (view.progressFill != null)
            {
                float ratio = total > 0 ? (float)done / total : 0f;
                view.progressFill.fillAmount = ratio;   // width % → fillAmount 대응
            }
        }

        // .guide-empty-hint: 26px, 흰색 50%, 중앙 정렬, padding-top 40px
        private static void AddEmptyHint(RectTransform parent, string text)
        {
            var wrap = UguiRuntimeFactory.Container(parent, "EmptyHint");
            UguiRuntimeFactory.VerticalLayout(wrap.gameObject, 0f, new RectOffset(0, 0, 40, 0));
            UguiRuntimeFactory.Label(wrap, text, 26f, EmptyHintColor, TextAlignmentOptions.Center, bold: false, wrap: true);
        }

        // 카탈로그의 원형 스프라이트로 교체 (border-radius: 999px 대응)
        private static void ApplyCircleSprite(Image img)
        {
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog != null && catalog.circle != null)
            {
                img.sprite = catalog.circle;
                img.type = Image.Type.Simple;
            }
        }
    }
}
