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
            UITween.PopIn((RectTransform)overlay.transform.Find(title.popupLoginBox.name), .18f, .96f);
            return dialog;
        }

        void Build(TitleScreenView title)
        {
            string L(string ko, string en) => title.presentation != null ? title.presentation.Localize(ko, en) : ko;
            var panel = (RectTransform)transform.Find(title.popupLoginBox.name);
            panel.gameObject.AddComponent<ModalSizeFitter>();
            var heading = panel.Find("Title").GetComponent<TMP_Text>();
            heading.text = L("이용약관 동의", "Terms and consent");
            var notice = panel.Find("Subtitle").GetComponent<TMP_Text>();
            notice.text = L("정식 출시 시 데모 진행도가 초기화됩니다.\n휴대폰 알림은 선택 사항이며 기기 설정에서 변경할 수 있습니다.",
                            "Demo progress resets at launch.\nNotifications are optional. You can change them in your device settings.");
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

            var all = AgreementToggle(panel, heading.font, "AcceptAll", L("전체 동의", "Agree to all"), true);
            var age = AgreementToggle(panel, heading.font, "AcceptAge", L("[필수] 만 14세 이상입니다", "[Required] I am 14 or older"));
            var termsRow = Row(panel, "TermsRow");
            var terms = AgreementToggle(termsRow, heading.font, "AcceptTerms", L("[필수] 서비스 이용약관에 동의합니다", "[Required] I agree to the Terms"));
            var read = LinkButton(termsRow, heading.font, "ReadTerms", L("보기", "View"), () => LegalDocuments.ShowTerms(transform));
            read.GetComponent<LayoutElement>().preferredWidth = 144;
            LinkButton(panel, heading.font, "ReadPrivacy", L("개인정보 처리 안내 보기", "View privacy notice"), () => LegalDocuments.ShowPrivacy(transform));
            var actions = Row(panel, "Actions");
            AddButton(template, actions, "Cancel", L("돌아가기", "Go back"), Cancel);
            var confirm = AddButton(template, actions, "Confirm", L("동의하고 계속", "Agree and continue"), () =>
            {
                if (age.isOn && terms.isOn) Finish(true);
            });
            var appearance = confirm.gameObject.AddComponent<CanvasGroup>();
            var confirmFace = confirm.GetComponent<Image>();
            var confirmLabel = confirm.GetComponentInChildren<TMP_Text>(true);
            void Refresh()
            {
                bool ready = age.isOn && terms.isOn;
                all.SetIsOnWithoutNotify(ready);
                confirm.interactable = ready;
                confirmFace.color = ready ? UguiTheme.Bronze : UguiTheme.RusticSurfaceDark;
                confirmLabel.color = ready ? Color.white : UguiTheme.Parchment;
                appearance.alpha = ready ? 1f : .55f;
            }
            all.onValueChanged.AddListener(value =>
            {
                age.SetIsOnWithoutNotify(value);
                terms.SetIsOnWithoutNotify(value);
                Refresh();
            });
            age.onValueChanged.AddListener(_ => Refresh());
            terms.onValueChanged.AddListener(_ => Refresh());
            Refresh();
        }

        static Transform Row(Transform parent, string name)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 128;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
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
            size.preferredHeight = 128;
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

        static Button LinkButton(Transform parent, TMP_FontAsset font, string name, string caption, Action open)
        {
            var link = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            link.transform.SetParent(parent, false);
            link.GetComponent<LayoutElement>().preferredHeight = 116;
            var face = link.GetComponent<Image>();
            face.color = UguiTheme.RusticPanel;
            var button = link.GetComponent<Button>();
            button.targetGraphic = face;
            button.colors = UguiTheme.MakeColorBlock();
            button.onClick.AddListener(() => open());
            var label = Label(link.transform, font, caption);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Underline;
            label.color = UguiTheme.BronzeLight;
            return button;
        }

        static Toggle AgreementToggle(Transform parent, TMP_FontAsset font, string name, string caption, bool master = false)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var size = row.GetComponent<LayoutElement>();
            size.preferredHeight = 128;
            size.flexibleWidth = 1;
            row.GetComponent<Image>().color = master ? UguiTheme.RusticSurface : UguiTheme.RusticPanel;
            var background = new GameObject("Checkbox", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(row.transform, false);
            var rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, .5f);
            rt.anchoredPosition = new Vector2(48, 0);
            rt.sizeDelta = new Vector2(48, 48);
            background.color = UguiTheme.BronzeLight;
            var inset = new GameObject("Interior", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            inset.transform.SetParent(background.transform, false);
            inset.rectTransform.sizeDelta = new Vector2(40, 40);
            inset.color = UguiTheme.RusticPanelDeep;
            inset.raycastTarget = false;
            var mark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            mark.transform.SetParent(background.transform, false);
            mark.rectTransform.sizeDelta = new Vector2(34, 34);
            mark.sprite = UIManager.Instance.Catalog.iconCheck;
            mark.color = UguiTheme.Parchment;
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            var label = Label(row.transform, font, caption);
            label.rectTransform.offsetMin = new Vector2(88, 5);
            if (master)
            {
                label.fontSize = 34;
                label.fontStyle = FontStyles.Bold;
            }
            var toggle = row.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            toggle.SetIsOnWithoutNotify(false);
            return toggle;
        }

        static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string caption)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 5);
            label.rectTransform.offsetMax = new Vector2(-12, -5);
            label.font = font;
            label.fontSize = 30;
            label.text = caption;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = UguiTheme.Parchment;
            label.raycastTarget = false;
            return label;
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
