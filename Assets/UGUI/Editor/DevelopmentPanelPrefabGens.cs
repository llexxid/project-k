using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 육성 패널(DevelopmentPanelController) 런타임 코드생성 UI → 프리팹 전환용 생성기.
    /// 고정 구조(설명 라벨 / 보유 골드 바 / 강화 카드 컨테이너 / 빈 상태 라벨)를
    /// Body_Development 프리팹 + DevelopmentBodyView 로 만든다.
    /// 반복되는 강화 카드/버튼은 기존 Item_EnhanceCard / Item_GachaPullButton 프리팹을 재사용한다.
    /// </summary>
    public static class DevelopmentPanelPrefabGens
    {
        internal static void GenerateAll()
        {
            GenerateBody();
            ApplyEnhanceCard();
        }

        /// <summary>육성 소유 자산만 갱신한다. 공용 카탈로그·열린 씬은 저장하지 않는다.</summary>
        [MenuItem("KingdomIdle/UGUI/Apply bronze growth")]
        public static void ApplyBronzeGrowth()
        {
            F.Init();
            F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            GenerateAll();
        }

        public static object ValidateBronzeGrowth()
        {
            var checks=new System.Collections.Generic.List<string>();
            void Check(bool value,string message)
            {
                if(!value)throw new System.InvalidOperationException("Bronze growth: "+message);
                checks.Add(message);
            }
            var body=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Panels/Body_Development.prefab").GetComponent<DevelopmentBodyView>();
            Check(body.navBar!=null && body.purchaseCountBar!=null && body.cardsRoot!=null && body.rubyCardsRoot!=null,
                "Growth tabs, gold count selector and both card roots are serialized");
            Check(body.goldLabel.fontSize>=42 && body.descLabel.fontSize>=36,
                "Balance and guidance use readable mobile text sizes");
            Check(body.goldLabel.color==UguiTheme.TextPrimary && body.descLabel.color==UguiTheme.TextSecondary &&
                body.emptyLabel.color==UguiTheme.TextSecondary,
                "Text outside the light cards remains readable on the dark growth shell");
            var card=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Items/Item_EnhanceCard.prefab").GetComponent<EnhanceCardView>();
            Check(card.nameLabel.fontSize>=42 && card.bonusLabel.fontSize>=40 && card.levelLabel.fontSize>=36,
                "Stat, current-to-next effect and level retain their information hierarchy");
            Check(card.ButtonRow.anchorMin.x==1 && card.ButtonRow.anchorMax.x==1 && card.ButtonRow.rect.width>=290,
                "Each card reserves a large right-side primary action");
            Check(card.ButtonRow.childCount==0 && card.transform.Find("InfoSurface")!=null,
                "Card template has no obsolete purchase/reset buttons and has a light information surface");
            return new{passed=checks.Count,checks};
        }

        // ── 본문 셸 ──
        // 스크롤 콘텐츠(VLayout)에 1회 인스턴스화되는 컨테이너.
        // 원본 Refresh()가 content 에 직접 쌓던 순서/치수를 그대로 재현한다:
        //   설명(22px @70% wrap, h60) → 보유 골드(26px gold bold, h44) → 카드들 → 빈 안내(24px @40% center, h60)
        internal static GameObject GenerateBody()
        {
            var root = F.Container(null, "Body_Development");
            F.VLayout(root.gameObject, 16f, new RectOffset(8,8,8,16), TextAnchor.UpperLeft, expandWidth: true);
            var view = root.gameObject.AddComponent<DevelopmentBodyView>();
            AddGrowthTabs(view);

            // 설명 (.ka-dev-desc: 22px @70%)
            var desc = F.Text(root, "Desc",
                "골드로 모든 왕국군의 공격력과 체력을 강화합니다.",
                36f, UguiTheme.TextSecondary, TextAlignmentOptions.Left, wrap: true);
            UseTextHeight(desc);
            view.descLabel = desc;

            // 보유 골드 바 (.ka-dev-gold-bar: 26px gold bold) — 컨트롤러가 텍스트만 갱신
            var gold = F.Text(root, "GoldBar", "보유 골드  0 G",
                42f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true, wrap: true);
            UseTextHeight(gold);
            view.goldLabel = gold;
            if(gold.font!=null)gold.fontSharedMaterial=gold.font.material;

            var counts = F.Container(root,"PurchaseCounts");
            F.HLayout(counts.gameObject,12,expandWidth:true);
            F.Preferred(counts,height:DevelopmentPanelController.PurchaseCountHeight);
            view.purchaseCountBar=counts;

            // 강화 카드 컨테이너 (컨트롤러가 Item_EnhanceCard 를 채운다)
            var cards = F.Container(root, "Cards");
            F.VLayout(cards.gameObject, 16f, null, TextAnchor.UpperLeft, expandWidth: true);
            view.cardsRoot = cards;
            var rubyCards=F.Container(root,"RubyCards");F.VLayout(rubyCards.gameObject,16,expandWidth:true);
            view.rubyCardsRoot=rubyCards;

            // 빈 상태 라벨 (강화 항목이 없을 때만 표시)
            var empty = F.Text(root, "Empty", "강화 가능한 항목이 없습니다.",
                36f, UguiTheme.TextSecondary, TextAlignmentOptions.Center);
            UseTextHeight(empty);
            view.emptyLabel = empty;
            empty.gameObject.SetActive(false);

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Panels/Body_Development.prefab");
        }

        internal static void AddGrowthTabs(DevelopmentBodyView view)
        {
            var nav=F.Container(view.transform,"GrowthTabs");F.HLayout(nav.gameObject,12,expandWidth:true);
            F.Preferred(nav,height:DevelopmentPanelController.GrowthTabHeight);view.navBar=nav;nav.SetAsFirstSibling();
        }

        private static void ApplyEnhanceCard()
        {
            const string path = "Assets/UGUI/Prefabs/Items/Item_EnhanceCard.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<EnhanceCardView>();
                foreach (var layout in root.GetComponentsInChildren<LayoutGroup>(true)) layout.enabled = false;
                var height = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
                height.minHeight = height.preferredHeight = 216;
                var rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");
                var background = root.GetComponent<Image>();
                background.sprite=rounded; background.type=Image.Type.Sliced;
                background.color=UguiTheme.Bronze; background.raycastTarget=false;
                var surface = root.transform.Find("InfoSurface") as RectTransform;
                if (surface == null) surface=F.Container(root.transform,"InfoSurface");
                var surfaceImage=surface.GetComponent<Image>() ?? surface.gameObject.AddComponent<Image>();
                surfaceImage.sprite=rounded;surfaceImage.type=Image.Type.Sliced;surfaceImage.color=UguiTheme.InfoSurface;
                surfaceImage.raycastTarget=false;
                F.Stretch(surface); surface.offsetMin=new Vector2(4,4);surface.offsetMax=new Vector2(-4,-4);surface.SetAsFirstSibling();
                view.accent.color=UguiTheme.RoyalBlue;
                var accentRect=view.accent.rectTransform;
                accentRect.anchorMin=Vector2.zero;accentRect.anchorMax=Vector2.up;accentRect.pivot=new Vector2(0,.5f);
                accentRect.sizeDelta=new Vector2(8,-32);accentRect.anchoredPosition=new Vector2(4,0);
                var header=(RectTransform)view.nameLabel.transform.parent;
                F.Stretch(header); header.offsetMin=new Vector2(24,16);header.offsetMax=new Vector2(-360,-16);
                Pin(view.nameLabel,0,64,44,UguiTheme.InfoInk);
                view.nameLabel.rectTransform.offsetMax = new Vector2(-220, view.nameLabel.rectTransform.offsetMax.y);
                Pin(view.levelLabel,0,64,36,UguiTheme.InfoMuted);
                var levelRect = view.levelLabel.rectTransform;
                levelRect.anchorMin = levelRect.anchorMax = Vector2.one; levelRect.pivot = Vector2.one;
                levelRect.anchoredPosition = Vector2.zero; levelRect.sizeDelta = new Vector2(208,64);
                view.levelLabel.alignment = TextAlignmentOptions.MidlineRight;
                view.bonusLabel.transform.SetParent(header,false);
                Pin(view.bonusLabel,72,112,40,UguiTheme.InfoInk);
                view.bonusLabel.textWrappingMode=TextWrappingModes.Normal;
                var buttonRect=view.ButtonRow;
                buttonRect.anchorMin=new Vector2(1,0);buttonRect.anchorMax=Vector2.one;buttonRect.pivot=new Vector2(1,.5f);
                buttonRect.sizeDelta=new Vector2(320,-48);buttonRect.anchoredPosition=new Vector2(-24,0);
                var buttonLayout=buttonRect.GetComponent<HorizontalLayoutGroup>();
                buttonLayout.enabled=true;buttonLayout.padding=new RectOffset();buttonLayout.spacing=0;
                buttonLayout.childControlWidth=buttonLayout.childControlHeight=true;
                buttonLayout.childForceExpandWidth=buttonLayout.childForceExpandHeight=true;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // TMP의 실제 줄 수를 사용한다. 비조작 안내/지갑에 두 줄짜리 빈 블록을 예약하지 않는다.
        private static void UseTextHeight(TMP_Text label)
        {
            var layout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = -1f;
            layout.flexibleHeight = 0f;
        }

        private static void Pin(TMP_Text label,float top,float height,float size,Color color)
        {
            var rect=label.rectTransform;
            rect.anchorMin=Vector2.up;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-top);rect.sizeDelta=new Vector2(0,height);
            label.fontSize=size;label.enableAutoSizing=false;label.color=color;
            if(label.font!=null)label.fontSharedMaterial=label.font.material;
            label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
        }
    }
}
