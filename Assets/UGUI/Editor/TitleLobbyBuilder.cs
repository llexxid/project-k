using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Rebuilds only the title prefab; retains its account/authentication bindings.</summary>
    public static class TitleLobbyBuilder
    {
        public const string ArtPath = "Assets/UGUI/Art/Lobby";
        public const string PrefabPath = "Assets/UGUI/Prefabs/Screens/Screen_Title.prefab";
        const string KitPath = "Assets/ExternalAssets/Layer Lab/GUI Pro-MinimalGame/Shared/Sprite_Common/";

        [Serializable] public class ArtManifest { public Layer[] layers; }
        [Serializable] public class Layer
        {
            public string name;
            public string parent;
            public float x, y, width, height, pivotX, pivotY, angle, rise, period, phase;
            public bool startled;
        }

        [MenuItem("KingdomIdle/UGUI/Rebuild illustrated lobby")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding title assets.");
            ImportArt();
            F.Init();
            F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { ApplyTo(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Lobby] Layered title prefab rebuilt.");
        }

        public static void ImportArt()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in Directory.GetFiles(ArtPath, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                foreach (string platform in new[] { "Android", "iPhone" })
                    importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    {
                        name = platform, overridden = true, maxTextureSize = 2048,
                        format = TextureImporterFormat.ASTC_6x6,
                        textureCompression = TextureImporterCompression.CompressedHQ,
                        compressionQuality = 100
                    });
                importer.SaveAndReimport();
            }
        }

        internal static void ApplyTo(GameObject root)
        {
            if (!File.Exists(ArtPath + "/Lobby.layers.json")) return;
            var view = root.GetComponent<TitleScreenView>();
            // Move retained controls out before deleting an earlier generated presentation.
            view.btnLogin.transform.SetParent(root.transform, false);
            view.pressHint.transform.SetParent(root.transform, false);
            foreach (string name in new[] { "LobbyArt", "LobbyLogo", "LobbyFooter", "LobbyLanguage", "LobbyLanguagePopup", "LobbyFade", "GameTitle", "Dark", "TitleLogo", "DecoDivider" })
            {
                var old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var oldPresentation = root.GetComponent<TitleLobbyPresentation>();
            if (oldPresentation != null) Object.DestroyImmediate(oldPresentation);
            var rootImage = root.GetComponent<Image>();
            if (rootImage != null) { rootImage.sprite = null; rootImage.color = new Color(0, 0, 0, 0); rootImage.raycastTarget = false; }
            var p = root.AddComponent<TitleLobbyPresentation>();
            view.presentation = p;

            p.artViewport = F.Container(root.transform, "LobbyArt");
            F.Stretch(p.artViewport);
            p.artViewport.SetAsFirstSibling();
            var backing = F.Box(p.artViewport, "LandscapeFallback", new Color(.07f, .09f, .09f, 1), false);
            p.landscapeFallback = backing;
            F.Stretch(backing.rectTransform);
            p.artWorld = F.Container(p.artViewport, "World");
            F.AnchorCenter(p.artWorld, 2048, 2048);
            var landscape = Art(p.artWorld, "Landscape", "Lobby_Background");
            F.Stretch(landscape.rectTransform);
            // Static backdrop and moving characters have separate canvas rebuild boundaries.
            landscape.gameObject.AddComponent<Canvas>();
            var actors = F.Container(p.artWorld, "Actors");
            F.Stretch(actors);
            actors.gameObject.AddComponent<Canvas>();
            p.heroes = F.Container(actors, "Heroes");
            F.Stretch(p.heroes);

            var manifest = JsonUtility.FromJson<ArtManifest>(File.ReadAllText(ArtPath + "/Lobby.layers.json"));
            var transforms = new Dictionary<string, RectTransform>();
            var pivots = new Dictionary<string, Vector2>();
            var motions = new List<TitleLobbyPresentation.MotionLayer>();
            var rigs = new List<LobbyActorRig>();
            foreach (var layer in manifest.layers)
            {
                var baseParent = layer.name.StartsWith("Bandit") ? actors : p.heroes;
                var parent = string.IsNullOrEmpty(layer.parent) ? baseParent : transforms[layer.parent];
                var image = Art(parent, layer.name, "Lobby_" + layer.name);
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(layer.width, layer.height);
                rt.pivot = new Vector2((layer.pivotX - layer.x) / layer.width, 1f - (layer.pivotY - layer.y) / layer.height);
                var pivot = new Vector2(layer.pivotX - 512, 768 - layer.pivotY);
                if (string.IsNullOrEmpty(layer.parent)) rt.anchoredPosition = pivot;
                else
                {
                    // Child anchors follow its parent's pivot, not its geometric centre.
                    rt.anchorMin = rt.anchorMax = parent.pivot;
                    rt.anchoredPosition = pivot - pivots[layer.parent];
                }
                transforms.Add(layer.name, rt); pivots.Add(layer.name, pivot);
                motions.Add(new TitleLobbyPresentation.MotionLayer
                {
                    target = rt, rest = rt.anchoredPosition, angle = layer.angle, rise = layer.rise,
                    period = layer.period, phase = layer.phase, startled = layer.startled
                });
                if (Enum.TryParse<LobbyActorRig.Actor>(layer.name, out var actor))
                {
                    var rig = image.gameObject.AddComponent<LobbyActorRig>();
                    rig.actor = actor;
                    rigs.Add(rig);
                }
            }
            p.layers = motions.ToArray();
            p.actorRigs = rigs.ToArray();

            p.siegeLights = new Image[3];
            var firePoints = new[] { new Vector2(174, 220), new Vector2(249, 294), new Vector2(294, 224) };
            for (int i = 0; i < p.siegeLights.Length; i++)
            {
                var light = F.Box(actors, "DistantFire" + i, new Color(1, .44f, .12f, .24f), false);
                light.sprite = F.CircleSoft;
                F.AnchorCenter(light.rectTransform, 40, 50, firePoints[i].x, firePoints[i].y);
                p.siegeLights[i] = light;
            }
            var crystal = F.Box(actors, "TowerCrystalGlow", new Color(.25f, .49f, 1f, .16f), false);
            crystal.sprite = F.CircleSoft;
            F.AnchorCenter(crystal.rectTransform, 26, 38, -20, 302);
            p.crystalGlow = crystal;
            var storm = F.Container(actors, "CloudInternalLightning");
            F.AnchorCenter(storm, 140, 44, 363, 390);
            p.stormFlash = storm.gameObject.AddComponent<LobbyStormFlash>();
            p.stormFlash.raycastTarget = false;
            p.stormFlash.canvasRenderer.cullTransparentMesh = true;
            p.stormFlash.SetFlash(0, 0);

            var glow = F.Box(transforms["Mage"], "StaffLight", new Color(.5f, .93f, 1f, .28f), false);
            glow.sprite = F.CircleSoft;
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = transforms["Mage"].pivot;
            glow.rectTransform.anchoredPosition = new Vector2(135, 116);
            glow.rectTransform.sizeDelta = new Vector2(62, 62);
            p.magicGlow = glow;
            p.motes = new RectTransform[6];
            for (int i = 0; i < p.motes.Length; i++)
            {
                var mote = F.Box(actors, "Mote" + i, new Color(1f, .83f, .43f, .55f), false);
                mote.sprite = F.CircleSoft;
                F.AnchorCenter(mote.rectTransform, 8, 8);
                p.motes[i] = mote.rectTransform;
            }

            var sidePanel = F.Box(p.artViewport, "LandscapePanel", new Color(.07f, .09f, .09f, 1), false);
            p.landscapePanel = sidePanel.rectTransform;
            p.landscapePanel.anchorMin = p.landscapePanel.anchorMax = new Vector2(0, .5f);
            p.landscapePanel.pivot = new Vector2(0, .5f);
            var edge = F.Box(p.landscapePanel, "BronzeEdge", UguiTheme.BronzeLight, false);
            edge.rectTransform.anchorMin = new Vector2(1, 0); edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.sizeDelta = new Vector2(2, 0);
            var fade = Art(p.artViewport, "FooterFade", "Lobby_FooterFade");
            p.footerShade = fade;
            fade.rectTransform.anchorMin = Vector2.zero; fade.rectTransform.anchorMax = Vector2.right;
            fade.rectTransform.pivot = new Vector2(.5f, 0);
            fade.rectTransform.sizeDelta = new Vector2(0, 520);
            fade.rectTransform.anchoredPosition = Vector2.zero;
            view.bgClickCatcher.transform.SetSiblingIndex(1);

            p.logoRoot = F.Container(root.transform, "LobbyLogo");
            p.logoRoot.anchorMin = p.logoRoot.anchorMax = new Vector2(.5f, 1);
            p.logoRoot.pivot = new Vector2(.5f, 1);
            p.logo = Art(p.logoRoot, "LocalizedLogo", "Lobby_Logo_KO");
            F.Stretch(p.logo.rectTransform);
            p.logo.preserveAspect = true;
            p.logo.color = new Color(.94f, .94f, .94f, 1f);
            p.koreanLogo = Load("Lobby_Logo_KO"); p.englishLogo = Load("Lobby_Logo_EN");

            p.footer = F.Container(root.transform, "LobbyFooter");
            p.footer.anchorMin = p.footer.anchorMax = new Vector2(.5f, 0);
            p.footer.pivot = new Vector2(.5f, 0);
            view.pressHint.transform.SetParent(p.footer, false);
            F.AnchorCenter(view.pressHint.rectTransform, 900, 90, 0, 86);
            PlainText(view.pressHint, 48, UguiTheme.Parchment);
            view.pressHint.characterSpacing = 3;
            p.pressHint = view.pressHint;

            var login = view.btnLogin;
            p.accountButton = (RectTransform)login.transform;
            login.transform.SetParent(p.footer, false);
            F.AnchorCenter((RectTransform)login.transform, 380, 144, 0, -50);
            StyleButton(login, true);
            var loginLabel = login.GetComponentInChildren<TMP_Text>(true);
            F.Stretch(loginLabel.rectTransform);
            PlainText(loginLabel, 38, UguiTheme.Parchment);
            loginLabel.characterSpacing = 0;
            var languageRoot = F.Container(root.transform, "LobbyLanguage"); F.Stretch(languageRoot);
            p.languageButton = IconButton(languageRoot, "Language", out var languageIcon);
            languageIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitPath + "~Demo/Demo_Icon/Icon_Setting_Language.png");
            var langRt = (RectTransform)p.languageButton.transform;
            langRt.anchorMin = langRt.anchorMax = new Vector2(0, 1);
            langRt.pivot = new Vector2(0, 1); langRt.sizeDelta = new Vector2(144, 144);
            langRt.anchoredPosition = new Vector2(24, -24);
            var version = F.Text(p.footer, "Version", "", 24, new Color(.85f, .88f, .84f, .65f), TextAlignmentOptions.Center);
            F.AnchorCenter(version.rectTransform, 400, 32, 0, -143);
            p.versionLabel = version;
            BuildLanguagePopup(root.transform, p);
            SettingsRevisionBuilder.WireTitleButton(view);

            // Preserve the development guest entry and authenticated scene gate.
            view.btnLoginGuest.gameObject.SetActive(true);
            view.btnLoginApple.gameObject.SetActive(false);
            p.loginBox = view.popupLoginBox;
            p.loginBox.GetComponent<Image>().color = UguiTheme.RusticPanelDeep;
            p.loginBox.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitPath + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Bg.png");
            foreach (var image in p.loginBox.GetComponentsInChildren<Image>(true))
                if (image.name == "Sheen" || image.name == "InnerRim" || image.name == "Icon") image.gameObject.SetActive(false);
            StyleButton(view.btnLoginGoogle, true);
            StyleButton(view.btnLoginGuest, true);
            var guestLabel = view.btnLoginGuest.GetComponentInChildren<TMP_Text>(true);
            PlainText(guestLabel, 32, UguiTheme.Parchment);
            var providerLabel = view.btnLoginGoogle.GetComponentInChildren<TMP_Text>(true);
            PlainText(providerLabel, 32, UguiTheme.Parchment);
            var localization = new List<TitleLobbyPresentation.LocalizedLabel>();
            void Add(TMP_Text target, string ko, string en)
            { localization.Add(new TitleLobbyPresentation.LocalizedLabel { target = target, korean = ko, english = en }); target.text = ko; }
            Add(view.pressHint, "탭해서 시작하세요", "TAP TO CONTINUE");
            Add(loginLabel, "계정 로그인", "SIGN IN");
            Add(providerLabel, "Google 계정으로 로그인", "Continue with Google");
            Add(guestLabel, "게스트로 시작", "Continue as Guest");
            Add(p.loginBox.Find("Title").GetComponent<TMP_Text>(), "왕국으로 출정", "YOUR KINGDOM AWAITS");
            Add(p.loginBox.Find("Subtitle").GetComponent<TMP_Text>(), "계정을 연결하고 모험을 이어가세요.", "Sign in to continue your adventure.");
            Add(p.loginBox.Find("Terms").GetComponent<TMP_Text>(),
                "로그인 시 이용약관 및 개인정보 처리방침에 동의한 것으로 간주됩니다.",
                "By signing in, you agree to the Terms of Service and Privacy Policy.");
            p.labels = localization.ToArray();
            foreach (var text in p.loginBox.GetComponentsInChildren<TMP_Text>(true))
            { text.raycastTarget = false; text.color = UguiTheme.Parchment; text.fontSharedMaterial = text.font.material; }
            view.popupLogin.transform.SetAsLastSibling();
            view.popupLogin.SetActive(false);
        }

        static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/" + name + ".png");
        static Image Art(Transform parent, string name, string asset)
        {
            var image = F.Box(parent, name, Color.white, false);
            image.sprite = Load(asset);
            if (image.sprite == null) throw new InvalidOperationException("Missing lobby art: " + asset);
            image.raycastTarget = false;
            return image;
        }
        static void PlainText(TMP_Text text, float size, Color color)
        {
            text.fontSharedMaterial = text.font.material;
            text.fontSize = size; text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false;
        }
        static Button SmallButton(Transform parent, string name, out TMP_Text label)
        {
            var image = F.Box(parent, name, new Color(0, 0, 0, 0), false, true);
            F.AnchorCenter(image.rectTransform, 220, 132);
            var button = F.ButtonOn(image, false);
            image.sprite = null;
            image.color = new Color(0, 0, 0, 0);
            label = F.Text(image.transform, "Label", "", 32, UguiTheme.Parchment, TextAlignmentOptions.Center);
            F.Stretch(label.rectTransform); PlainText(label, 32, UguiTheme.Parchment);
            button.targetGraphic = label;
            return button;
        }

        static Button IconButton(Transform parent, string name, out Image icon)
        {
            var image = F.Box(parent, name, UguiTheme.RusticSurfaceDark, false, true);
            F.AnchorCenter(image.rectTransform, 144, 144);
            var button = F.ButtonOn(image, false);
            StyleButton(button, true);
            icon = F.Box(image.transform, "Symbol", UguiTheme.Parchment, false);
            F.AnchorCenter(icon.rectTransform, 60, 60);
            icon.preserveAspect = true;
            return button;
        }

        static void BuildLanguagePopup(Transform root, TitleLobbyPresentation p)
        {
            var popup = F.Container(root, "LobbyLanguagePopup"); F.Stretch(popup);
            p.languagePopup = popup.gameObject;
            var dim = F.Box(popup, "Dismiss", new Color(0, 0, 0, .18f), false, true);
            F.Stretch(dim.rectTransform);
            // A full-screen dismiss target must not inherit the opaque button skin.
            p.languageDismiss = dim.gameObject.AddComponent<Button>();
            p.languageDismiss.targetGraphic = dim;
            p.languageDismiss.transition = Selectable.Transition.None;
            var panel = F.Box(popup, "Panel", UguiTheme.RusticPanelDeep, false, true);
            panel.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitPath + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Bg.png");
            panel.type = Image.Type.Sliced;
            p.languagePanel = panel.rectTransform;
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0, 1);
            panel.rectTransform.pivot = new Vector2(0, 1);
            panel.rectTransform.sizeDelta = new Vector2(490, 414);
            panel.rectTransform.anchoredPosition = new Vector2(24, -180);
            var border = F.Box(panel.transform, "Border", UguiTheme.Bronze, false);
            border.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitPath + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Border1.png");
            border.type = Image.Type.Sliced; F.Stretch(border.rectTransform);
            p.currentLanguage = F.Text(panel.transform, "CurrentLanguage", "현재 언어: 한국어", 29, UguiTheme.Parchment, TextAlignmentOptions.Left);
            p.currentLanguage.rectTransform.anchorMin = new Vector2(0, 1); p.currentLanguage.rectTransform.anchorMax = Vector2.one;
            p.currentLanguage.rectTransform.pivot = new Vector2(.5f, 1);
            p.currentLanguage.rectTransform.sizeDelta = new Vector2(-56, 58);
            p.currentLanguage.rectTransform.anchoredPosition = new Vector2(0, -18);
            Button Option(string name, string label, float y, out Image selected)
            {
                var button = F.TextButton(panel.transform, name, label, 34, UguiTheme.RusticSurfaceDark, out var text);
                F.AnchorCenter((RectTransform)button.transform, 426, 132, 0, y);
                StyleButton(button, true); PlainText(text, 34, UguiTheme.Parchment);
                selected = F.Box(button.transform, "Selected", UguiTheme.BronzeLight, false);
                selected.sprite = F.CircleSoft;
                F.AnchorCenter(selected.rectTransform, 16, 16, 170, 0);
                return button;
            }
            p.koreanButton = Option("Korean", "한국어", 52, out var ko);
            p.englishButton = Option("English", "English", -96, out var en);
            p.koreanSelected = ko; p.englishSelected = en;
            popup.gameObject.SetActive(false);
        }
        internal static void StyleButton(Button button, bool framed)
        {
            var image = button.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitPath + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Bg.png");
            image.type = Image.Type.Sliced; image.color = UguiTheme.RusticSurfaceDark;
            foreach (var child in button.GetComponentsInChildren<Image>(true))
                if (child != image) child.gameObject.SetActive(false);
            if (framed)
            {
                var old = button.transform.Find("LobbyBronze");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var frame = F.Box(button.transform, "LobbyBronze", UguiTheme.BronzeLight, false);
                frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitPath + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Border1.png");
                frame.type = Image.Type.Sliced; F.Stretch(frame.rectTransform);
                var layout = frame.gameObject.AddComponent<LayoutElement>(); layout.ignoreLayout = true;
            }
        }
    }
}
