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
