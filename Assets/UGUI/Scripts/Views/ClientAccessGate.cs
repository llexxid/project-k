using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using Scripts.Core;
using TMPro;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    public sealed class ClientAccessGate : MonoBehaviour
    {
        private readonly TaskCompletionSource<bool> _completion = new();
        private PlayFabClientInstanceAPI _client;
        private Func<bool> _isCurrent;
        private TMP_Text _title, _message;
        private UnityEngine.UI.Button _retry, _update;
        private string _updateUrl;
        private bool _checking;
        private int _requestId;

#if UNITY_EDITOR
        public static bool SkipInEditor
        {
            get => UnityEditor.SessionState.GetBool("KingdomIdle.SkipClientAccessCheck", false);
            set => UnityEditor.SessionState.SetBool("KingdomIdle.SkipClientAccessCheck", value);
        }
#endif

        public static Task<bool> WaitAsync(PlayFabClientInstanceAPI client, Func<bool> isCurrent)
        {
#if UNITY_EDITOR
            if (SkipInEditor)
            {
                Debug.LogWarning("[ClientAccessGate] Editor-only access check bypass is enabled.");
                return Task.FromResult(true);
            }
#endif
            var host = UIManager.Instance;
            if (host == null || host.Catalog == null || host.Catalog.defaultFont == null || host.LayerOverlays == null)
                throw new InvalidOperationException("Client access UI is not ready.");
            var root = Rect("ClientAccessGate", host.LayerOverlays);
            var gate = root.gameObject.AddComponent<ClientAccessGate>();
            gate._client = client;
            gate._isCurrent = isCurrent;
            gate.BuildView(host.Catalog);
            gate.Check();
            return gate._completion.Task;
        }

        private async void Check()
        {
            if (_checking || _completion.Task.IsCompleted) return;
            _checking = true;
            int request = ++_requestId;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            _retry.interactable = false;
            _update.gameObject.SetActive(false);
            _title.text = "접속 확인";
            _message.text = "게임 접속 가능 여부를 확인하고 있습니다.";
            try
            {
                var received = new TaskCompletionSource<GetTitleDataResult>();
                _client.GetTitleData(new GetTitleDataRequest { Keys = new List<string> { ClientAccessPolicy.TitleDataKey } },
                    result => received.TrySetResult(result), error => received.TrySetResult(null));
                await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(15)));
                if (this == null || request != _requestId || !_isCurrent()) return;
                if (!received.Task.IsCompleted || received.Task.Result == null)
                    throw new InvalidOperationException("Client access settings could not be retrieved.");
                var data = received.Task.Result.Data;
                // A successful response without the key keeps pre-configuration clients working.
                if (data == null || !data.TryGetValue(ClientAccessPolicy.TitleDataKey, out string json))
                {
                    Finish(true);
                    return;
                }
                var policy = ClientAccessPolicy.Parse(json);
                switch (policy.Evaluate(Application.version))
                {
                    case ClientAccessState.Allowed:
                        Finish(true);
                        return;
                    case ClientAccessState.Maintenance:
                        _title.text = "서버 점검 중";
                        _message.text = string.IsNullOrWhiteSpace(policy.Message)
                            ? "서버 점검 중입니다.\n잠시 후 다시 시도해 주세요." : policy.Message;
                        break;
                    case ClientAccessState.UpdateRequired:
                        _title.text = "업데이트 안내";
                        _message.text = $"새 버전으로 업데이트해 주세요.\n\n현재 버전 {Application.version}\n최소 지원 버전 {policy.MinimumVersion}";
                        _updateUrl = policy.UpdateUrl;
                        _update.gameObject.SetActive(true);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (this == null || request != _requestId || !_isCurrent()) return;
                Debug.LogWarning("[ClientAccessGate] Access check failed: " + ex.GetType().Name);
                _title.text = "접속 확인 실패";
                _message.text = "접속 정보를 확인하지 못했습니다.\n인터넷 연결을 확인한 뒤 다시 시도해 주세요.";
            }
            finally
            {
                if (this != null && request == _requestId)
                {
                    _checking = false;
                    _retry.interactable = true;
                    if (gameObject.activeInHierarchy)
                        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_retry.gameObject);
                }
            }
        }

        private void Update()
        {
            if (_isCurrent != null && !_isCurrent()) Finish(false);
        }

        private void Finish(bool allowed)
        {
            gameObject.SetActive(false);
            _completion.TrySetResult(allowed);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _requestId++;
            _completion.TrySetResult(false);
        }

        private void BuildView(UIViewCatalog catalog)
        {
            var root = (RectTransform)transform;
            Stretch(root);
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color = UguiTheme.DimHeavy;
            ModalBackHandler.Bind(gameObject, () => { });
            var panel = Rect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(960, 860);
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.sprite = catalog.kitWindow;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.color = UguiTheme.RusticPanelDeep;
            panel.gameObject.AddComponent<ModalSizeFitter>();
            _title = Text("Title", panel, catalog, 44, UguiTheme.AccentGold, 44, 100);
            _message = Text("Message", panel, catalog, 34, UguiTheme.Parchment, 158, 370);
            _message.enableAutoSizing = true;
            _message.fontSizeMin = 26;
            _message.fontSizeMax = 34;
            _update = Button("Update", "업데이트", panel, catalog, 564, () => Application.OpenURL(_updateUrl));
            _retry = Button("Retry", "다시 확인", panel, catalog, 700, Check);
            UITween.PopIn(panel, .2f, .96f);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Position(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(48, -top - height); rect.offsetMax = new Vector2(-48, -top);
        }

        private static TMP_Text Text(string name, Transform parent, UIViewCatalog catalog, float size, Color color, float top, float height)
        {
            var rect = Rect(name, parent);
            Position(rect, top, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = catalog.defaultFont; label.fontSize = size; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.richText = false; label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, string text, Transform parent, UIViewCatalog catalog, float top, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent);
            Position(rect, top, 112);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            UguiPixelSkin.ApplyButton(image, button, UguiTheme.BtnConfirm, catalog);
            var label = Text("Label", rect, catalog, 36, UguiTheme.Parchment, 0, 112);
            label.text = text;
            button.onClick.AddListener(action);
            return button;
        }
    }
}
