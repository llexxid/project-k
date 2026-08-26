using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 생성된 UGUI 프리팹을 실제로 렌더링해 PNG로 저장하는 진단 도구.
    /// (플레이 없이 UI 외형을 확인하기 위한 용도 — Unity를 -batchmode 로 실행하되
    ///  -nographics 는 빼야 렌더링이 된다.)
    /// </summary>
    internal static class UguiPreviewCapture
    {
        private const int W = 1080;
        private const int H = 1920;
        private static readonly string OutDir = Path.Combine(Path.GetTempPath(), "ugui_preview");

        internal static void CaptureAll()
        {
            Directory.CreateDirectory(OutDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 렌더용 카메라
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.35f, 0.15f, 1f);   // 게임 배경 대용(초원 느낌)
            cam.transform.position = new Vector3(0, 0, -100);

            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Preview] 카탈로그 없음");
                return;
            }

            var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabGenUtil.PrefabRoot}/UGUI_UIRoot.prefab");
            var rootGo = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, scene);

            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            // UIManager가 Awake를 못 도니 레이어를 직접 찾는다
            var layers = new Dictionary<string, RectTransform>();
            foreach (var rt in rootGo.GetComponentsInChildren<RectTransform>(true))
                layers[rt.name] = rt;

            var shots = new (string name, GameObject prefab, string layer)[]
            {
                ("01_title",       catalog.screenTitle,      "LayerScreens"),
                ("02_main",        catalog.screenMain,       "LayerScreens"),
                ("03_kingdomarmy", catalog.panelKingdomArmy, "LayerPanels"),
                ("04_settings",    catalog.overlaySettings,  "LayerOverlays"),
                ("05_gacharesult", catalog.popupGachaResult, "LayerOverlays"),
                ("10_profile",     catalog.popupProfile,     "LayerOverlays"),
                ("18_reincarnation", catalog.popupReincarnation, "LayerPopups"),
            };

            foreach (var s in shots)
            {
                if (s.prefab == null) continue;
                if (!layers.TryGetValue(s.layer, out var parent)) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(s.prefab, scene);
                inst.transform.SetParent(parent, false);
                var irt = (RectTransform)inst.transform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                inst.SetActive(true);

                // 메인 화면일 땐 하단바/상단바가 보이도록 그대로 두고 캡처
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(irt);
                Canvas.ForceUpdateCanvases();

                Render(cam, Path.Combine(OutDir, s.name + ".png"));

                Object.DestroyImmediate(inst);
            }

            CaptureNavTabs(scene, cam, catalog, layers);
            CaptureGachaWidgets(scene, cam, catalog, layers);
            CaptureKingdomArmyWidgets(scene, cam, catalog, layers);
            CaptureKASubPanels(scene, cam, catalog, layers);
            CapturePartyHud(scene, cam, catalog, layers);
            CaptureDropdown(scene, cam, catalog, layers);
            CaptureMainComposite(scene, cam, catalog, layers);
            CaptureDungeon(scene, cam, catalog, layers);

            Debug.Log($"[Preview] 캡처 완료: {OutDir}");
        }

        /// <summary>
        /// 던전 패널(하단 시트+카드 목록) + 난이도 팝업(샘플 데이터) 정적 캡처.
        /// 16_dungeon.png / 17_dungeon_difficulty.png
        /// 에디트 모드라 Awake/OnEnable 이 돌지 않으므로 View 공개 API 로 직접 채운다.
        /// </summary>
        private static void CaptureDungeon(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog.panelDungeon == null) return;
            if (!layers.TryGetValue("LayerPanels", out var parent)) return;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(catalog.panelDungeon, scene);
            inst.transform.SetParent(parent, false);
            var irt = (RectTransform)inst.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            inst.SetActive(true);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(irt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "16_dungeon.png"));

            // 난이도 팝업 — 스테이지 번호 기반 샘플(2단계까지 해금)로 실제 화면 유사 상태를 만든다
            var popup = inst.GetComponentInChildren<DungeonDifficultyPopupView>(true);
            var card = inst.GetComponentInChildren<DungeonCardView>(true);
            if (popup != null && card != null)
            {
                var difficulties = new DungeonDifficultyDisplayData[5];
                for (int i = 0; i < difficulties.Length; i++)
                    difficulties[i] = new DungeonDifficultyDisplayData(i + 1, i < 2, (i + 1) * 2700L);
                popup.SetDifficultyData(difficulties, 4000L);
                popup.Show(card);

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(irt);
                Canvas.ForceUpdateCanvases();
                Render(cam, Path.Combine(OutDir, "17_dungeon_difficulty.png"));
            }

            Object.DestroyImmediate(inst);
        }

        /// <summary>
        /// 인게임 메인 화면 합성 샷 — 런타임 배치 순서 그대로
        /// (마탑 환경 → Screen_Main → 파티 HUD → 궁극기 버튼) 를 겹쳐 실제 레이아웃을 검증한다.
        /// 12_main_composite.png / 13_divine_collection.png
        /// </summary>
        private static void CaptureMainComposite(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (!layers.TryGetValue("LayerScreens", out var parent)) return;

            var spawned = new List<GameObject>();
            void Add(GameObject prefab, bool stretch)
            {
                if (prefab == null) return;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.transform.SetParent(parent, false);
                if (stretch)
                {
                    var rt = (RectTransform)inst.transform;
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                }
                inst.SetActive(true);
                spawned.Add(inst);
            }

            Add(catalog.hudMageTowerEnv, stretch: false);   // 화면 프리팹보다 먼저 = 하단바 뒤
            Add(catalog.screenMain, stretch: true);
            Add(catalog.hudParty, stretch: false);
            Add(catalog.hudDivineSkill, stretch: false);

            // 런타임 컨트롤러가 채우는 값(초상화·HP·스킬·장착 카드)을 정적 캡처에서도 채운다 —
            // 그러지 않으면 합성 샷이 빈 메달리온/미장착 버튼만 보여 실제 화면과 딴판이 된다.
            PopulateCompositeRuntimeVisuals(spawned);

            Canvas.ForceUpdateCanvases();
            foreach (var go in spawned)
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)go.transform);
            Canvas.ForceUpdateCanvases();

            Render(cam, Path.Combine(OutDir, "12_main_composite.png"));

            // 궁극기 버튼 컨셉 스킨 — 런타임 Refresh 가 하는 스왑을 정적 캡처에서 재현해 육안 검증한다
            // (미장착 상태만 찍으면 링 아트가 한 번도 화면에 안 잡힌다).
            CaptureDivineConceptRings(scene, cam, spawned);

            foreach (var go in spawned) Object.DestroyImmediate(go);

            // 신 스킬 도감 팝업 단독 샷
            if (catalog.popupDivineCollection != null && layers.TryGetValue("LayerPopups", out var popupLayer))
            {
                var popup = (GameObject)PrefabUtility.InstantiatePrefab(catalog.popupDivineCollection, scene);
                popup.transform.SetParent(popupLayer, false);
                var prt = (RectTransform)popup.transform;
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                popup.SetActive(true);
                PopulateDivineCollection(popup, catalog);

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(prt);
                Canvas.ForceUpdateCanvases();

                Render(cam, Path.Combine(OutDir, "13_divine_collection.png"));
                Object.DestroyImmediate(popup);
            }

            // 궁극기 컷인 오버레이 단독 샷 (연출 중간 프레임 상태로 세팅)
            if (catalog.overlayDivineCutIn != null && layers.TryGetValue("LayerPopups", out var cutInLayer))
            {
                var cut = (GameObject)PrefabUtility.InstantiatePrefab(catalog.overlayDivineCutIn, scene);
                cut.transform.SetParent(cutInLayer, false);
                var crt = (RectTransform)cut.transform;
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                cut.SetActive(true);
                PopulateDivineCutIn(cut);

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
                Canvas.ForceUpdateCanvases();

                Render(cam, Path.Combine(OutDir, "15_divine_cutin.png"));
                Object.DestroyImmediate(cut);
            }
        }

        private static List<KingdomIdle.Divine.DivineSkillSO> LoadDivineCards()
        {
            var cards = new List<KingdomIdle.Divine.DivineSkillSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:DivineSkillSO", new[] { "Assets/DivineSkill/SO" }))
            {
                var so = AssetDatabase.LoadAssetAtPath<KingdomIdle.Divine.DivineSkillSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) cards.Add(so);
            }
            cards.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return cards;
        }

        /// <summary>도감 팝업을 실제 카드로 채운다 — 빈 껍데기 샷으로는 디자인을 판단할 수 없다.</summary>
        private static void PopulateDivineCollection(GameObject popup, UIViewCatalog catalog)
        {
            var v = popup.GetComponent<DivineCollectionPopupView>();
            if (v == null || catalog.itemDivineCard == null) return;

            var cards = LoadDivineCards();
            if (cards.Count == 0) return;

            if (v.cardGrid != null)
            {
                for (int i = v.cardGrid.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(v.cardGrid.GetChild(i).gameObject);

                for (int i = 0; i < cards.Count; i++)
                {
                    var cellGo = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemDivineCard, popup.scene);
                    cellGo.transform.SetParent(v.cardGrid, false);
                    var cell = cellGo.GetComponent<DivineCardItemView>();
                    if (cell == null) continue;
                    // 보유/미보유·레벨·중복·장착을 섞어 실제 도감처럼 보이게 한다
                    bool owned = i < 5;
                    cell.Set(cards[i], owned, owned ? 1 + i : 0, i == 1 ? 2 : 0, i == 0, i == 0, null);
                }
            }

            var card0 = cards[0];
            if (v.illustration != null)
            {
                var sp = card0.illustration != null ? card0.illustration : card0.icon;
                v.illustration.sprite = sp;
                v.illustration.enabled = sp != null;
                v.illustration.color = Color.white;
            }
            var grade = KingdomIdle.Divine.DivineSkillSO.GetGradeColor(card0.grade);
            if (v.cardNameLabel != null) { v.cardNameLabel.text = card0.DisplayName; v.cardNameLabel.color = grade; }
            if (v.gradePill != null) v.gradePill.color = grade;
            if (v.gradePillLabel != null)
                v.gradePillLabel.text = KingdomIdle.Divine.DivineSkillSO.GetGradeName(card0.grade);
            if (v.skillNameLabel != null) v.skillNameLabel.text = card0.skillNameKor;
            if (v.descriptionLabel != null) v.descriptionLabel.text = card0.description;
            if (v.statCooldownLabel != null) v.statCooldownLabel.text = $"쿨타임  {card0.cooldown:0}초";
            if (v.statMultiplierLabel != null) v.statMultiplierLabel.text = "레벨 배율  x1.00";
            if (v.bonusLabel != null) v.bonusLabel.text = "컬렉션 보너스: 공격력 +5%";
            if (v.equipButtonLabel != null) v.equipButtonLabel.text = "장착됨";
            if (v.levelUpButtonLabel != null) v.levelUpButtonLabel.text = "레벨업 (2/3)";
            if (v.lockedHintLabel != null) v.lockedHintLabel.gameObject.SetActive(false);
        }

        /// <summary>컷인 오버레이를 '연출 절정' 프레임 상태로 세팅한다 (일러스트 인 + 플레이트 팝인 완료).</summary>
        private static void PopulateDivineCutIn(GameObject go)
        {
            var v = go.GetComponent<DivineCutInView>();
            if (v == null) return;
            var cards = LoadDivineCards();
            if (cards.Count == 0) return;
            var card = cards[0];

            if (v.scrim != null)
            {
                v.scrim.gameObject.SetActive(true);   // 프리팹 초기 상태가 꺼져 있어 암막이 안 찍혔다
                v.scrim.enabled = true;
                v.scrim.color = new Color(0f, 0f, 0f, 0.82f);
            }
            if (v.illustGroup != null) v.illustGroup.alpha = 1f;
            if (v.illustHolder != null) v.illustHolder.anchoredPosition = Vector2.zero;
            if (v.illust != null)
            {
                // 런타임(DivineCutInController)과 같은 대체 순서: 컷씬 → 스탠딩 → 아이콘
                var sp = card.cutInIllustration != null ? card.cutInIllustration
                       : card.illustration != null ? card.illustration
                       : card.icon;
                v.illust.sprite = sp;
                v.illust.enabled = sp != null;
                v.illust.color = Color.white;
            }
            if (v.plateGroup != null) v.plateGroup.alpha = 1f;
            if (v.plate != null) v.plate.localScale = Vector3.one;
            var grade = KingdomIdle.Divine.DivineSkillSO.GetGradeColor(card.grade);
            if (v.gradeRibbon != null) v.gradeRibbon.color = grade;
            if (v.gradeLabel != null) v.gradeLabel.text = KingdomIdle.Divine.DivineSkillSO.GetGradeName(card.grade);
            if (v.nameLabel != null) { v.nameLabel.text = card.nameKor; v.nameLabel.color = grade; }
            if (v.skillLabel != null) v.skillLabel.text = card.skillNameKor;
            if (v.flash != null) v.flash.color = new Color(1f, 1f, 1f, 0f);   // 섬광 전 프레임
        }

        /// <summary>합성 샷용: 파티 HUD 초상화/HP/스킬 + 궁극기 버튼 장착 상태를 실제 에셋으로 채운다.</summary>
        /// <summary>
        /// 프리뷰용 파티 스킬 아이콘 표 — 런타임 PartyHudController.ResolveSkillIcon 와 같은 규칙.
        /// [멤버(기사/궁수/법사), 슬롯(기본공격/오라/특수)]
        /// </summary>
        private static Sprite[,] PartySkillIconMatrix()
        {
            var cat = AssetDatabase.LoadAssetAtPath<UIViewCatalog>("Assets/UGUI/UIViewCatalog.asset");
            if (cat == null) return new Sprite[3, 3];
            return new[,]
            {
                { cat.iconSkillSword, cat.iconSkillShield, cat.iconSkillPotion },   // 기사 — 강철의지
                { cat.iconSkillBow,   cat.iconSkillShield, cat.iconSkillArrows },   // 궁수 — 집중사격
                { cat.iconSkillWand,  cat.iconSkillShield, cat.iconSkillStar },     // 법사 — 에너지 파동
            };
        }

        /// <summary>아이콘이 있으면 아이콘, 없으면 이름 라벨 — 런타임과 동일한 폴백.</summary>
        private static void ApplySkillIcon(PartyHudView.SkillSlot slot, Sprite sp)
        {
            if (slot.icon != null)
            {
                slot.icon.sprite = sp;
                slot.icon.gameObject.SetActive(sp != null);
            }
            if (slot.nameLabel != null)
                slot.nameLabel.gameObject.SetActive(sp == null);
        }

        private static void PopulateCompositeRuntimeVisuals(List<GameObject> spawned)
        {
            foreach (var go in spawned)
            {
                var party = go.GetComponent<PartyHudView>();
                if (party != null)
                {
                    var jobs = new[]
                    {
                        AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Knight.asset"),
                        AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Archer.asset"),
                        AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Mage.asset"),
                    };
                    float[] hp = { 0.85f, 0.45f, 1f };
                    var icons = PartySkillIconMatrix();
                    for (int i = 0; i < 3 && i < party.members.Length; i++)
                    {
                        var m = party.members[i];
                        if (m == null) continue;
                        if (m.portraitImage != null && jobs[i] != null)
                        {
                            m.portraitImage.sprite = jobs[i].Portrait;
                            m.portraitImage.enabled = jobs[i].Portrait != null;
                        }
                        if (m.hpFill != null) m.hpFill.fillAmount = hp[i];
                        for (int s = 0; s < m.skills.Length; s++)
                        {
                            var slot = m.skills[s];
                            if (slot?.root == null) continue;
                            slot.root.SetActive(true);
                            ApplySkillIcon(slot, icons[i, s]);
                            bool passive = s == 1, cooling = s == 2;
                            if (slot.cooldownMask != null)
                            {
                                slot.cooldownMask.gameObject.SetActive(cooling);
                                slot.cooldownMask.fillAmount = 0.6f;   // 드레인 중간 상태
                            }
                            if (slot.cooldownLabel != null)
                            {
                                slot.cooldownLabel.gameObject.SetActive(passive || cooling);
                                slot.cooldownLabel.text = passive ? "상시" : cooling ? "5" : "";
                                slot.cooldownLabel.color = passive ? new Color(0.4f, 1f, 0.4f, 1f) : Color.white;
                            }
                        }
                    }
                }

                // (구 좌측 마탑 스킬 슬롯 열 populate 는 HUD 제거와 함께 삭제됨)

                var hud = go.GetComponent<DivineSkillHudView>();
                if (hud != null)
                {
                    var card = AssetDatabase.LoadAssetAtPath<KingdomIdle.Divine.DivineSkillSO>(
                        "Assets/DivineSkill/SO/DivineSkill_Astra.asset");
                    if (card != null)
                    {
                        if (hud.conceptRing != null && card.buttonRingSprite != null)
                        {
                            hud.conceptRing.sprite = card.buttonRingSprite;
                            hud.conceptRing.gameObject.SetActive(true);
                        }
                        if (hud.icon != null && card.icon != null)
                        {
                            hud.icon.sprite = card.icon;
                            hud.icon.gameObject.SetActive(true);
                        }
                        if (hud.emptyLabel != null) hud.emptyLabel.gameObject.SetActive(false);
                        if (hud.gradeBorder != null)
                            hud.gradeBorder.color = KingdomIdle.Divine.DivineSkillSO.GetGradeColor(card.grade);
                        if (hud.readyGlow != null) hud.readyGlow.gameObject.SetActive(true);
                    }
                }
            }
        }

        /// <summary>
        /// 궁극기 버튼에 카드별 컨셉 링을 순서대로 끼워 넣고 한 장씩 찍는다.
        /// 런타임 DivineSkillHudController.Refresh 와 같은 조작(ConceptRing 스프라이트+활성, 아이콘, 등급색)만 한다.
        /// </summary>
        private static void CaptureDivineConceptRings(UnityEngine.SceneManagement.Scene scene, Camera cam,
            List<GameObject> spawned)
        {
            DivineSkillHudView hud = null;
            foreach (var go in spawned)
            {
                hud = go.GetComponent<DivineSkillHudView>();
                if (hud != null) break;
            }
            if (hud == null || hud.conceptRing == null) return;

            var cards = new List<KingdomIdle.Divine.DivineSkillSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:DivineSkillSO", new[] { "Assets/DivineSkill/SO" }))
            {
                var so = AssetDatabase.LoadAssetAtPath<KingdomIdle.Divine.DivineSkillSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && so.buttonRingSprite != null) cards.Add(so);
            }
            if (cards.Count == 0) return;
            cards.Sort((a, b) => a.concept.CompareTo(b.concept));

            var shown = new HashSet<KingdomIdle.Divine.eDivineConcept>();
            foreach (var card in cards)
            {
                if (!shown.Add(card.concept)) continue;   // 컨셉당 1장 (루멘·아스트라는 Holy 공유)

                hud.conceptRing.sprite = card.buttonRingSprite;
                hud.conceptRing.gameObject.SetActive(true);
                if (hud.icon != null && card.icon != null)
                {
                    hud.icon.sprite = card.icon;
                    hud.icon.gameObject.SetActive(true);
                }
                if (hud.emptyLabel != null) hud.emptyLabel.gameObject.SetActive(false);
                if (hud.gradeBorder != null)
                    hud.gradeBorder.color = KingdomIdle.Divine.DivineSkillSO.GetGradeColor(card.grade);

                Canvas.ForceUpdateCanvases();
                Render(cam, Path.Combine(OutDir, $"14_divine_ring_{card.concept}.png"));
            }

            hud.conceptRing.gameObject.SetActive(false);
        }

        /// <summary>
        /// 탭/네비 버튼은 런타임에 생성되므로 프리팹 캡처엔 안 잡힌다.
        /// 실제 컨트롤러와 동일하게 아이콘·라벨·선택 상태를 넣어 샘플 바를 렌더링한다.
        /// </summary>
        /// <summary>메인 화면 + 재화 드롭다운을 골드 칩 아래로 정렬해 렌더(스타일/정렬 검증). 09_dropdown.png</summary>
        private static void CaptureDropdown(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || catalog.screenMain == null) return;
            if (!layers.TryGetValue("LayerScreens", out var parent)) return;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(catalog.screenMain, scene);
            inst.transform.SetParent(parent, false);
            var irt = (RectTransform)inst.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            inst.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(irt);
            Canvas.ForceUpdateCanvases();

            var view = inst.GetComponent<MainScreenView>();
            if (view != null)
            {
                if (view.lblGold != null) view.lblGold.text = "1,284,300";
                if (view.lblAncientCoin != null) view.lblAncientCoin.text = "1,720";

                if (view.popupCurrencies != null && view.popupCurrenciesContent != null && catalog.itemCurrencyLine != null)
                {
                    AddCurrencySample(view.popupCurrenciesContent, catalog, null, "보유 재화", null, true);
                    AddCurrencySample(view.popupCurrenciesContent, catalog, catalog.iconCoin, "골드", "1,284,300", false);
                    AddCurrencySample(view.popupCurrenciesContent, catalog, catalog.iconArcane, "비전 지식", "820", false);
                    AddCurrencySample(view.popupCurrenciesContent, catalog, catalog.iconFragment, "전직 파편", "40", false);

                    view.popupCurrencies.SetActive(true);
                    view.popupCurrencies.transform.SetAsLastSibling();
                    var target = view.btnCurrency != null ? view.btnCurrency.transform as RectTransform : null;
                    PositionUnder(view.popupCurrenciesRect, target, irt);
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(irt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "09_dropdown.png"));
            Object.DestroyImmediate(inst);
        }

        private static void SampleEquation(RectTransform container, UIViewCatalog catalog, string a, string b, string mult, string final)
        {
            if (container == null || catalog == null || catalog.itemStatTerm == null) return;
            OpSample(container, catalog, "(");
            TermSample(container, catalog, a);
            OpSample(container, catalog, "+");
            TermSample(container, catalog, b);
            OpSample(container, catalog, ")");
            OpSample(container, catalog, "×");
            TermSample(container, catalog, mult);
            OpSample(container, catalog, "=");
            TermSample(container, catalog, final);
        }

        private static void TermSample(RectTransform c, UIViewCatalog cat, string t)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemStatTerm);
            go.transform.SetParent(c, false);
            var term = go.GetComponent<StatTermView>();
            if (term != null && term.label != null) term.label.text = t;
        }

        private static void OpSample(RectTransform c, UIViewCatalog cat, string op)
        {
            var go = new GameObject("Op", typeof(RectTransform)); go.layer = 5;
            var rt = (RectTransform)go.transform; rt.SetParent(c, false);
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            if (cat != null && cat.defaultFont != null) tmp.font = cat.defaultFont;
            tmp.text = op; tmp.fontSize = 26f; tmp.color = UguiTheme.TextSecondary;
            tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.raycastTarget = false; tmp.fontStyle = TMPro.FontStyles.Bold;
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = (op == "(" || op == ")") ? 14f : 26f; le.preferredHeight = 44f;
        }

        private static void AddCurrencySample(RectTransform content, UIViewCatalog catalog, Sprite icon, string name, string value, bool isTitle)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemCurrencyLine);
            go.transform.SetParent(content, false);
            var line = go.GetComponent<CurrencyLineItemView>();
            if (line != null) line.Set(icon, name, value, isTitle);
        }

        private static void PositionUnder(RectTransform dropdown, RectTransform target, RectTransform parent, float gap = 10f)
        {
            if (dropdown == null || target == null || parent == null) return;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 brLocal = parent.InverseTransformPoint(corners[3]);
            dropdown.anchorMin = dropdown.anchorMax = new Vector2(1f, 1f);
            dropdown.pivot = new Vector2(1f, 1f);
            Rect pr = parent.rect;
            dropdown.anchoredPosition = new Vector2(brLocal.x - pr.xMax, brLocal.y - pr.yMax - gap);
        }

        private static void CaptureNavTabs(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || catalog.itemNavTabButton == null) return;
            if (!layers.TryGetValue("LayerPanels", out var parent)) return;

            var host = new GameObject("NavTabPreview", typeof(RectTransform));
            var hostRt = (RectTransform)host.transform;
            hostRt.SetParent(parent, false);
            hostRt.anchorMin = new Vector2(0f, 0.5f);
            hostRt.anchorMax = new Vector2(1f, 0.5f);
            hostRt.pivot = new Vector2(0.5f, 0.5f);
            hostRt.offsetMin = new Vector2(40f, -200f);
            hostRt.offsetMax = new Vector2(-40f, 200f);
            var col = host.AddComponent<VerticalLayoutGroup>();
            col.spacing = 24f;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;

            // 왕국군 네비 (종합/장비/스킬/전직) + 뽑기 탭 두 줄
            MakeBar(host.transform, catalog, 104f, new[]
            {
                ("종합", catalog.iconUser), ("장비", catalog.iconSword),
                ("스킬", catalog.iconBook), ("전직", catalog.iconStar),
            }, selectedIndex: 1, activeBg: UguiTheme.AccentBlue);

            MakeBar(host.transform, catalog, 104f, new[]
            {
                ("장비 뽑기", catalog.iconChest), ("마탑 스킬 뽑기", catalog.iconWand),
            }, selectedIndex: 0, activeBg: new Color(80f / 255f, 60f / 255f, 180f / 255f, 0.6f));

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRt);
            Canvas.ForceUpdateCanvases();

            Render(cam, Path.Combine(OutDir, "06_navtabs.png"));
            Object.DestroyImmediate(host);
        }

        private static void MakeBar(Transform parent, UIViewCatalog catalog, float height,
            (string label, Sprite icon)[] items, int selectedIndex, Color activeBg)
        {
            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var row = bar.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            var le = bar.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            for (int i = 0; i < items.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemNavTabButton);
                go.transform.SetParent(bar.transform, false);
                var v = go.GetComponent<NavTabButtonView>();
                if (v == null) continue;
                v.SetLabel(items[i].label);
                v.SetIcon(items[i].icon);
                v.SetSelected(i == selectedIndex, activeBg);
            }
        }

        /// <summary>뽑기 옵션 버튼 / 확률 알약 프리팹을 실제 데이터로 렌더링해 확인.</summary>
        private static void CaptureGachaWidgets(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || !layers.TryGetValue("LayerPanels", out var parent)) return;

            var host = new GameObject("GachaWidgetPreview", typeof(RectTransform));
            var hostRt = (RectTransform)host.transform;
            hostRt.SetParent(parent, false);
            hostRt.anchorMin = new Vector2(0f, 0.5f);
            hostRt.anchorMax = new Vector2(1f, 0.5f);
            hostRt.pivot = new Vector2(0.5f, 0.5f);
            hostRt.offsetMin = new Vector2(40f, -240f);
            hostRt.offsetMax = new Vector2(-40f, 240f);
            var col = host.AddComponent<VerticalLayoutGroup>();
            col.spacing = 24f; col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true;

            // 확률 알약 행
            if (catalog.itemRatePill != null)
            {
                var pillRow = new GameObject("Pills", typeof(RectTransform));
                pillRow.transform.SetParent(host.transform, false);
                var pr = pillRow.AddComponent<HorizontalLayoutGroup>();
                pr.spacing = 8f; pr.childControlWidth = false; pr.childControlHeight = false; pr.childForceExpandWidth = false; pr.childAlignment = TextAnchor.MiddleLeft;
                pillRow.AddComponent<LayoutElement>().preferredHeight = 56f;
                MakePill(pillRow.transform, catalog, "일반  70.0%", UguiTheme.RarityNormal);
                MakePill(pillRow.transform, catalog, "레어  25.0%", UguiTheme.RarityRare);
                MakePill(pillRow.transform, catalog, "에픽  5.0%", UguiTheme.RarityEpic);
            }

            // 뽑기 옵션 버튼 행
            if (catalog.itemGachaPullButton != null)
            {
                var btnRow = new GameObject("Pulls", typeof(RectTransform));
                btnRow.transform.SetParent(host.transform, false);
                var br = btnRow.AddComponent<HorizontalLayoutGroup>();
                br.spacing = 14f; br.childControlWidth = true; br.childControlHeight = true; br.childForceExpandWidth = true;
                btnRow.AddComponent<LayoutElement>().preferredHeight = 140f;
                MakePull(btnRow.transform, catalog, "1회 뽑기", "1,000 고대주화", true);
                MakePull(btnRow.transform, catalog, "10연 뽑기", "10,000 고대주화", false);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "07_gachawidgets.png"));
            Object.DestroyImmediate(host);
        }

        private static void MakePill(Transform parent, UIViewCatalog catalog, string text, Color c)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemRatePill);
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<RatePillView>();
            if (v != null) v.Set(text, c);
        }

        private static void MakePull(Transform parent, UIViewCatalog catalog, string title, string cost, bool afford)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemGachaPullButton);
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<GachaPullButtonView>();
            if (v != null) v.Set(title, cost, afford, catalog.iconChest);
        }

        /// <summary>장비 셀 / 전직 카드 / 강화 카드 / 스킬 행 프리팹을 샘플 데이터로 렌더링.</summary>
        private static void CaptureKingdomArmyWidgets(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || !layers.TryGetValue("LayerPanels", out var parent)) return;

            var host = new GameObject("KaWidgetPreview", typeof(RectTransform));
            var hostRt = (RectTransform)host.transform;
            hostRt.SetParent(parent, false);
            hostRt.anchorMin = new Vector2(0f, 1f); hostRt.anchorMax = new Vector2(1f, 1f); hostRt.pivot = new Vector2(0.5f, 1f);
            hostRt.anchoredPosition = new Vector2(0f, -120f);
            hostRt.offsetMin = new Vector2(40f, hostRt.offsetMin.y);
            hostRt.sizeDelta = new Vector2(-80f, 1600f);
            var col = host.AddComponent<VerticalLayoutGroup>();
            col.spacing = 20f; col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = true; col.childForceExpandHeight = false; col.padding = new RectOffset(0, 0, 0, 0);

            // 캐릭터 시트 (스탯 블록 + 상세 방정식 롤다운 검증)
            if (catalog.panelKACharacterSheet != null)
            {
                var cs = (GameObject)PrefabUtility.InstantiatePrefab(catalog.panelKACharacterSheet);
                cs.transform.SetParent(host.transform, false);
                cs.AddComponent<LayoutElement>().preferredHeight = 620f;
                var v = cs.GetComponent<KACharacterSheetView>();
                if (v != null)
                {
                    if (v.jobLabel != null) v.jobLabel.text = "기사 (Knight)";
                    if (v.atkValueLabel != null) v.atkValueLabel.text = "1,240";
                    if (v.moveValueLabel != null) v.moveValueLabel.text = "5";
                    if (v.hpFill != null) { v.hpFill.fillAmount = 0.7f; v.hpFill.color = new Color(0.8f, 0.6f, 0.2f, 1f); }
                    if (v.hpValueLabel != null) v.hpValueLabel.text = "2,520 / 3,600";
                    if (v.equippedLabel != null) v.equippedLabel.text = "롱소드 +3 (ATK +42)";
                    // 상세 롤다운 펼친 상태로 샘플 방정식
                    if (v.detailRoot != null) v.detailRoot.SetActive(true);
                    SampleEquation(v.atkEqRow, catalog, "50", "42", "1.08", "99");
                    SampleEquation(v.hpEqRow, catalog, "600", "10", "2.00", "1220");
                    if (v.termPopupLabel != null) v.termPopupLabel.text = "장비 공격력";
                    // 스킬 샘플(스탯 탭 하단)
                    if (v.skillsRoot != null)
                    {
                        MakeSkillRow(v.skillsRoot, catalog, "기본 공격", "적 1체 공격", false);
                        MakeSkillRow(v.skillsRoot, catalog, "수호의 오라", "아군 방어 강화", true);
                        MakeSkillRow(v.skillsRoot, catalog, "강철 의지", "체력 낮을 때 회복", false);
                    }
                }
            }

            // 장비 셀 / 전직 카드 그리드
            if (catalog.itemEquipCell != null || catalog.itemJobCard != null)
            {
                var gridGo = new GameObject("Grid", typeof(RectTransform));
                gridGo.transform.SetParent(host.transform, false);
                var g = gridGo.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(180f, 240f); g.spacing = new Vector2(12f, 12f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 5;
                gridGo.AddComponent<LayoutElement>().preferredHeight = 500f;

                if (catalog.itemEquipCell != null)
                {
                    MakeEquipCell(gridGo.transform, catalog, "롱소드 +3", UguiTheme.RarityRare, "ATK +42  HP +10", UguiTheme.RarityRare, true, false, "장착 중");
                    MakeEquipCell(gridGo.transform, catalog, "고대 검 +1", UguiTheme.RarityEpic, "ATK +88", UguiTheme.RarityEpic, false, false, null);
                    MakeEquipCell(gridGo.transform, catalog, "낡은 단검", UguiTheme.RarityNormal, "ATK +5", UguiTheme.RarityNormal, false, true, null);
                }
                if (catalog.itemJobCard != null)
                {
                    // 실제 JobData 로드 → 진짜 jobSprite 로 초상화 메달리온 균일성 검증
                    var knight = AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Knight.asset");
                    var archer = AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Archer.asset");
                    var mage = AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Mage.asset");
                    MakeJobCard(gridGo.transform, catalog, knight, "Knight", "현재", UguiTheme.AccentGoldStrong, "HP 600 / ATK 50", "무료 재전직", UguiTheme.SuccessGreenBright, null, new Color(1f, 230f/255f, 100f/255f, 0.12f), new Color(1f, 230f/255f, 100f/255f, 1f));
                    MakeJobCard(gridGo.transform, catalog, archer, "Archer", "전직가능", UguiTheme.SuccessGreenBright, "HP 320 / ATK 80", "전직 파편 40/40", UguiTheme.SuccessGreenBright, null, new Color(1f,1f,1f,0.07f), null);
                    MakeJobCard(gridGo.transform, catalog, mage, "Mage", "전직가능", UguiTheme.WarnRed, "HP 400 / ATK 60", "전직 파편 30/40", UguiTheme.WarnRed, "선행: Archer 필요", new Color(1f,1f,1f,0.07f), null);
                }
            }

            // 강화 카드
            if (catalog.itemEnhanceCard != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemEnhanceCard);
                go.transform.SetParent(host.transform, false);
                var v = go.GetComponent<EnhanceCardView>();
                if (v != null)
                {
                    v.Set("공격력", "Lv. 12", "현재 효과  +24%");
                    if (catalog.itemGachaPullButton != null)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            var b = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemGachaPullButton);
                            b.transform.SetParent(v.ButtonRow, false);
                            b.GetComponent<GachaPullButtonView>()?.Set(i == 0 ? "강화 x1" : "강화 x10", i == 0 ? "50 G" : "480 G", i == 0);
                        }
                    }
                }
            }

            // 스킬 행
            if (catalog.itemSkillRow != null)
            {
                MakeSkillRow(host.transform, catalog, "강타", "적에게 200% 피해", false);
                MakeSkillRow(host.transform, catalog, "인내", "받는 피해 15% 감소", true);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "08_kawidgets.png"));
            Object.DestroyImmediate(host);
        }

        /// <summary>KA 장비 탭 / 전직 상세 서브패널의 러스틱 스타일(아이템 슬롯·초상화 메달리온·버튼 컬러) 검증. 11_kasub.png</summary>
        private static void CaptureKASubPanels(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || !layers.TryGetValue("LayerPanels", out var parent)) return;

            var host = new GameObject("KaSubPreview", typeof(RectTransform));
            var hostRt = (RectTransform)host.transform;
            hostRt.SetParent(parent, false);
            hostRt.anchorMin = new Vector2(0f, 1f); hostRt.anchorMax = new Vector2(1f, 1f); hostRt.pivot = new Vector2(0.5f, 1f);
            hostRt.anchoredPosition = new Vector2(0f, -60f);
            hostRt.offsetMin = new Vector2(40f, hostRt.offsetMin.y);
            hostRt.sizeDelta = new Vector2(-80f, 1760f);
            var col = host.AddComponent<VerticalLayoutGroup>();
            col.spacing = 26f; col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = true; col.childForceExpandHeight = false;

            var archer = AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Archer.asset");

            // 장비 탭 (장착 카드 슬롯 + 보유 장비 그리드)
            if (catalog.panelKAEquipment != null)
            {
                var eq = (GameObject)PrefabUtility.InstantiatePrefab(catalog.panelKAEquipment);
                eq.transform.SetParent(host.transform, false);
                eq.AddComponent<LayoutElement>().preferredHeight = 560f;
                var v = eq.GetComponent<KAEquipmentView>();
                if (v != null)
                {
                    if (v.equippedFrame != null) v.equippedFrame.gameObject.SetActive(true);
                    if (v.equippedIcon != null) { v.equippedIcon.sprite = catalog.iconSword; v.equippedIcon.enabled = true; }
                    if (v.equippedNameLabel != null) v.equippedNameLabel.text = "롱소드 +3";
                    if (v.equippedStatLabel != null) v.equippedStatLabel.text = "ATK +42  HP +10";
                    if (v.emptyLabel != null) v.emptyLabel.gameObject.SetActive(false);
                    if (v.inventoryGrid != null)
                    {
                        MakeEquipCell(v.inventoryGrid, catalog, "고대 검 +1", UguiTheme.RarityEpic, "ATK +88", UguiTheme.RarityEpic, false, false, null);
                        MakeEquipCell(v.inventoryGrid, catalog, "롱소드 +3", UguiTheme.RarityRare, "ATK +42", UguiTheme.RarityRare, true, false, "장착 중");
                        MakeEquipCell(v.inventoryGrid, catalog, "낡은 단검", UguiTheme.RarityNormal, "ATK +5", UguiTheme.RarityNormal, false, false, null);
                    }
                }
            }

            // 전직 상세 (초상화 메달리온 + 스탯 비교 + 전직 버튼)
            if (catalog.panelKAJobDetail != null)
            {
                var jd = (GameObject)PrefabUtility.InstantiatePrefab(catalog.panelKAJobDetail);
                jd.transform.SetParent(host.transform, false);
                jd.AddComponent<LayoutElement>().preferredHeight = 900f;
                var v = jd.GetComponent<KAJobDetailView>();
                if (v != null)
                {
                    if (v.image != null && archer != null) { v.image.sprite = archer.Portrait; v.image.enabled = archer.Portrait != null; v.image.preserveAspect = true; }
                    if (v.jobNameLabel != null) v.jobNameLabel.text = "궁수 (Archer)";
                    if (v.stateBadge != null) v.stateBadge.text = "전직 가능";
                    if (v.roleLabel != null) v.roleLabel.text = "원거리 물리 딜러";
                    if (v.freeLabel != null) v.freeLabel.gameObject.SetActive(false);
                    if (v.fragCondValue != null) v.fragCondValue.text = "40 / 40";
                    if (v.prereqCondValue != null) v.prereqCondValue.text = "충족";
                    if (v.compareTable != null && catalog.itemStatCompareRow != null)
                    {
                        AddCompareSample(v.compareTable, catalog, "스탯", "현재", "신규", "변화");
                        AddCompareSample(v.compareTable, catalog, "HP", "600", "320", "▼280");
                        AddCompareSample(v.compareTable, catalog, "공격력", "50", "80", "▲30");
                    }
                    if (v.skillList != null)
                        MakeSkillRow(v.skillList, catalog, "집중 사격", "3회 연속 타격", false);
                    if (v.changeRow != null && catalog.itemActionButton != null)
                    {
                        var b = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemActionButton);
                        b.transform.SetParent(v.changeRow, false);
                        b.GetComponent<ActionButtonView>()?.Set("전직하기 (파편 40개)", UguiTheme.BtnSpend, true);
                    }
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "11_kasub.png"));
            Object.DestroyImmediate(host);
        }

        private static void AddCompareSample(RectTransform table, UIViewCatalog cat, string a, string b, string c, string d)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemStatCompareRow);
            go.transform.SetParent(table, false);
            var v = go.GetComponent<StatCompareRowView>();
            if (v == null) return;
            if (v.cell0 != null) v.cell0.text = a;
            if (v.cell1 != null) v.cell1.text = b;
            if (v.cell2 != null) v.cell2.text = c;
            if (v.cell3 != null) v.cell3.text = d;
        }

        /// <summary>파티 HUD(초상화 메달리온·HP바·스킬 슬롯) 러스틱 스타일 검증. 12_partyhud.png</summary>
        private static void CapturePartyHud(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || catalog.hudParty == null || !layers.TryGetValue("LayerPopups", out var parent)) return;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.hudParty);
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var v = go.GetComponent<PartyHudView>();
            if (v != null)
            {
                var jobs = new[]
                {
                    AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Knight.asset"),
                    AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Archer.asset"),
                    AssetDatabase.LoadAssetAtPath<JobData>("Assets/_Project/Scripts/Player/Job/SO/Mage.asset"),
                };
                float[] hp = { 0.85f, 0.45f, 1f };
                var icons = PartySkillIconMatrix();
                for (int i = 0; i < 3 && i < v.members.Length; i++)
                {
                    var m = v.members[i];
                    if (m == null) continue;
                    if (m.portraitImage != null && jobs[i] != null)
                    {
                        m.portraitImage.sprite = jobs[i].Portrait;
                        m.portraitImage.enabled = jobs[i].Portrait != null;
                    }
                    if (m.hpFill != null) m.hpFill.fillAmount = hp[i];
                    for (int s = 0; s < m.skills.Length; s++)
                    {
                        var slot = m.skills[s];
                        if (slot?.root == null) continue;
                        slot.root.SetActive(true);
                        ApplySkillIcon(slot, icons[i, s]);
                        bool passive = s == 1, cooling = s == 2;
                        if (slot.cooldownMask != null) slot.cooldownMask.gameObject.SetActive(cooling);
                        if (slot.cooldownLabel != null)
                        {
                            slot.cooldownLabel.gameObject.SetActive(passive || cooling);
                            slot.cooldownLabel.text = passive ? "상시" : cooling ? "5" : "";
                            slot.cooldownLabel.color = passive ? new Color(0.4f, 1f, 0.4f, 1f) : Color.white;
                            slot.cooldownLabel.fontSize = passive ? 14f : 18f;
                        }
                    }
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "12_partyhud.png"));
            Object.DestroyImmediate(go);
        }

        private static void MakeEquipCell(Transform parent, UIViewCatalog cat, string name, Color nameColor, string sub, Color rarity, bool equipped, bool dim, string state)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemEquipCell);
            go.transform.SetParent(parent, false);
            // 샘플 무기 아이콘으로 셀 레이아웃 검증 (실게임은 item.baseData.icon 사용)
            var sampleIcon = UguiGenAssets.IconSword;
            go.GetComponent<EquipCellView>()?.Set(sampleIcon, name, nameColor, sub, rarity, equipped, dim, state);
        }

        private static void MakeJobCard(Transform parent, UIViewCatalog cat, JobData job, string fallbackName, string badge, Color badgeC, string stat, string frag, Color fragC, string prereq, Color bg, Color? frame)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemJobCard);
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<JobCardView>();
            if (v == null) return;
            v.Set(job, bg, frame, badge, badgeC, stat, frag, fragC, prereq);
            if (job == null && v.nameLabel != null) v.nameLabel.text = fallbackName;
        }

        private static void MakeSkillRow(Transform parent, UIViewCatalog cat, string name, string detail, bool passive)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemSkillRow);
            go.transform.SetParent(parent, false);
            go.GetComponent<SkillRowView>()?.Set(name, detail, passive);
        }

        /// <summary>Layer Lab 원본 프리팹 갤러리 렌더 — 실제 에셋 컴포넌트 외형 확인용.</summary>
        internal static void CaptureLLGallery()
        {
            Directory.CreateDirectory(OutDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            cam.transform.position = new Vector3(0, 0, -100);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H); scaler.matchWidthOrHeight = 0.5f;

            string[] names = {
                "Button_03_Blue", "Button_01_Green", "Button_02_Red", "Button_Auto_01",
                "Tab_01", "Popup_Box_02_DecoLine_Basic_Blue", "PanelFrame_03",
                "CardFrame_02_Blue", "ItemFrame_02_Blue", "Title_LineDeco_01_l", "Slider_01_Blue",
            };
            float y = 850f;
            foreach (var n in names)
            {
                var prefab = FindLLPrefab(n);
                if (prefab == null) { Debug.LogWarning($"[LLGallery] 없음: {n}"); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var rt = inst.transform as RectTransform;
                rt.SetParent(canvas.transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, y);
                float h = rt.sizeDelta.y > 10f ? rt.sizeDelta.y : 120f;
                y -= h + 44f;
            }
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "ll_gallery.png"));
            Debug.Log("[Preview] LL 갤러리 캡처 완료: " + Path.Combine(OutDir, "ll_gallery.png"));
        }

        private static GameObject FindLLPrefab(string exactName)
        {
            foreach (var g in AssetDatabase.FindAssets($"{exactName} t:Prefab", new[] { "Assets/ExternalAssets/Layer Lab" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(path) == exactName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        private static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
