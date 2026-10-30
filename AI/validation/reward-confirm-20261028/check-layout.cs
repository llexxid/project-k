// Play Mode display-only check; no reward is granted.
var host = KingdomIdle.UGUI.UIManager.Instance;
var result = (KingdomIdle.OfflineRewards.OfflineRewardClaimResult)Activator.CreateInstance(
    typeof(KingdomIdle.OfflineRewards.OfflineRewardClaimResult),
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
    new object[] { "ui-preview", null, 100L, 20L, 0L, 1, 0L, 0L }, null);
var checks = new List<string>();
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); }
foreach (bool doubled in new[] { false, true })
{
    var v = KingdomIdle.UGUI.RewardPopupView.ShowOfflineRewards(host.LayerPopups, result, doubled);
    var rect = (RectTransform)v.confirmButton.transform;
    Check(rect.parent == v.panel && v.panel.Find("OfflineActions") == null, "Original hierarchy: " + doubled);
    var label = v.confirmButton.GetComponentInChildren<TMPro.TMP_Text>();
    label.ForceMeshUpdate();
    Check(Mathf.Abs(rect.rect.width - 120) < .1f && Mathf.Abs(rect.rect.height - 100) < .1f,
        "120 x 100: " + doubled);
    Check(label.gameObject.activeInHierarchy && !label.isTextOverflowing && label.textInfo.characterCount == 2,
        "확인 label visible: " + doubled);
    var parent = (RectTransform)rect.parent;
    Check(Mathf.Abs(parent.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).x - parent.rect.center.x) < .1f,
        "Centered: " + doubled);
    Check(((RectTransform)v.panel.Find("Header")).rect.width > 600 && v.descriptionArea.activeSelf == doubled,
        "Header and description preserved: " + doubled);
    v.confirmButton.onClick.Invoke();
    Check(!v.gameObject.activeSelf, "Confirm closes: " + doubled);
}
KingdomIdle.UGUI.RewardPopupView.ShowRewards(host.LayerPopups,
    new[] { new KingdomIdle.Rewards.RewardGrant(eCurrency.Ruby, 5) });
var common = UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.RewardPopupView>();
void CheckShared(KingdomIdle.UGUI.RewardPopupView view, bool description)
{
    Check(!view.confirmButton.gameObject.activeSelf && view.confirmButton.transform.parent == view.panel && view.closeButton.gameObject.activeSelf,
        "Shared popup uses original close button: " + view.title.text);
    Check(((RectTransform)view.panel.Find("Header")).rect.width > 600 &&
        ((RectTransform)view.rewardScroll.transform).rect.width > 600 && view.title.rectTransform.rect.width > 400,
        "Shared title and rewards retain width: " + view.title.text);
    Check(view.descriptionArea.activeSelf == description && (!description || view.description.rectTransform.rect.width > 400),
        "Shared description area: " + view.title.text);
}
CheckShared(common, false);
common.backdrop.onClick.Invoke();
Check(!common.gameObject.activeSelf, "Shared result backdrop closes");
KingdomIdle.UGUI.RewardPopupView.ShowMail(host.LayerPopups, new KingdomIdle.Mailbox.MailMessage
{
    Id = "ui-layout-probe", Title = "우편 보상 확인", Body = "보상 내용을 확인해 주세요.\n골드와 루비를 받을 수 있습니다.",
    Rewards = new List<KingdomIdle.Rewards.RewardGrant>
    { new(eCurrency.Gold, 12345), new(eCurrency.Ruby, 10) }
});
common = UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.RewardPopupView>();
CheckShared(common, true);
common.slots.GetComponentInChildren<KingdomIdle.UGUI.RewardSlotView>().button.onClick.Invoke();
var detail = common.GetComponentsInChildren<KingdomIdle.UGUI.RewardPopupView>().First(v => v != common);
CheckShared(detail, true);
detail.closeButton.onClick.Invoke();
Check(!detail.gameObject.activeSelf && common.gameObject.activeSelf, "Item X closes only the nested detail");
host.RequestBack();
Check(!common.gameObject.activeSelf, "Mail back closes");
KingdomIdle.UGUI.RewardPopupView.ShowItem(host.LayerPopups, new KingdomIdle.Rewards.RewardGrant(eCurrency.Ruby, 10));
common = UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.RewardPopupView>();
CheckShared(common, true);
common.Close();
var report = new { passed = checks.Count, checks, frame = Time.frameCount };
System.IO.File.WriteAllText("AI/validation/reward-confirm-20261028/checks-flat.json",
    Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
return report;
