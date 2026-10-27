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
Check(!common.confirmButton.gameObject.activeSelf && !common.confirmButton.transform.parent.gameObject.activeSelf && common.closeButton.gameObject.activeSelf,
    "Shared reward popup keeps its original close button and has no offline footer");
common.Close();
var report = new { passed = checks.Count, checks, frame = Time.frameCount };
System.IO.File.WriteAllText("AI/validation/reward-confirm-20261028/checks.json",
    Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
return report;
