using UnityEngine;
using Scripts.Core;

namespace KingdomIdle.UGUI
{
    // Panel_Guide 셸에 TutorialManager 데이터를 채우고 인터랙션 처리 (프리팹 기반).
    // 단계 카드는 Item_GuideStepRow, 빈 상태 힌트는 Item_GuideEmptyHint 프리팹을 Instantiate 한다.
    // (런타임 코드빌드 제거 — 팩토리 코드생성을 참조하지 않는다)
    public static class GuidePanelController
    {
        public static void Populate(GuidePanelView view, System.Action onProgressChanged = null)
        {
            if (view == null) return;

            var manager = TutorialManager.Instance;
            if (view.listContent == null) return;

            // 원본은 이벤트를 구독하지 않으므로 OnClosed 에서 해제할 것도 없다.
            // (진행 변경 통지는 onProgressChanged 콜백으로만 전달 — 원본과 동일)

            ClearList(view.listContent);

            if (manager == null)
            {
                AddEmptyHint(view, "TutorialManager를 씬에 배치해주세요.");
                return;
            }

            var steps = manager.GetSteps();

            if (steps.Count == 0)
            {
                AddEmptyHint(view, "등록된 가이드 단계가 없습니다.");
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
            System.Action onProgressChanged)
        {
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.itemGuideStepRow == null) return;

            bool done = manager.IsStepCompleted(step.id);

            var go = Object.Instantiate(catalog.itemGuideStepRow, view.listContent, false);
            var row = go.GetComponent<GuideStepRowView>();
            if (row == null) { Object.Destroy(go); return; }

            row.Set(step.title, step.description, step.completionHint, done);

            if (row.checkButton != null)
            {
                row.checkButton.onClick.AddListener(() =>
                {
                    if (manager.IsStepCompleted(step.id))
                        manager.UncompleteStep(step.id);
                    else
                        manager.CompleteStep(step.id);

                    row.SetDone(manager.IsStepCompleted(step.id));

                    RefreshProgress(manager, view);
                    onProgressChanged?.Invoke();
                });
            }
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

        private static void AddEmptyHint(GuidePanelView view, string text)
        {
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.itemGuideEmptyHint == null) return;

            var go = Object.Instantiate(catalog.itemGuideEmptyHint, view.listContent, false);
            var hint = go.GetComponent<GuideEmptyHintView>();
            if (hint != null) hint.SetText(text);
        }

        // 자식 셀 비활성화 후 파괴 (Destroy 지연이 레이아웃에 끼지 않게 — 마탑 팝업과 동일 관례)
        private static void ClearList(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }
    }
}
