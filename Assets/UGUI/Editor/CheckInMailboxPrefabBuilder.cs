using KingdomIdle.CheckIn;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Inspector에서 편집한 출석·우편 에셋을 카탈로그에 연결한다.</summary>
    public static class CheckInMailboxPrefabBuilder
    {
        [MenuItem("KingdomIdle/UGUI/Connect CheckIn and Mailbox")]
        public static void Build()
        {
            var table = AssetDatabase.LoadAssetAtPath<CheckInRewardTableSO>("Assets/_Project/ScriptableObjects/CheckInRewards.asset");
            var slot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Items/Item_RewardSlot.prefab");
            var row = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Items/Item_MailRow.prefab");
            var popup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Popups/Popup_RewardInfo.prefab");
            var checkIn = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Panels/Panel_CheckIn.prefab");
            var mailbox = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Panels/Panel_Mailbox.prefab");
            if (table == null || slot == null || row == null || popup == null || checkIn == null || mailbox == null)
            {
                Debug.LogError("[CheckInMailbox] 기존 보상표 또는 전용 프리팹이 없습니다. 에셋을 복구한 뒤 다시 연결하세요.");
                return;
            }
            var catalog = PrefabGenUtil.GetOrCreateCatalog();
            catalog.checkInRewards = table;
            catalog.itemRewardSlot = slot;
            catalog.itemMailRow = row;
            catalog.popupRewardInfo = popup;
            catalog.panelCheckIn = checkIn;
            catalog.panelMailbox = mailbox;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("[CheckInMailbox] 기존 프리팹·보상표 카탈로그 연결 완료");
        }

        internal static void ApplyMenu(GameObject root)
        {
            var main = root.GetComponent<MainScreenView>();
            if (main == null || main.btnMenuInventory == null) return;
            if (main.btnMenuCheckIn == null)
            {
                var copy = Object.Instantiate(main.btnMenuInventory.gameObject, main.btnMenuInventory.transform.parent, false);
                copy.name = "BtnMenuCheckIn"; main.btnMenuCheckIn = copy.GetComponent<UnityEngine.UI.Button>();
            }
            if (main.btnMenuMail == null)
            {
                var copy = Object.Instantiate(main.btnMenuInventory.gameObject, main.btnMenuInventory.transform.parent, false);
                copy.name = "BtnMenuMail"; main.btnMenuMail = copy.GetComponent<UnityEngine.UI.Button>();
            }
            SetMenu(main.btnMenuCheckIn, main.btnMenuInventory, "출석", F.Catalog.iconCheck);
            SetMenu(main.btnMenuMail, main.btnMenuInventory, "우편함", F.Catalog.iconEnvelope);
            main.btnMenuCheckIn.transform.SetSiblingIndex(main.btnMenuInventory.transform.GetSiblingIndex() + 1);
            main.btnMenuMail.transform.SetSiblingIndex(main.btnMenuCheckIn.transform.GetSiblingIndex() + 1);
        }
        private static void SetMenu(UnityEngine.UI.Button button, UnityEngine.UI.Button template, string title, Sprite icon)
        {
            button.gameObject.SetActive(true);
            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            EditorUtility.CopySerialized(template.GetComponent<LayoutElement>(), layout);
            var background = button.GetComponent<UnityEngine.UI.Image>();
            var source = template.GetComponent<UnityEngine.UI.Image>();
            background.sprite = source.sprite; background.type = source.type; background.color = source.color;
            var frame = button.transform.Find("Frame"); if (frame != null) frame.gameObject.SetActive(false);
            var label = button.transform.Find("MenuLabel");
            if (label == null)
            {
                label = Object.Instantiate(template.transform.Find("MenuLabel"), button.transform, false);
                label.name = "MenuLabel";
            }
            label.GetComponent<TMP_Text>().text = title;
            var image = button.transform.Find("Icon");
            if (image != null)
            {
                EditorUtility.CopySerialized(template.transform.Find("Icon").GetComponent<RectTransform>(), image.GetComponent<RectTransform>());
                image.GetComponent<UnityEngine.UI.Image>().sprite = icon;
            }
        }
    }
}
