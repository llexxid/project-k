#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.UGUI;
using Protocol.Message.Dto;
using UnityEngine;

namespace KingdomIdle.Mailbox.Testing
{
    public static class MailboxUiAcceptance
    {
        private static GameObject _root;
        private static MailboxAcceptance.Fixture _fixture;
        private static MailboxPanelView _view;
        private static EquipmentManager _temporaryEquipment;

        public static object Open(int count = 100)
        {
            if (!Application.isPlaying || UIManager.Instance == null) throw new InvalidOperationException("Enter Play mode from bootstrap first.");
            Close();
            if (EquipmentManager.Instance == null)
            {
                _temporaryEquipment = new GameObject("MailboxPreviewEquipment").AddComponent<EquipmentManager>();
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(UnityEditor.AssetDatabase.FindAssets("t:EquipmentDatabase")[0]);
                var serialized = new UnityEditor.SerializedObject(_temporaryEquipment);
                serialized.FindProperty("_database").objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentDatabase>(path);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            _fixture = new MailboxAcceptance.Fixture(count);
            for (int i = 0; i < count; i++)
            {
                _fixture.Mails[i].Rewards.Add(new MailRewardDTO { Kind = RewardKind.Equipment, DefinitionId = 65540, Amount = i % 3 + 1 });
                if (i % 2 == 0) _fixture.Mails[i].Rewards.Add(new MailRewardDTO { Kind = RewardKind.Currency, CurrencyType = Protocol.Message.Data.eCurrency.Ruby, Amount = 20 });
                if (i == 3) _fixture.Mails[i].TitleKey = "mail.unregistered.title";
            }
            _root = new GameObject("MailboxIsolatedPreview", typeof(RectTransform));
            _root.SetActive(false);
            var rect = (RectTransform)_root.transform;
            rect.SetParent(UIManager.Instance.LayerPanels, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var panel = UnityEngine.Object.Instantiate(UIManager.Instance.Catalog.panelMailbox, rect, false);
            panel.GetComponent<MailboxPanelController>().UseTestService(_fixture.Service);
            _view = panel.GetComponent<MailboxPanelView>();
            _view.closeButton.onClick.AddListener(Close);
            _view.backdrop.onClick.AddListener(Close);
            ModalBackHandler.Bind(panel, Close);
            _root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            return Inspect();
        }

        public static object Inspect()
        {
            Canvas.ForceUpdateCanvases();
            var rows = _root.GetComponentsInChildren<MailRowView>(true);
            var visible = rows.Where(r => r.gameObject.activeInHierarchy).ToArray();
            var scroll = _view.content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            var list = _view.GetComponent<VirtualizedMailList>();
            float rowHeight = rows.Length > 0 ? ((RectTransform)rows[0].transform).rect.height : 0;
            return new { count = _fixture.Service.Messages.Count, pool = list.PoolCount, active = visible.Length,
                rowHeight, viewport = scroll.viewport.rect.size.ToString(), contentHeight = _view.content.rect.height,
                ids = visible.Select(r => r.MailId).ToArray(), attachments = visible.Select(r => r.slots.GetComponentsInChildren<RewardSlotView>().Length).ToArray(),
                iconCount = visible.SelectMany(r => r.slots.GetComponentsInChildren<RewardSlotView>()).Count(s => s.icon.enabled),
                listRequests = _fixture.ListCalls, claimRequests = _fixture.ClaimCalls };
        }

        public static object Scroll(float position)
        {
            var scroll = _view.content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            scroll.verticalNormalizedPosition = position;
            scroll.onValueChanged.Invoke(scroll.normalizedPosition);
            return Inspect();
        }

        public static object VerifyRecycling()
        {
            var checks = new List<string>();
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("MAIL UI ASSERT: " + label); checks.Add(label); }
            var scroll = _view.content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            var list = _view.GetComponent<VirtualizedMailList>();
            int pool = list.PoolCount, requests = _fixture.ListCalls;
            var originalIds = _root.GetComponentsInChildren<MailRowView>(true).Select(r => r.GetInstanceID()).OrderBy(i => i).ToArray();
            var visited = new HashSet<long>();
            for (int i = 0; i <= 100; i++)
            {
                scroll.verticalNormalizedPosition = 1f - i / 100f;
                scroll.onValueChanged.Invoke(scroll.normalizedPosition);
                Canvas.ForceUpdateCanvases();
                foreach (var current in _root.GetComponentsInChildren<MailRowView>())
                {
                    visited.Add(current.MailId);
                    var mail = _fixture.Mails.Single(m => m.MailId == current.MailId);
                    if (current.slots.GetComponentsInChildren<RewardSlotView>().Length != mail.Rewards.Count)
                        throw new InvalidOperationException("Recycled row kept a previous attachment.");
                }
            }
            Check(pool < 100 && originalIds.SequenceEqual(_root.GetComponentsInChildren<MailRowView>(true).Select(r => r.GetInstanceID()).OrderBy(i => i)), "100 scroll positions reuse the same row objects");
            Check(visited.Count == _fixture.Mails.Count, "every mail rebound with its own attachment count");
            Check(_fixture.ListCalls == requests, "scroll sends no network requests");
            var row = _root.GetComponentsInChildren<MailRowView>().First();
            var nested = row.slots.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            nested.content.anchoredPosition = new Vector2(-120, nested.content.anchoredPosition.y);
            Scroll(1);
            Check(Mathf.Abs(nested.content.anchoredPosition.x) < .01f, "rebound mail resets horizontal attachment position");
            long id = row.MailId;
            Check(_fixture.Mails.Any(m => m.MailId == id), "rebound row uses current long ID");
            row.details.onClick.Invoke();
            var popup = _root.GetComponentsInChildren<RewardPopupView>().Last();
            Check(popup.SourceMailId == id, "details target rebound mail");
            popup.Close();
            row.claim.onClick.Invoke();
            Check(!_fixture.Mails.Any(m => m.MailId == id) && _fixture.Applied == 1, "rebound claim removes correct server mail");
            Check(_view.deleteClaimed != null && !_view.deleteClaimed.interactable, "delete prefab button retained and disabled");
            var result = _root.GetComponentsInChildren<RewardPopupView>().Last();
            Check(result.slots.GetComponentsInChildren<RewardSlotView>().Any(s => s.day.text == "자동 분해"), "actual equipment disposition in reward popup");
            return new { passed = checks.Count, checks, pool, state = Inspect() };
        }

        public static object VerifyCloseReopen()
        {
            Open(1);
            var closing = _view;
            closing.OnClosed?.Invoke();
            bool stopped = !closing.GetComponent<MailboxPanelController>().enabled;
            _fixture.HoldLists = true;

            var host = new GameObject("MailboxReopened", typeof(RectTransform));
            host.SetActive(false);
            var rect = (RectTransform)host.transform;
            rect.SetParent(_root.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var panel = UnityEngine.Object.Instantiate(UIManager.Instance.Catalog.panelMailbox, rect, false);
            panel.GetComponent<MailboxPanelController>().UseTestService(_fixture.Service);
            _view = panel.GetComponent<MailboxPanelView>();
            host.SetActive(true);

            // The closing panel is disabled/destroyed only after its replacement has started loading.
            closing.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(closing.gameObject);
            _fixture.Lists.Dequeue()();
            if (!stopped || !_fixture.Service.CanClaim || !_view.claimAll.interactable)
                throw new InvalidOperationException("MAIL UI ASSERT: closing panel invalidated the reopened mailbox");

            _view.gameObject.SetActive(false);
            _view.gameObject.SetActive(true);
            _fixture.Lists.Dequeue()();
            if (!_fixture.Service.CanClaim) throw new InvalidOperationException("MAIL UI ASSERT: covered panel did not reconnect");
            _view.claimAll.onClick.Invoke();
            _fixture.Lists.Dequeue()();
            if (_fixture.Applied != 1 || _fixture.Service.Messages.Count != 0)
                throw new InvalidOperationException("MAIL UI ASSERT: reopened mailbox could not claim");
            return new { passed = 3, stopped, _fixture.Applied, _fixture.ListCalls };
        }

        public static object VerifyAnchorAndCompatibility()
        {
            Open(100);
            var checks = new List<string>();
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("MAIL UI ASSERT: " + label); checks.Add(label); }
            Scroll(.5f);
            var rows = _root.GetComponentsInChildren<MailRowView>().OrderBy(r => r.MailId).ToArray();
            var anchor = rows[0]; long anchorId = anchor.MailId, nextId = rows[1].MailId;
            float beforeY = _view.content.anchoredPosition.y;
            rows[^1].claim.onClick.Invoke();
            Check(_root.GetComponentsInChildren<MailRowView>().Any(r => r.MailId == anchorId) && Mathf.Abs(_view.content.anchoredPosition.y - beforeY) < .1f,
                "claim below viewport anchor preserves its offset");
            anchor.claim.onClick.Invoke();
            Check(_root.GetComponentsInChildren<MailRowView>().First().MailId == nextId && Mathf.Abs(_view.content.anchoredPosition.y - beforeY) < .1f,
                "deleted anchor continues at adjacent mail");
            foreach (var popup in _root.GetComponentsInChildren<RewardPopupView>()) popup.Close();
            _fixture.FailListAt = _fixture.ListCalls + 1; _fixture.Service.RequestRefresh();
            Check(!_view.content.gameObject.activeSelf && _view.emptyLabel.text.Length > 0 && _view.claimAllLabel.text == "다시 불러오기" && _view.claimAll.interactable,
                "read failure shows retry instead of empty mailbox");
            _view.claimAll.onClick.Invoke();
            Check(_fixture.Service.CanClaim && _view.content.gameObject.activeSelf, "retry button refreshes immediately");
            _view.claimAll.onClick.Invoke();
            Check(_fixture.Service.Messages.Count == 0 && _view.content.anchoredPosition.y == 0 && !_view.claimAll.interactable,
                "claim all clamps empty content and disables receipt");
            foreach (var popup in _root.GetComponentsInChildren<RewardPopupView>()) popup.Close();
            var currency = new KingdomIdle.Rewards.RewardGrant(eCurrency.Gold, 10);
            var gear = KingdomIdle.Rewards.RewardGrant.Equipment(65540, 1);
            RewardPopupView.ShowRewards(_root.transform, new[] { currency, gear }, "출석 보상 획득");
            var common = _root.GetComponentsInChildren<RewardPopupView>().Single();
            var slots = common.slots.GetComponentsInChildren<RewardSlotView>();
            Check(common.title.text == "출석 보상 획득" && slots.Length == 2 && slots.All(slot => slot.icon.enabled), "shared attendance reward popup supports currency and equipment");
            slots[0].Bind(currency, () => { }, 1, true, false);
            Check(slots[0].day.text == "1일차" && slots[0].button.interactable && !slots[0].check.gameObject.activeSelf,
                "attendance day label and claim state remain compatible");
            common.Close();
            bool closed = false;
            var offline = RewardPopupView.ShowOfflineRewards(_root.transform,
                new KingdomIdle.OfflineRewards.OfflineRewardClaimResult("mail-ui-qa", null, 10, 20, 0, 1, 0, 0), false, () => closed = true);
            Check(offline != null && offline.slots.GetComponentsInChildren<RewardSlotView>().Length == 2 && offline.confirmButton.gameObject.activeSelf,
                "offline reward popup retains gold experience and confirm");
            offline.confirmButton.onClick.Invoke(); Check(closed, "offline popup completion callback retained");
            return new { passed = checks.Count, checks };
        }

        public static void Close()
        {
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); _root = null; }
            if (_temporaryEquipment != null) { UnityEngine.Object.DestroyImmediate(_temporaryEquipment.gameObject); _temporaryEquipment = null; }
        }
    }
}
#endif
