using UnityEditor;
using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>UIViewCatalog 에셋에 생성된 프리팹/공용 에셋을 배선한다.</summary>
    internal static class CatalogGen
    {
        /// <summary>
        /// 폰트/SFX/공용 스프라이트/픽셀 키트 배선.
        /// 프리팹 생성 전에 실행해야 팩토리가 카탈로그의 키트 스프라이트를 쓸 수 있다.
        /// </summary>
        internal static void AssignSharedAssets(UIViewCatalog catalog)
        {
            catalog.defaultFont = UguiGenAssets.Font;
            catalog.damageTextMaterial = GetOrCreateDamageOutlineMaterial();

            catalog.roundedRect = PrefabGenUtil.GetOrCreateRoundedRect();
            catalog.circle = PrefabGenUtil.GetOrCreateCircle();

            catalog.panelOpenSfx = UguiGenAssets.SfxPanelOpen;
            catalog.panelCloseSfx = UguiGenAssets.SfxPanelClose;
            catalog.buttonClickSfx = UguiGenAssets.SfxButtonClick;

            // ── 픽셀 아트 키트 ──
            catalog.kitWindow = UguiGenAssets.KitWindow;
            catalog.kitTitleBar = UguiGenAssets.KitTitleBar;
            catalog.kitCard = UguiGenAssets.KitCard;
            catalog.kitSlot = UguiGenAssets.KitSlot;
            catalog.kitEllipse = UguiGenAssets.KitEllipse;
            catalog.kitBtnBlue = UguiGenAssets.KitBtnBlue;
            catalog.kitBtnBlueDown = UguiGenAssets.KitBtnBlueDown;
            catalog.kitBtnGreen = UguiGenAssets.KitBtnGreen;
            catalog.kitBtnGreenDown = UguiGenAssets.KitBtnGreenDown;
            catalog.kitBtnGrey = UguiGenAssets.KitBtnGrey;
            catalog.kitBtnGreyDown = UguiGenAssets.KitBtnGreyDown;
            catalog.kitBtnInactive = UguiGenAssets.KitBtnInactive;
            catalog.kitToggleOn = UguiGenAssets.KitToggleOn;
            catalog.kitToggleOff = UguiGenAssets.KitToggleOff;
            catalog.kitBarTrack = UguiGenAssets.KitBarTrack;
            catalog.kitFillBlue = UguiGenAssets.KitFillBlue;
            catalog.kitFillGreen = UguiGenAssets.KitFillGreen;
            catalog.kitFillRed = UguiGenAssets.KitFillRed;
            catalog.kitFillYellow = UguiGenAssets.KitFillYellow;
            catalog.kitBarHandle = UguiGenAssets.KitBarHandle;

            catalog.iconX = UguiGenAssets.IconX;
            catalog.iconCheck = UguiGenAssets.IconCheck;
            catalog.iconArrowLeft = UguiGenAssets.IconArrowLeft;
            catalog.iconSwords = UguiGenAssets.IconSwords;
            catalog.iconHelmet = UguiGenAssets.IconHelmet;
            catalog.iconStar = UguiGenAssets.IconStar;
            catalog.iconBag = UguiGenAssets.IconBag;
            catalog.iconEnvelope = UguiGenAssets.IconEnvelope;
            catalog.iconRepeat = UguiGenAssets.IconRepeat;

            catalog.iconUser = UguiGenAssets.IconUserRole;
            catalog.iconSword = UguiGenAssets.IconSword;
            catalog.iconBook = UguiGenAssets.IconBook;
            catalog.iconWand = UguiGenAssets.IconWand;
            catalog.iconChest = UguiGenAssets.IconChest;
            catalog.iconGem = UguiGenAssets.IconGem;
            catalog.iconCoin = UguiGenAssets.IconCoin;

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[UguiGen] 카탈로그 공용 에셋 배선 완료");
        }

        internal static void AssignPrefabs(UIViewCatalog catalog)
        {
            string p = PrefabGenUtil.PrefabRoot;

            catalog.screenTitle = Load($"{p}/Screens/Screen_Title.prefab");
            catalog.screenMain = Load($"{p}/Screens/Screen_Main.prefab");

            catalog.panelPlaceholder = Load($"{p}/Panels/Panel_Placeholder.prefab");
            catalog.panelGuide = Load($"{p}/Panels/Panel_Guide.prefab");
            catalog.panelGacha = Load($"{p}/Panels/Panel_Gacha.prefab");
            catalog.panelKingdomArmy = Load($"{p}/Panels/Panel_KingdomArmy.prefab");
            catalog.panelDevelopment = Load($"{p}/Panels/Panel_Development.prefab");
            catalog.panelInventory = Load($"{p}/Panels/Panel_Inventory.prefab");

            catalog.popupGachaResult = Load($"{p}/Popups/Popup_GachaResult.prefab");

            catalog.overlayLoading = Load($"{p}/Overlays/Overlay_Loading.prefab");
            catalog.overlayToast = Load($"{p}/Overlays/Overlay_Toast.prefab");
            catalog.overlaySettings = Load($"{p}/Overlays/Overlay_Settings.prefab");

            catalog.hudParty = Load($"{p}/Huds/Hud_Party.prefab");
            catalog.hudMageTower = Load($"{p}/Huds/Hud_MageTower.prefab");

            catalog.itemNavTabButton = Load($"{p}/Items/Item_NavTabButton.prefab");
            catalog.itemGachaCard = Load($"{p}/Items/Item_GachaCard.prefab");
            catalog.itemCurrencyLine = Load($"{p}/Items/Item_CurrencyLine.prefab");
            catalog.itemDamageText = Load($"{p}/Items/Item_DamageText.prefab");
            catalog.itemGachaPullButton = Load($"{p}/Items/Item_GachaPullButton.prefab");
            catalog.itemRatePill = Load($"{p}/Items/Item_RatePill.prefab");
            catalog.itemActionButton = Load($"{p}/Items/Item_ActionButton.prefab");
            catalog.itemEquipCell = Load($"{p}/Items/Item_EquipCell.prefab");
            catalog.itemJobCard = Load($"{p}/Items/Item_JobCard.prefab");
            catalog.itemEnhanceCard = Load($"{p}/Items/Item_EnhanceCard.prefab");
            catalog.itemSkillRow = Load($"{p}/Items/Item_SkillRow.prefab");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[UguiGen] 카탈로그 배선 완료");
        }

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                Debug.LogWarning($"[UguiGen] 카탈로그 배선: 프리팹 없음 — {path}");
            return go;
        }

        /// <summary>데미지 텍스트용 아웃라인 머티리얼 프리셋 (공유 머티리얼 오염 방지).</summary>
        internal static Material GetOrCreateDamageOutlineMaterial()
        {
            const string path = "Assets/UGUI/Fonts/Galmuri11 SDF - DamageOutline.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var font = UguiGenAssets.Font;
            if (font == null || font.material == null)
            {
                Debug.LogWarning("[UguiGen] 폰트가 없어 데미지 아웃라인 머티리얼을 만들 수 없습니다.");
                return null;
            }

            PrefabGenUtil.EnsureFolder("Assets/UGUI/Fonts");

            var mat = new Material(font.material);
            mat.name = "Galmuri11 SDF - DamageOutline";
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.70f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
