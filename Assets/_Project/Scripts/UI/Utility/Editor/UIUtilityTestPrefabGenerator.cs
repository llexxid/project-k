using KingdomIdle.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 네 UI 클릭 유틸리티를 Inspector와 Play Mode에서 독립적으로 확인할 수 있는 테스트 프리팹을 생성한다.
    /// 생성 결과는 실제 게임 프리팹과 분리된 UtilityTests 폴더에 저장하며 메뉴를 다시 실행해도 같은 경로를 갱신한다.
    /// </summary>
    public static class UIUtilityTestPrefabGenerator
    {
        /// <summary>테스트 프리팹을 저장할 프로젝트 상대 경로다.</summary>
        private const string TestPrefabRoot = "Assets/_Project/Prefabs/UI/UtilityTests";

        /// <summary>테스트 프리팹의 안내 문구에 사용할 프로젝트 기본 TMP 폰트 경로다.</summary>
        private const string TestFontPath = "Assets/UGUI/Art/Font/Galmuri11 SDF.asset";

        /// <summary>테스트 카드 배경색이다.</summary>
        private static readonly Color CardColor = new(0.10f, 0.12f, 0.18f, 0.96f);

        /// <summary>클릭 가능한 테스트 버튼의 기본색이다.</summary>
        private static readonly Color ButtonColor = new(0.22f, 0.42f, 0.72f, 1f);

        /// <summary>표시·숨김 결과를 눈으로 확인할 대상 패널의 색이다.</summary>
        private static readonly Color TargetColor = new(0.22f, 0.68f, 0.38f, 1f);

        /// <summary>테스트 프리팹을 생성할 때 재사용하는 TMP 폰트 에셋이다.</summary>
        private static TMP_FontAsset testFont;

        /// <summary>메뉴 또는 배치 실행에서 네 테스트 프리팹을 모두 생성한다.</summary>
        [MenuItem("KingdomIdle/UGUI/Utility/Generate Test Prefabs", false, 60)]
        public static void GenerateAll()
        {
            // 테스트 라벨이 프로젝트 UI와 같은 폰트를 사용하도록 기존 에셋을 읽기만 한다.
            testFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TestFontPath);

            // 각 프리팹은 한 유틸리티만 포함해 동작 실패 원인을 독립적으로 확인할 수 있게 한다.
            GenerateOpenPanelTest();
            GenerateShowTargetTest();
            GenerateHideTargetTest();
            GeneratePopPanelTest();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UI Utility] 테스트 프리팹 4개를 생성했습니다: {TestPrefabRoot}");
        }

        /// <summary>Inventory 패널 열기를 요청하는 OpenPanelOnClick 테스트 프리팹을 생성한다.</summary>
        private static void GenerateOpenPanelTest()
        {
            RectTransform root = CreateTestCard(
                "Test_OpenPanelOnClick",
                "OpenPanelOnClick",
                "UIManager가 있는 실행 씬에서 Inventory 패널이 열리는지 확인합니다.");

            Button button = CreateButton(root, "OpenInventoryButton", "Inventory 패널 열기");
            OpenPanelOnClick utility = button.gameObject.AddComponent<OpenPanelOnClick>();

            // private 직렬화 필드는 런타임 공개 API를 늘리지 않고 SerializedObject로 테스트 설정을 저장한다.
            SerializedObject serializedUtility = new(utility);
            serializedUtility.FindProperty("panelId").intValue = (int)UIPanelId.Inventory;
            serializedUtility.FindProperty("clearBefore").boolValue = false;
            serializedUtility.FindProperty("isTabPanel").boolValue = false;
            serializedUtility.ApplyModifiedPropertiesWithoutUndo();

            SaveTestPrefab(root, "Test_OpenPanelOnClick.prefab");
        }

        /// <summary>비활성 대상을 표시하는 ShowTargetOnClick 테스트 프리팹을 생성한다.</summary>
        private static void GenerateShowTargetTest()
        {
            RectTransform root = CreateTestCard(
                "Test_ShowTargetOnClick",
                "ShowTargetOnClick",
                "버튼을 누르면 숨겨진 초록색 TargetPanel이 나타납니다.");

            GameObject target = CreateTargetPanel(root, "TargetPanel", new Vector2(0f, 8f));
            Button button = CreateButton(root, "ShowTargetButton", "TargetPanel 표시");
            ShowTargetOnClick utility = button.gameObject.AddComponent<ShowTargetOnClick>();
            AssignTarget(utility, target);

            // 프리팹을 처음 배치했을 때 표시 동작을 직접 확인할 수 있도록 대상만 비활성 상태로 저장한다.
            target.SetActive(false);
            SaveTestPrefab(root, "Test_ShowTargetOnClick.prefab");
        }

        /// <summary>활성 대상을 숨기는 HideTargetOnClick 테스트 프리팹을 생성한다.</summary>
        private static void GenerateHideTargetTest()
        {
            RectTransform root = CreateTestCard(
                "Test_HideTargetOnClick",
                "HideTargetOnClick",
                "버튼을 누르면 보이는 초록색 TargetPanel이 사라집니다.");

            GameObject target = CreateTargetPanel(root, "TargetPanel", new Vector2(0f, 8f));
            Button button = CreateButton(root, "HideTargetButton", "TargetPanel 숨기기");
            HideTargetOnClick utility = button.gameObject.AddComponent<HideTargetOnClick>();
            AssignTarget(utility, target);

            SaveTestPrefab(root, "Test_HideTargetOnClick.prefab");
        }

        /// <summary>UIManager의 최상단 패널을 닫는 PopPanelOnClick 테스트 프리팹을 생성한다.</summary>
        private static void GeneratePopPanelTest()
        {
            RectTransform root = CreateTestCard(
                "Test_PopPanelOnClick",
                "PopPanelOnClick",
                "UIManager 패널을 먼저 연 뒤 버튼을 눌러 최상단 패널이 닫히는지 확인합니다.");

            Button button = CreateButton(root, "PopPanelButton", "최상단 패널 닫기");
            button.gameObject.AddComponent<PopPanelOnClick>();

            SaveTestPrefab(root, "Test_PopPanelOnClick.prefab");
        }

        /// <summary>테스트 제목, 안내 문구, 배경을 가진 공통 카드 루트를 만든다.</summary>
        /// <param name="name">프리팹과 Hierarchy에서 식별할 루트 이름이다.</param>
        /// <param name="title">카드 상단에 표시할 유틸리티 이름이다.</param>
        /// <param name="description">Play Mode 확인 방법을 설명하는 안내 문구다.</param>
        /// <returns>테스트 구성 요소를 배치할 카드의 RectTransform이다.</returns>
        private static RectTransform CreateTestCard(string name, string title, string description)
        {
            GameObject rootObject = new(name, typeof(RectTransform), typeof(Image));
            rootObject.layer = 5;

            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(720f, 360f);

            Image background = rootObject.GetComponent<Image>();
            background.color = CardColor;
            background.raycastTarget = false;

            CreateLabel(root, "Title", title, 34f, new Vector2(0f, 126f), new Vector2(650f, 52f));
            CreateLabel(root, "Description", description, 22f, new Vector2(0f, 82f), new Vector2(650f, 54f));
            return root;
        }

        /// <summary>테스트 유틸리티를 부착할 시각적 버튼을 만든다.</summary>
        /// <param name="parent">버튼이 배치될 테스트 카드다.</param>
        /// <param name="name">Hierarchy에서 식별할 버튼 이름이다.</param>
        /// <param name="label">버튼 중앙에 표시할 동작 설명이다.</param>
        /// <returns>유틸리티 컴포넌트를 부착할 Button이다.</returns>
        private static Button CreateButton(Transform parent, string name, string label)
        {
            GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.layer = 5;
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -112f);
            rect.sizeDelta = new Vector2(360f, 82f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            CreateLabel(rect, "Label", label, 26f, Vector2.zero, rect.sizeDelta);
            return button;
        }

        /// <summary>Show/Hide 테스트에서 상태 변화를 눈으로 확인할 초록색 패널을 만든다.</summary>
        /// <param name="parent">대상 패널이 배치될 테스트 카드다.</param>
        /// <param name="name">Hierarchy에서 식별할 대상 이름이다.</param>
        /// <param name="position">카드 중심을 기준으로 한 위치다.</param>
        /// <returns>유틸리티의 target 필드에 연결할 게임 오브젝트다.</returns>
        private static GameObject CreateTargetPanel(Transform parent, string name, Vector2 position)
        {
            GameObject targetObject = new(name, typeof(RectTransform), typeof(Image));
            targetObject.layer = 5;
            targetObject.transform.SetParent(parent, false);

            RectTransform rect = targetObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(380f, 92f);

            Image image = targetObject.GetComponent<Image>();
            image.color = TargetColor;
            image.raycastTarget = false;
            CreateLabel(rect, "Label", "TargetPanel", 28f, Vector2.zero, rect.sizeDelta);
            return targetObject;
        }

        /// <summary>테스트 카드에 중앙 정렬된 TMP 안내 문구를 만든다.</summary>
        /// <param name="parent">라벨의 부모 RectTransform이다.</param>
        /// <param name="name">Hierarchy에서 식별할 라벨 이름이다.</param>
        /// <param name="text">화면에 표시할 문자열이다.</param>
        /// <param name="fontSize">기준 해상도에서 사용할 글자 크기다.</param>
        /// <param name="position">부모 중심을 기준으로 한 위치다.</param>
        /// <param name="size">라벨의 가로와 세로 크기다.</param>
        private static void CreateLabel(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Vector2 position,
            Vector2 size)
        {
            GameObject labelObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = 5;
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (testFont != null)
                label.font = testFont;

            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
        }

        /// <summary>ShowTargetOnClick의 private target 직렬화 필드에 테스트 대상을 연결한다.</summary>
        /// <param name="utility">대상을 표시할 유틸리티 컴포넌트다.</param>
        /// <param name="target">클릭 후 활성화될 대상이다.</param>
        private static void AssignTarget(ShowTargetOnClick utility, GameObject target)
        {
            SerializedObject serializedUtility = new(utility);
            serializedUtility.FindProperty("target").objectReferenceValue = target;
            serializedUtility.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>HideTargetOnClick의 private target 직렬화 필드에 테스트 대상을 연결한다.</summary>
        /// <param name="utility">대상을 숨길 유틸리티 컴포넌트다.</param>
        /// <param name="target">클릭 후 비활성화될 대상이다.</param>
        private static void AssignTarget(HideTargetOnClick utility, GameObject target)
        {
            SerializedObject serializedUtility = new(utility);
            serializedUtility.FindProperty("target").objectReferenceValue = target;
            serializedUtility.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>임시 테스트 카드 계층을 지정 경로의 프리팹으로 저장하고 메모리에서 정리한다.</summary>
        /// <param name="root">저장할 테스트 카드의 루트다.</param>
        /// <param name="fileName">UtilityTests 폴더 아래에 사용할 프리팹 파일명이다.</param>
        private static void SaveTestPrefab(RectTransform root, string fileName)
        {
            string path = $"{TestPrefabRoot}/{fileName}";
            EnsureFolder(TestPrefabRoot);

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path, out bool success);
            Object.DestroyImmediate(root.gameObject);

            if (!success)
                Debug.LogError($"[UI Utility] 테스트 프리팹 저장에 실패했습니다: {path}");
        }

        /// <summary>AssetDatabase를 사용해 중첩된 프로젝트 폴더를 필요한 만큼 생성한다.</summary>
        /// <param name="folderPath">Assets에서 시작하는 프로젝트 상대 폴더 경로다.</param>
        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            int separatorIndex = folderPath.LastIndexOf('/');
            string parent = folderPath[..separatorIndex];
            string leaf = folderPath[(separatorIndex + 1)..];

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
