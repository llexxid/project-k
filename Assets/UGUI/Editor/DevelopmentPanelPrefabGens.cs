using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 육성 패널(DevelopmentPanelController) 런타임 코드생성 UI → 프리팹 전환용 생성기.
    /// 고정 구조(설명 라벨 / 보유 골드 바 / 강화 카드 컨테이너 / 빈 상태 라벨)를
    /// Body_Development 프리팹 + DevelopmentBodyView 로 만든다.
    /// 반복되는 강화 카드/버튼은 기존 Item_EnhanceCard / Item_GachaPullButton 프리팹을 재사용한다.
    /// </summary>
    internal static class DevelopmentPanelPrefabGens
    {
        internal static void GenerateAll()
        {
            GenerateBody();
        }

        // ── 본문 셸 ──
        // 스크롤 콘텐츠(VLayout)에 1회 인스턴스화되는 컨테이너.
        // 원본 Refresh()가 content 에 직접 쌓던 순서/치수를 그대로 재현한다:
        //   설명(22px @70% wrap, h60) → 보유 골드(26px gold bold, h44) → 카드들 → 빈 안내(24px @40% center, h60)
        internal static GameObject GenerateBody()
        {
            var root = F.Container(null, "Body_Development");
            F.VLayout(root.gameObject, 10f, null, TextAnchor.UpperLeft, expandWidth: true);
            var view = root.gameObject.AddComponent<DevelopmentBodyView>();

            // 설명 (.ka-dev-desc: 22px @70%)
            var desc = F.Text(root, "Desc",
                "골드를 소비해 모든 캐릭터의 공격력과 체력을 영구 강화합니다.",
                22f, new Color(1f, 1f, 1f, 0.70f), TextAlignmentOptions.Left, wrap: true);
            F.Preferred(desc, height: 60f);
            view.descLabel = desc;

            // 보유 골드 바 (.ka-dev-gold-bar: 26px gold bold) — 컨트롤러가 텍스트만 갱신
            var gold = F.Text(root, "GoldBar", "보유 골드  0 G",
                26f, UguiTheme.AccentGoldStrong, TextAlignmentOptions.Left, bold: true);
            F.Preferred(gold, height: 44f);
            view.goldLabel = gold;

            // 강화 카드 컨테이너 (컨트롤러가 Item_EnhanceCard 를 채운다)
            var cards = F.Container(root, "Cards");
            F.VLayout(cards.gameObject, 10f, null, TextAnchor.UpperLeft, expandWidth: true);
            view.cardsRoot = cards;

            // 빈 상태 라벨 (강화 항목이 없을 때만 표시)
            var empty = F.Text(root, "Empty", "강화 가능한 항목이 없습니다.",
                24f, new Color(1f, 1f, 1f, 0.40f), TextAlignmentOptions.Center);
            F.Preferred(empty, height: 60f);
            view.emptyLabel = empty;
            empty.gameObject.SetActive(false);

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Body_Development.prefab");
        }
    }
}
