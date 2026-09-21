using System;
using System.Linq;
using Direction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>안내 프리팹·데이터·bootstrap 연결만 생성한다. 전체 UI 생성이나 기존 사용자 에셋 덮어쓰기는 하지 않는다.</summary>
    public static class GameDirectPreparation
    {
        public const string MenusId = "first_start_menus";
        public const string ReincarnationId = "first_reincarnation_guide";
        public const string DataRoot = "Assets/_Project/Data/Direction";
        public const string OverlayPath = "Assets/UGUI/Prefabs/Overlays/Overlay_FeatureGuide.prefab";

        /// <summary>실습 SO 다섯 개와 입력 필터를 연결한다. 기존 SO 문구·ID와 bootstrap의 사용자 변경은 보존한다.</summary>
        [MenuItem("KingdomIdle/Direction/Prepare interactive guides")]
        public static void PrepareInteractiveGuides()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
            Prepare();
            var development = GetOrCreate("guide_development", new[] {
                InteractiveStep("open", GameDirectTarget.Development, "육성", "육성 버튼을 눌러 왕국군의 성장 화면을 열어 보세요.", GuideContext.Main, GuideCompletion.Click, GuideContext.Development),
                InteractiveStep("gold", GameDirectTarget.GoldGrowthTab, "골드 강화", "골드 강화는 모든 왕국군의 공격력과 체력을 높입니다. 루비 영구 성장은 별도 탭에서 관리합니다.", GuideContext.Development),
                InteractiveStep("attack_once", GameDirectTarget.AttackOnce, "공격력 1회 강화", "공격력의 1회 강화 버튼을 눌러 보세요. 골드가 부족하면 전투로 모으거나 ‘나중에 계속’을 선택할 수 있습니다.", GuideContext.Development, GuideCompletion.Action, action: GuideAction.AttackOnce)
            });
            var army = GetOrCreate("guide_kingdom_army", new[] {
                InteractiveStep("open", GameDirectTarget.KingdomArmy, "왕국군", "왕국군 버튼을 눌러 편성된 캐릭터를 살펴보세요.", GuideContext.Main, GuideCompletion.Click, GuideContext.ArmyCharacter),
                InteractiveStep("member", GameDirectTarget.ArmyMember, "캐릭터 선택", "각 왕국군 버튼으로 살펴볼 캐릭터를 선택할 수 있습니다. 표시된 캐릭터를 눌러 보세요.", GuideContext.ArmyCharacter, GuideCompletion.Click, GuideContext.ArmyCharacter),
                InteractiveStep("stats", GameDirectTarget.ArmyStats, "종합 · 능력치", "종합에서는 선택한 캐릭터의 직업과 현재 능력치를 확인할 수 있습니다.", GuideContext.ArmyCharacter, scroll: true),
                InteractiveStep("skills", GameDirectTarget.ArmySkills, "종합 · 스킬", "종합 아래에는 현재 캐릭터의 스킬과 효과가 표시됩니다. 화면을 스크롤해서 읽어 보세요.", GuideContext.ArmyCharacter, scroll: true),
                InteractiveStep("equipment_tab", GameDirectTarget.ArmyEquipmentTab, "장비", "장비 탭을 눌러 보유 장비를 확인해 보세요.", GuideContext.ArmyCharacter, GuideCompletion.Click, GuideContext.ArmyEquipment),
                InteractiveStep("equipment", GameDirectTarget.ArmyEquipment, "장비 착용 조건", "보유한 장비를 확인하고 장착할 수 있습니다. 캐릭터의 직업에 맞는 장비만 착용할 수 있으며, 장비가 없어도 안내를 계속할 수 있습니다.", GuideContext.ArmyEquipment, scroll: true),
                InteractiveStep("jobs_tab", GameDirectTarget.ArmyJobsTab, "전직", "전직 탭에서 캐릭터가 선택할 수 있는 직업을 살펴보세요.", GuideContext.ArmyEquipment, GuideCompletion.Click, GuideContext.ArmyJobs),
                InteractiveStep("job_detail", GameDirectTarget.ArmyJobCard, "전직 정보 열기", "표시된 직업을 눌러 상세 정보를 열어 보세요. 정보를 확인하는 것만으로 전직되지는 않습니다.", GuideContext.ArmyJobs, GuideCompletion.Click, GuideContext.ArmyJobDetail, scroll: true),
                InteractiveStep("job_stats", GameDirectTarget.ArmyJobStats, "전직 후 능력치", "전직 상세에서 현재 능력치와 해당 직업으로 바뀐 뒤의 능력치를 비교할 수 있습니다.", GuideContext.ArmyJobDetail, scroll: true),
                InteractiveStep("job_skills", GameDirectTarget.ArmyJobSkills, "전직 후 스킬", "해당 직업에서 사용할 스킬을 확인할 수 있습니다. 실제 전직은 조건과 변화를 확인한 뒤 선택하세요.", GuideContext.ArmyJobDetail, scroll: true)
            });
            var dungeon = GetOrCreate("guide_dungeon", new[] {
                InteractiveStep("open", GameDirectTarget.Dungeon, "던전", "던전 버튼을 눌러 골드 던전과 루비 던전을 확인해 보세요.", GuideContext.Main, GuideCompletion.Click, GuideContext.Dungeon),
                InteractiveStep("gold_open", GameDirectTarget.GoldDungeonCard, "골드 던전", "골드 던전을 눌러 상세 정보를 열어 보세요. 안내 중에는 입장하지 않습니다.", GuideContext.Dungeon, GuideCompletion.Click, GuideContext.GoldDungeonDetail),
                InteractiveStep("gold_info", GameDirectTarget.DungeonDetail, "처치 수에 따른 골드", "골드 던전은 제한 시간 동안 몬스터를 처치한 수에 따라 골드를 얻습니다. 난이도와 보상 정보를 확인할 수 있습니다.", GuideContext.GoldDungeonDetail),
                InteractiveStep("ruby_open", GameDirectTarget.RubyDungeonCard, "루비 던전", "루비 던전을 눌러 골드 던전과 다른 보상 조건을 확인해 보세요.", GuideContext.Dungeon, GuideCompletion.Click, GuideContext.RubyDungeonDetail),
                InteractiveStep("ruby_info", GameDirectTarget.DungeonDetail, "보스 처치로 루비 획득", "루비 던전은 각 제한 시간 안에 보스 3체를 모두 처치하면 루비를 얻습니다. 입장 전 해금 조건과 난이도를 확인하세요.", GuideContext.RubyDungeonDetail),
                InteractiveStep("tickets", GameDirectTarget.GoldDungeonCard, "일일 입장 횟수", "각 던전의 입장권은 하루 {dailyTickets}회분입니다. 클리어가 아닌 입장 시 차감되며, 매일 00시에 충전됩니다.", GuideContext.Dungeon)
            });
            var gacha = GetOrCreate("guide_gacha", new[] {
                InteractiveStep("open", GameDirectTarget.Gacha, "뽑기", "뽑기 버튼을 눌러 장비와 마탑 스킬 뽑기를 체험해 보세요.", GuideContext.Main, GuideCompletion.Click, GuideContext.EquipmentGacha),
                InteractiveStep("equipment_once", GameDirectTarget.EquipmentPullOnce, "장비 1회 뽑기", "체험용 고대주화 50개를 한 번 지급합니다. 장비 1회 뽑기를 눌러 보세요. 기존 확률에 따라 장비 또는 전직 파편을 얻습니다.", GuideContext.EquipmentGacha, GuideCompletion.Action, action: GuideAction.EquipmentPullOnce, grant: true),
                InteractiveStep("equipment_done", GameDirectTarget.EquipmentPullOnce, "장비 뽑기 확인", "획득한 장비는 왕국군의 장비 탭에서 확인하고 착용할 수 있습니다. 전직 파편은 직업 해금에 사용됩니다.", GuideContext.EquipmentGacha),
                InteractiveStep("skill_tab", GameDirectTarget.SkillGachaTab, "마탑 스킬", "마탑 스킬 탭을 눌러 다음 뽑기를 체험해 보세요.", GuideContext.EquipmentGacha, GuideCompletion.Click, GuideContext.SkillGacha),
                InteractiveStep("skill_once", GameDirectTarget.SkillPullOnce, "마탑 스킬 1회 뽑기", "이번 체험용 고대주화 50개를 한 번 지급합니다. 1회 뽑기를 눌러 보세요. 기존 확률에 따라 스킬·스킬 파편 또는 비전 지식을 얻습니다.", GuideContext.SkillGacha, GuideCompletion.Action, action: GuideAction.SkillPullOnce, grant: true),
                InteractiveStep("skill_done", GameDirectTarget.SkillPullOnce, "뽑기 체험 완료", "장비와 마탑 스킬 뽑기를 모두 체험했습니다. 이후에는 보유한 고대주화로 원하는 뽑기를 이용하세요.", GuideContext.SkillGacha)
            });
            ConnectBootstrap(development, army, dungeon, gacha);
            PrepareMageTowerGuide();
            var root = PrefabUtility.LoadPrefabContents(OverlayPath);
            try
            {
                var view = root.GetComponent<FeatureGuideView>();
                var blocker = root.transform.Find("InputBlocker").gameObject;
                view.inputGate = blocker.GetComponent<FeatureGuideInputGate>() ?? blocker.AddComponent<FeatureGuideInputGate>();
                // 카드 빈 곳의 터치가 뒤의 게임 UI로 흘러가지 않게 한다.
                view.card.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
                PrefabUtility.SaveAsPrefabAsset(root, OverlayPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            PlayerSettings.bundleVersion = "0.17.1";
            AssetDatabase.SaveAssets();
            Debug.Log("[GameDirect] 안내 SO 5개·bootstrap·입력 필터 연결 완료 (빌드 없음).");
        }

        /// <summary>편집 모드에서 마탑 설명 SO만 추가하고 카탈로그에 연결한다. 기존 데이터는 유지하며 실제 장착·재화 변경은 요구하지 않는다.</summary>
        [MenuItem("KingdomIdle/Direction/Prepare mage tower guide")]
        public static void PrepareMageTowerGuide()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
            var mage = GetOrCreate("guide_mage_tower", new[] {
                InteractiveStep("open", GameDirectTarget.MageTower, "마탑 스킬", "전장의 마탑을 눌러 스킬 관리 창을 열어 보세요.", GuideContext.Main, GuideCompletion.Click, GuideContext.MageTower),
                InteractiveStep("skills", GameDirectTarget.MageSkillList, "스킬 목록", "마탑 스킬 창에서 보유한 스킬을 확인할 수 있습니다. 아직 얻지 못한 스킬은 뽑기로 획득한 뒤 장착할 수 있습니다.", GuideContext.MageTower, scroll: true),
                InteractiveStep("equip", GameDirectTarget.MageEquipAction, "스킬 장착", "왼쪽 장착 슬롯을 고르고 목록에서 보유 스킬을 선택한 뒤 ‘장착’을 누르면 사용할 수 있습니다. 이미 스킬이 있는 슬롯은 교체할 수 있습니다.", GuideContext.MageTower),
                InteractiveStep("unequip", GameDirectTarget.MageUnequipAction, "스킬 해제", "장착된 슬롯을 선택하고 ‘해제’를 누르면 슬롯에서 스킬을 뺄 수 있습니다. 보유 스킬은 사라지지 않으며 다시 장착할 수 있습니다.", GuideContext.MageTower)
            });
            ConnectBootstrap(mage);
            PlayerSettings.bundleVersion = "0.17.1";
            AssetDatabase.SaveAssets();
        }

        /// <summary>명시적 문맥·조건·허용 대상을 가진 최초 SO 데이터를 만든다. 경제 동작은 Action ID로만 연결한다.</summary>
        private static GameDirectStepData InteractiveStep(string id, GameDirectTarget target, string title, string description,
            GuideContext context, GuideCompletion completion = GuideCompletion.Confirm, GuideContext destination = GuideContext.Main,
            GuideAction action = GuideAction.None, bool scroll = false, bool grant = false) => new()
        {
            id = id, target = target, title = title, description = description, context = context,
            completion = completion, destination = destination, action = action, allowScroll = scroll,
            grantPracticeCoins = grant, placement = GuideCardPlacement.Auto,
            allowedTargets = completion == GuideCompletion.Confirm ? Array.Empty<GameDirectTarget>() : new[] { target }
        };

        /// <summary>편집 모드에서 최초 구성 또는 누락 연결만 복구한다. 기존 데이터·프리팹의 수동 편집을 보존한다.</summary>
        [MenuItem("KingdomIdle/Direction/Prepare guide foundation")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
            PrefabGenUtil.EnsureFolder(DataRoot);
            var catalog = PrefabGenUtil.GetOrCreateCatalog();
            var menus = GetOrCreate(MenusId, new[]
            {
                Step("development", GameDirectTarget.Development, "육성", "골드 강화와 루비 영구 성장으로 왕국군을 강화합니다.", GuideCardPlacement.Above),
                Step("kingdom_army", GameDirectTarget.KingdomArmy, "왕국군", "병사의 전직과 장비를 관리합니다.", GuideCardPlacement.Above),
                Step("dungeon", GameDirectTarget.Dungeon, "던전", "던전에 도전해 성장에 필요한 재화를 획득합니다.", GuideCardPlacement.Above),
                Step("gacha", GameDirectTarget.Gacha, "뽑기", "장비와 마탑 스킬을 획득합니다.", GuideCardPlacement.Above)
            });
            var reincarnation = GetOrCreate(ReincarnationId, new[]
            {
                Step("reincarnation", GameDirectTarget.Reincarnation, "환생", "환생은 다시 도전하며 성장하는 기능입니다.\n환생 화면에서 현재 조건과 실행 후 변화를 확인하세요.", GuideCardPlacement.Below)
            });
            var overlay = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPath);
            if (overlay == null) overlay = CreateOverlay(catalog);
            catalog.overlayFeatureGuide = overlay;
            EditorUtility.SetDirty(catalog);
            ConnectBootstrap(menus, reincarnation);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameDirect] 안내 데이터 2종·오버레이·bootstrap 연결 완료. 자동 발생 조건은 연결하지 않았습니다.");
        }

        /// <summary>초기 에셋에만 사용할 단계 정의를 만든다. 런타임 상태는 포함하지 않는다.</summary>
        private static GameDirectStepData Step(string id, GameDirectTarget target, string title, string description, GuideCardPlacement placement)
            => new() { id = id, target = target, title = title, description = description, placement = placement };

        /// <summary>동일 경로의 SO가 있으면 그대로 재사용한다. 생성기의 재실행이 편집한 문구와 ID를 초기화하지 않는다.</summary>
        private static GameDirectSequenceSO GetOrCreate(string id, GameDirectStepData[] steps)
        {
            string path = $"{DataRoot}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<GameDirectSequenceSO>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<GameDirectSequenceSO>();
            asset.sequenceId = id; asset.steps = steps;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>bootstrap의 기존 영속 GameManager에 컴포넌트를 추가한다. 열린 씬의 미저장 편집은 자동 저장하지 않는다.</summary>
        private static void ConnectBootstrap(params GameDirectSequenceSO[] definitions)
        {
            const string path = "Assets/_Project/Scenes/buildScenes/bootstrap.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("bootstrap의 미저장 변경을 먼저 저장한 뒤 다시 실행하세요.");
                var game = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Scripts.Core.GameManager>(true)).Single();
                var manager = game.GetComponent<GameDirectManager>();
                if (manager == null) manager = Undo.AddComponent<GameDirectManager>(game.gameObject);
                var serialized = new SerializedObject(manager);
                var array = serialized.FindProperty("sequences");
                foreach (var definition in definitions)
                {
                    bool exists = false;
                    for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == definition) exists = true;
                    if (exists) continue;
                    int index = array.arraySize++;
                    array.GetArrayElementAtIndex(index).objectReferenceValue = definition;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        /// <summary>기존 카탈로그 폰트·패널을 사용하는 프리팹을 만든다. 새 텍스처나 개별 TMP 머티리얼은 생성하지 않는다.</summary>
        private static GameObject CreateOverlay(UIViewCatalog catalog)
        {
            var root = Rect("Overlay_FeatureGuide", null);
            Stretch(root);
            var view = root.gameObject.AddComponent<FeatureGuideView>();
            view.inputGroup = root.gameObject.AddComponent<CanvasGroup>();
            // 시각적인 구멍에도 투명 입력 막은 남긴다. 실제 메뉴를 누르게 하는 안내는 후속 단계에서 별도 정책으로 확장한다.
            var blocker = Box("InputBlocker", root, Color.clear);
            blocker.raycastTarget = true;
            Stretch(blocker.rectTransform);
            view.dimPanels = new RectTransform[4];
            for (int i = 0; i < 4; i++) view.dimPanels[i] = Box("Dim" + i, root, UguiTheme.DimMedium).rectTransform;
            view.highlight = Rect("Highlight", root);
            Border(view.highlight, UguiTheme.BronzeLight, 4);
            view.card = Box("Card", root, UguiTheme.RusticPanel).rectTransform;
            Border(view.card, UguiTheme.Bronze, 3);
            view.progressLabel = Label("Progress", view.card, catalog, 25, UguiTheme.BronzeLight);
            SetAnchors(view.progressLabel.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(36, -52), new Vector2(-36, -20));
            view.titleLabel = Label("Title", view.card, catalog, 38, UguiTheme.Parchment);
            SetAnchors(view.titleLabel.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(36, -104), new Vector2(-36, -54));
            view.descriptionLabel = Label("Description", view.card, catalog, 36, UguiTheme.Parchment);
            SetAnchors(view.descriptionLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(36, 176), new Vector2(-36, -116));
            view.descriptionLabel.alignment = TextAlignmentOptions.TopLeft;
            view.descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
            view.nextButton = Button("Next", view.card, catalog, "다음", out view.nextLabel);
            SetAnchors(view.nextButton.transform as RectTransform, new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(10, 24), new Vector2(-28, 156));
            view.skipButton = Button("Skip", view.card, catalog, "건너뛰기", out _);
            SetAnchors(view.skipButton.transform as RectTransform, Vector2.zero, new Vector2(.5f, 0), new Vector2(28, 24), new Vector2(-10, 156));
            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(root.gameObject, OverlayPath);
        }

        /// <summary>Editor에서만 임시 RectTransform을 만든다. 런타임은 저장된 프리팹을 인스턴스화한다.</summary>
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>균일한 평면 색 사각형을 만든다. 기본은 터치를 받지 않고 입력 막과 버튼만 별도로 허용한다.</summary>
        private static UnityEngine.UI.Image Box(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        /// <summary>원본 버튼과 공용 아트를 변경하지 않고 네 개의 얇은 사각형으로 테두리를 구성한다.</summary>
        private static void Border(RectTransform parent, Color color, float thickness)
        {
            SetAnchors(Box("Top", parent, color).rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -thickness), Vector2.zero);
            SetAnchors(Box("Bottom", parent, color).rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, thickness));
            SetAnchors(Box("Left", parent, color).rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(thickness, 0));
            SetAnchors(Box("Right", parent, color).rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-thickness, 0), Vector2.zero);
        }

        /// <summary>카탈로그의 공유 글꼴과 기본 머티리얼을 참조하는 텍스트를 만든다.</summary>
        private static TMP_Text Label(string name, Transform parent, UIViewCatalog catalog, float size, Color color)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = catalog.defaultFont; label.fontSize = size; label.color = color;
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        /// <summary>충분한 모바일 터치 영역을 갖는 안내 버튼을 만든다. 공유 스프라이트는 복사·수정하지 않는다.</summary>
        private static UnityEngine.UI.Button Button(string name, Transform parent, UIViewCatalog catalog, string text, out TMP_Text label)
        {
            var image = Box(name, parent, UguiTheme.RusticSurface);
            image.raycastTarget = true;
            image.sprite = catalog.kitBtnGrey; image.type = UnityEngine.UI.Image.Type.Sliced;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            Border(image.rectTransform, UguiTheme.Bronze, 2);
            label = Label("Label", image.transform, catalog, 32, UguiTheme.Parchment);
            label.text = text; label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>생성 단계의 고정 앵커/오프셋을 적용한다. 실행 중 실제 카드 위치는 View가 계산한다.</summary>
        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }

        /// <summary>부모 전체에 맞추며 새로운 CanvasScaler나 SafeArea 설정을 만들지 않는다.</summary>
        private static void Stretch(RectTransform rect) => SetAnchors(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        /// <summary>현재 게임 화면에서 네 메뉴 설명을 미리본다. 실제 계정의 진행 기록은 읽거나 쓰지 않는다.</summary>
        [MenuItem("KingdomIdle/Direction/Preview/First start menus")]
        public static void PreviewMenus() => Preview(MenusId);

        /// <summary>현재 게임 화면에서 상단 환생 버튼 설명을 미리본다. 환생 요청이나 재화 변경은 발생하지 않는다.</summary>
        [MenuItem("KingdomIdle/Direction/Preview/Reincarnation")]
        public static void PreviewReincarnation() => Preview(ReincarnationId);

        /// <summary>미리보기 입력 오류를 콘솔에 명확히 알린다. Editor 명령은 게임 진행 이벤트를 대신 발행하지 않는다.</summary>
        private static void Preview(string id)
        {
            var manager = GameDirectManager.Instance;
            if (!EditorApplication.isPlaying || manager == null) { Debug.LogWarning("bootstrap부터 Play 후 메인 화면에서 실행하세요."); return; }
            if (!manager.RequestPlay(id, true)) Debug.LogWarning(manager.LastError);
        }

        /// <summary>현재 수동 실행을 완료 기록 없이 취소한다. 입력 막은 Player의 정리 경로에서 해제된다.</summary>
        [MenuItem("KingdomIdle/Direction/Preview/Cancel current")]
        public static void CancelPreview() => GameDirectManager.Instance?.CancelCurrent();
    }
}
