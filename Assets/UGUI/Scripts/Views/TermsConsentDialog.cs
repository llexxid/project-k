using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    internal sealed class TermsConsentDialog : MonoBehaviour
    {
        readonly TaskCompletionSource<bool> _answer = new();
        internal Task<bool> Result => _answer.Task;

        internal static TermsConsentDialog Open(TitleScreenView title)
        {
            // Reuse the title's window, button skin and localized font at their native UI scale.
            var overlay = Instantiate(title.popupLogin, title.transform, false);
            overlay.name = "TermsConsent";
            var dialog = overlay.AddComponent<TermsConsentDialog>();
            dialog.Build(title);
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            return dialog;
        }

        void Build(TitleScreenView title)
        {
            string L(string ko, string en) => title.presentation != null ? title.presentation.Localize(ko, en) : ko;
            var panel = (RectTransform)transform.Find(title.popupLoginBox.name);
            panel.gameObject.AddComponent<ModalSizeFitter>();
            var heading = panel.Find("Title").GetComponent<TMP_Text>();
            heading.text = L("게임 이용 안내", "Before you play");
            var notice = panel.Find("Subtitle").GetComponent<TMP_Text>();
            notice.text = L("이번 알파 테스트는 만 14세 이상 참여할 수 있습니다. 정식 출시 시 데모 진행도가 초기화됩니다.\n휴대폰 알림은 선택 사항이며 설정 → 기기에서 관리할 수 있습니다.",
                            "This alpha test is for ages 14 and up. Demo progress resets at the official launch.\nPhone notifications are optional. Manage them in Settings → Device.");
            notice.textWrappingMode = TextWrappingModes.Normal;
            notice.overflowMode = TextOverflowModes.Overflow;
            notice.GetComponent<LayoutElement>().preferredHeight = -1;
            var originalTerms = panel.Find("Terms");
            if (originalTerms != null) originalTerms.gameObject.SetActive(false);

            var template = panel.Find(title.btnLoginGoogle.name).GetComponent<Button>();
            foreach (var button in panel.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
            var dim = transform.Find(title.popupLoginDim.name).GetComponent<Button>();
            dim.onClick = new Button.ButtonClickedEvent();
            dim.onClick.AddListener(Cancel);
            ModalBackHandler.Bind(gameObject, Cancel);

            var documents = Row(panel, "Documents");
            AddButton(template, documents, "ReadTerms", L("이용약관 전문", "Terms of Service"), () => LegalDocuments.ShowTerms(transform));
            AddButton(template, documents, "ReadPrivacy", L("개인정보 처리 안내", "Privacy notice"), () => LegalDocuments.ShowPrivacy(transform));
            var accepted = AgreementToggle(panel, heading.font, L("[필수] 서비스 이용약관에 동의합니다.", "[Required] I agree to the Terms of Service."));
            var actions = Row(panel, "Actions");
            AddButton(template, actions, "Cancel", L("돌아가기", "Go back"), Cancel);
            var confirm = AddButton(template, actions, "Confirm", L("동의하고 계속", "Accept and continue"), () =>
            {
                if (accepted.isOn) Finish(true);
            });
            confirm.interactable = false;
            accepted.onValueChanged.AddListener(value => confirm.interactable = value);
        }

        static Transform Row(Transform parent, string name)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 96;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            return row.transform;
        }

        static Button AddButton(Button template, Transform parent, string name, string caption, Action action)
        {
            var button = Instantiate(template, parent, false);
            button.name = name;
            button.interactable = true;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => action());
            var size = button.GetComponent<LayoutElement>();
            size.preferredWidth = 0;
            size.preferredHeight = 96;
            size.flexibleWidth = 1;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = caption;
            label.fontSize = 28;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            button.gameObject.SetActive(true);
            return button;
        }

        static Toggle AgreementToggle(Transform parent, TMP_FontAsset font, string caption)
        {
            var row = new GameObject("AcceptTerms", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 98;
            row.GetComponent<Image>().color = UguiTheme.RusticSurface;
            var background = new GameObject("Checkbox", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(row.transform, false);
            var rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, .5f);
            rt.anchoredPosition = new Vector2(48, 0);
            rt.sizeDelta = new Vector2(48, 48);
            background.color = UguiTheme.BronzeLight;
            var mark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            mark.transform.SetParent(background.transform, false);
            mark.rectTransform.sizeDelta = new Vector2(34, 34);
            mark.sprite = UIManager.Instance.Catalog.iconCheck;
            mark.color = UguiTheme.RusticPanelDeep;
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(row.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(88, 5);
            label.rectTransform.offsetMax = new Vector2(-14, -5);
            label.font = font;
            label.fontSize = 29;
            label.text = caption;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = UguiTheme.Parchment;
            label.raycastTarget = false;
            var toggle = row.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.SetIsOnWithoutNotify(false);
            return toggle;
        }

        internal void Cancel() => Finish(false);

        void Finish(bool accepted)
        {
            if (_answer.Task.IsCompleted) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
            _answer.TrySetResult(accepted);
        }

        void OnDestroy() => _answer.TrySetResult(false);
    }
}
