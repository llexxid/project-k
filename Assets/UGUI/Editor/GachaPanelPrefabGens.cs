using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 런타임 코드생성 UI → 프리팹 전환용 생성기 (뽑기 패널).
    /// GachaPanelController 가 탭 전환 때마다 _view.content 아래에 1개 인스턴스화하는
    /// 탭 콘텐츠 셸(GachaTabContent = GachaTabContentView) 을 만든다.
    /// 반복 위젯(확률 알약·뽑기 버튼·보상 카드)은 기존 item 프리팹을 그대로 재사용하므로
    /// 여기서는 그 부모 컨테이너(rateRow/pullRow/rewardGrid)와 고정 라벨만 배선한다.
    /// </summary>
    internal static class GachaPanelPrefabGens
    {
        // 원본 컨트롤러가 쓰던 색상 (UguiRuntimeFactory 코드 생성 시절과 동일 값)
        private static readonly Color DescColor = new Color(1f, 1f, 1f, 0.70f);
        private static readonly Color CostColor = new Color(1f, 204f / 255f, 0f, 0.95f);
        private static readonly Color SectionColor = new Color(1f, 1f, 1f, 0.80f);

        internal static void GenerateAll()
        {
            GenerateGachaTabContent();
        }

        // ── 탭 콘텐츠 셸 ──
        internal static GameObject GenerateGachaTabContent()
        {
            var root = F.Container(null, "GachaTabContent");
            var view = root.gameObject.AddComponent<GachaTabContentView>();
            // content(_view.content) 의 VerticalLayout(spacing 10) 아래 단일 자식으로 들어가며,
            // 자체 VerticalLayout(spacing 10) 으로 내부 고정 구조를 세로로 쌓는다.
            F.VLayout(root.gameObject, 10f, null, TextAnchor.UpperLeft);

            // 전체 안내 메시지 (매니저/테이블 없음 등) — 일반 콘텐츠일 땐 컨트롤러가 숨긴다.
            var message = F.Text(root, "Message", "", 24f, UguiTheme.TextSecondary,
                TextAlignmentOptions.Center, wrap: true);
            F.Preferred(message, height: 60f);
            view.messageLabel = message;
            message.gameObject.SetActive(false);

            // 설명 (.gacha-desc: 26px @70%)
            var desc = F.Text(root, "Desc", "", 26f, DescColor, TextAlignmentOptions.Left, wrap: true);
            F.Preferred(desc, height: 70f);
            view.descLabel = desc;

            // 보유/비용 바 (.gacha-cost: 26px gold)
            var cost = F.Text(root, "Cost", "", 26f, CostColor, TextAlignmentOptions.Left, bold: true);
            F.Preferred(cost, height: 40f);
            view.costLabel = cost;

            // 확률 요약 알약 행 (Item_RatePill 부모).
            // 알약이 자체 ContentSizeFitter 폭을 쓰도록 childControlWidth=false.
            var rateRow = F.Container(root, "RateRow");
            F.HLayout(rateRow.gameObject, 8f, null, TextAnchor.MiddleLeft, childControlWidth: false);
            F.Preferred(rateRow, height: 50f);
            view.rateRow = rateRow;

            // 뽑기 버튼 행 (Item_GachaPullButton 부모). 버튼을 균등하게 채운다.
            var pullRow = F.Container(root, "PullRow");
            F.HLayout(pullRow.gameObject, 14f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(pullRow, height: 140f);
            view.pullRow = pullRow;

            // 보상 섹션 타이틀
            var section = F.Text(root, "RewardSectionTitle", "획득 가능 보상", 26f, SectionColor,
                TextAlignmentOptions.Left, bold: true);
            F.Preferred(section, height: 38f);
            view.rewardSectionTitle = section;

            // 보상 카드 그리드 (Item_GachaCard 부모)
            var grid = F.Container(root, "RewardGrid");
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(160f, 200f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;
            view.rewardGrid = grid;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/GachaTabContent.prefab");
        }
    }
}
