using System;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Core;

namespace KingdomIdle.UIToolkit
{
    // Panel_Guide.uxml에 TutorialManager 데이터를 채우고 인터랙션 처리
    public static class UITKGuidePanelController
    {
        public static void Populate(VisualElement panelRoot, Action onProgressChanged = null)
        {
            if (panelRoot == null) return;

            var manager = TutorialManager.Instance;
            var scrollView = panelRoot.Q<ScrollView>("GuideScrollView");
            var progressFill = panelRoot.Q<VisualElement>("GuideProgressFill");
            var progressLabel = panelRoot.Q<Label>("LblGuideProgress");

            if (scrollView == null) return;
            scrollView.Clear();

            if (manager == null)
            {
                var hint = new Label("TutorialManager를 씬에 배치해주세요.");
                hint.AddToClassList("guide-empty-hint");
                scrollView.Add(hint);
                return;
            }

            var steps = manager.GetSteps();

            if (steps.Count == 0)
            {
                var hint = new Label("등록된 가이드 단계가 없습니다.");
                hint.AddToClassList("guide-empty-hint");
                scrollView.Add(hint);
                RefreshProgress(manager, progressFill, progressLabel);
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) continue;
                scrollView.Add(BuildStepRow(step, manager, progressFill, progressLabel, onProgressChanged));
            }

            RefreshProgress(manager, progressFill, progressLabel);
        }

        private static VisualElement BuildStepRow(
            TutorialStepDataSO step,
            TutorialManager manager,
            VisualElement progressFill,
            Label progressLabel,
            Action onProgressChanged)
        {
            bool done = manager.IsStepCompleted(step.id);

            var row = new VisualElement();
            row.AddToClassList("guide-step-row");
            if (done) row.AddToClassList("guide-step-done");

            var checkBtn = new Button();
            checkBtn.AddToClassList("guide-check-btn");
            checkBtn.text = done ? "✓" : "";
            checkBtn.pickingMode = PickingMode.Position;

            var textCol = new VisualElement();
            textCol.AddToClassList("guide-text-col");
            textCol.pickingMode = PickingMode.Ignore;

            var titleLabel = new Label(step.title);
            titleLabel.AddToClassList("guide-step-title");
            if (done) titleLabel.AddToClassList("guide-step-title-done");

            var descLabel = new Label(step.description);
            descLabel.AddToClassList("guide-step-desc");

            textCol.Add(titleLabel);
            textCol.Add(descLabel);

            if (!string.IsNullOrEmpty(step.completionHint) && !done)
            {
                var hintLabel = new Label(step.completionHint);
                hintLabel.AddToClassList("guide-step-hint");
                textCol.Add(hintLabel);
            }

            row.Add(checkBtn);
            row.Add(textCol);

            checkBtn.clicked += () =>
            {
                if (manager.IsStepCompleted(step.id))
                    manager.UncompleteStep(step.id);
                else
                    manager.CompleteStep(step.id);

                bool nowDone = manager.IsStepCompleted(step.id);
                checkBtn.text = nowDone ? "✓" : "";

                if (nowDone)
                {
                    row.AddToClassList("guide-step-done");
                    titleLabel.AddToClassList("guide-step-title-done");
                }
                else
                {
                    row.RemoveFromClassList("guide-step-done");
                    titleLabel.RemoveFromClassList("guide-step-title-done");
                }

                RefreshProgress(manager, progressFill, progressLabel);
                onProgressChanged?.Invoke();
            };

            return row;
        }

        private static void RefreshProgress(
            TutorialManager manager,
            VisualElement progressFill,
            Label progressLabel)
        {
            if (manager == null) return;

            int total = manager.GetSteps().Count;
            int done = manager.GetCompletedCount();

            if (progressLabel != null)
                progressLabel.text = $"{done} / {total} 완료";

            if (progressFill != null)
            {
                float ratio = total > 0 ? (float)done / total : 0f;
                progressFill.style.width = new Length(ratio * 100f, LengthUnit.Percent);
            }
        }
    }
}
