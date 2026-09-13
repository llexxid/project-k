using UnityEngine;
using Scripts.Core;
using System.Collections.Generic;

namespace KingdomIdle.UGUI
{
    // Panel_Guide 셸에 TutorialManager 데이터를 채우고 인터랙션 처리 (프리팹 기반).
    // 단계 카드는 Item_GuideStepRow, 빈 상태 힌트는 Item_GuideEmptyHint 프리팹을 Instantiate 한다.
    // (런타임 코드빌드 제거 — 팩토리 코드생성을 참조하지 않는다)
    public static class GuidePanelController
    {
        public static void Populate(GuidePanelView view, System.Action onProgressChanged = null)
        {
if (view == null || view.listContent == null) return;
            var balance = view.GetComponent<BalanceQuestPanel>() ?? view.gameObject.AddComponent<BalanceQuestPanel>();
            balance.Bind(view);
    }

        private static void ShowUpcoming(GuidePanelView view)
        {
            if (view == null) return;
            ClearList(view.listContent);
            if (view.progressFill != null) view.progressFill.transform.parent.gameObject.SetActive(false);
            var quests = QuestManager.Instance;
            var current = quests != null ? quests.GetActiveGuideState() : null;
            var definition = current != null ? quests.GetQuestDefinition(current.QuestId) : null;
            var visited = new HashSet<long>();
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            int count = 0;
            while (definition != null && definition.NextQuestId != 0 && visited.Add(definition.NextQuestId))
            {
                definition = quests.GetQuestDefinition(definition.NextQuestId);
                if (definition == null || definition.Category != eQuestCategory.Guide || catalog == null || catalog.itemGuideStepRow == null) break;
                var go = Object.Instantiate(catalog.itemGuideStepRow, view.listContent, false);
                var row = go.GetComponent<GuideStepRowView>();
                row.Set($"가이드 {definition.QuestId:N0}  ·  예정", definition.Description, "", false);
                if (row.checkButton != null) row.checkButton.gameObject.SetActive(false);
                row.titleLabel.fontSize = 26;
                row.titleLabel.color = UguiTheme.AccentGold;
                row.descLabel.fontSize = 30;
                var layout = row.descLabel.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layout != null) layout.preferredHeight = 76;
                count++;
            }
            if (view.progressLabel != null) view.progressLabel.text = count > 0 ? "다음 가이드" : "가이드 안내";
            if (count == 0) AddEmptyHint(view, current != null ? "마지막 가이드를 진행하고 있습니다." : "현재 등록된 가이드를 모두 확인했습니다.");
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
                view.progressLabel.text = $"플레이 도움말  ·  {done}/{total} 확인";

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
