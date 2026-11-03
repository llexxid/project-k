using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    internal sealed class AlphaAgeGate : MonoBehaviour
    {
        const int MinimumAge = 14;
        const string RetryKey = "age.alpha14.v1.retryAfterUtc";
        Action _eligible, _cancel;
        TitleLobbyPresentation _language;
        TMP_Text _notice;
        TMP_InputField _age;
        Button _next, _edit;
        int _reviewedAge;
        bool _reviewing, _finished;
        internal static bool IsBlocked =>
            long.TryParse(PlayerPrefs.GetString(RetryKey, ""), out long retry) &&
            retry > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        internal static void Show(Transform parent, Action onEligible, Action onCancel)
        {
            var title = parent != null ? parent.GetComponentInParent<TitleScreenView>() : null;
            if (title == null || !title.isActiveAndEnabled || title.popupLogin == null) { onCancel(); return; }
            var overlay = Instantiate(title.popupLogin, parent, false);
            overlay.name = "AlphaAgeGate";
            var gate = overlay.AddComponent<AlphaAgeGate>();
            gate._eligible = onEligible;
            gate._cancel = onCancel;
            gate.Build(title);
            overlay.transform.SetAsLastSibling();
            overlay.SetActive(true);
        }

        string L(string ko, string en) => _language != null ? _language.Localize(ko, en) : ko;

        void Build(TitleScreenView title)
        {
            _language = title.presentation;
            var panel = transform.Find(title.popupLoginBox.name);
            panel.gameObject.AddComponent<ModalSizeFitter>();
            var heading = panel.Find("Title").GetComponent<TMP_Text>();
            heading.text = L("나이 확인", "Age check");
            _notice = panel.Find("Subtitle").GetComponent<TMP_Text>();
            _notice.textWrappingMode = TextWrappingModes.Normal;
            _notice.overflowMode = TextOverflowModes.Overflow;
            _notice.GetComponent<LayoutElement>().preferredHeight = -1;
            panel.Find("Terms")?.gameObject.SetActive(false);
            var dim = transform.Find(title.popupLoginDim.name).GetComponent<Button>();
            dim.onClick = new Button.ButtonClickedEvent();
            dim.onClick.AddListener(Cancel);
            ModalBackHandler.Bind(gameObject, Cancel);

            _next = panel.Find(title.btnLoginGoogle.name).GetComponent<Button>();
            _edit = panel.Find(title.btnLoginGuest.name).GetComponent<Button>();
            panel.Find(title.btnLoginApple.name).gameObject.SetActive(false);
            var cancel = Instantiate(_edit, panel, false);
            Bind(cancel, "Cancel", L("돌아가기", "Go back"), Cancel);
            Bind(_edit, "Edit", L("수정하기", "Edit age"), StartEntry);
            _next.onClick = new Button.ButtonClickedEvent();
            _next.onClick.AddListener(Submit);
            _age = MakeInput(panel, heading.font);
            _age.transform.SetSiblingIndex(_edit.transform.GetSiblingIndex());
            _edit.transform.SetSiblingIndex(_next.transform.GetSiblingIndex());
            _age.onValueChanged.AddListener(_ => _next.interactable = TryGetAge(out _reviewedAge));

            if (IsBlocked) ShowUnavailable();
            else StartEntry();
        }

        TMP_InputField MakeInput(Transform parent, TMP_FontAsset font)
        {
            var row = new GameObject("Age", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 106;
            var background = row.GetComponent<Image>();
            background.color = UguiTheme.RusticSurface;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(row.transform, false);
            Stretch(viewport, 24);
            TextMeshProUGUI Label(string name, string caption)
            {
                var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(viewport, false);
                Stretch(label.rectTransform, 0);
                label.font = font;
                label.fontSize = 32;
                label.color = UguiTheme.Parchment;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.raycastTarget = false;
                label.text = caption;
                return label;
            }
            var placeholder = Label("Placeholder", L("만 나이 입력", "Age in years"));
            placeholder.alpha = .7f;
            var input = row.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = viewport;
            input.textComponent = Label("Value", "");
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.keyboardType = TouchScreenKeyboardType.NumberPad;
            input.characterLimit = 3;
            return input;
        }

        static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, 0); rect.offsetMax = new Vector2(-padding, 0);
        }
        static void Bind(Button button, string name, string caption, Action click)
        {
            button.name = name; button.gameObject.SetActive(true);
            button.interactable = true;
            button.GetComponentInChildren<TMP_Text>(true).text = caption;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => click());
        }
        bool TryGetAge(out int age) =>
            int.TryParse(_age.text, NumberStyles.None, CultureInfo.InvariantCulture, out age) && age <= 125;

        void StartEntry()
        {
            _reviewing = false;
            _notice.text = L("현재 만 나이를 숫자로 입력해 주세요.", "Enter your current age in years.");
            _age.gameObject.SetActive(true);
            _edit.gameObject.SetActive(false);
            _next.name = "Continue";
            _next.GetComponentInChildren<TMP_Text>(true).text = L("계속", "Continue");
            _next.interactable = TryGetAge(out _reviewedAge);
        }

        void Submit()
        {
            if (_finished || !_next.isActiveAndEnabled || !_next.interactable) return;
            if (!_reviewing)
            {
                if (!TryGetAge(out _reviewedAge)) return;
                _reviewing = true;
                _age.DeactivateInputField();
                _age.gameObject.SetActive(false);
                _edit.gameObject.SetActive(true);
                _notice.text = L($"입력한 만 나이가 {_reviewedAge}세인가요?", $"Is your age {_reviewedAge}?");
                _next.name = "Confirm";
                _next.GetComponentInChildren<TMP_Text>(true).text = L("확인", "Confirm");
                return;
            }
            if (_reviewedAge >= MinimumAge) { Finish(true); return; }
            PlayerPrefs.SetString(RetryKey, DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            ShowUnavailable();
        }

        void ShowUnavailable()
        {
            _age.SetTextWithoutNotify("");
            _reviewedAge = 0;
            _age.gameObject.SetActive(false);
            _edit.gameObject.SetActive(false);
            _next.gameObject.SetActive(false);
            _notice.text = L("연령 기준에 따라 이번 비공개 테스트에 참여할 수 없습니다.",
                             "You are not eligible for this closed test based on your age.");
        }

        void Cancel() => Finish(false);
        void Finish(bool eligible)
        {
            if (_finished) return;
            _finished = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            if (eligible) _eligible(); else _cancel();
        }
        void OnDisable() { if (!_finished) Finish(false); }
        void OnDestroy() { if (!_finished) { _finished = true; _cancel?.Invoke(); } }
    }
}
