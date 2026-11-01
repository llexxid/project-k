
// Run in Play Mode on the isolated balance-qa-offline-ad-ui account, with battle time paused.
if (!Application.isPlaying || !KingdomIdle.Balance.LocalProgression.AccountKey.StartsWith("balance-qa-offline-ad-ui-"))
    throw new InvalidOperationException("Use the isolated offline ad UI fixture first.");
var checks = new List<string>();
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); }
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
var controller = typeof(KingdomIdle.UGUI.OfflineRewardPopupController);
object Choice() => controller.GetField("_choice", flags).GetValue(null);
KingdomIdle.OfflineRewards.OfflineRewardClaimResult Snapshot(object choice) => (KingdomIdle.OfflineRewards.OfflineRewardClaimResult)choice.GetType().GetField("Result").GetValue(choice);
void Collect() => controller.GetMethod("Collect", flags).Invoke(null, null);
KingdomIdle.UGUI.RewardPopupView Receipt() => UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.RewardPopupView>();
void NextSettlement()
{
    KingdomIdle.Balance.LocalProgression.TestUtcNow += 600;
    Check(KingdomIdle.Balance.LocalProgression.Execute("ui-next-offline", s => { s.OfflineStage = 0x200010001; s.OfflineKpm = 20; return true; }), "다음 정산 준비");
    Check(KingdomIdle.OfflineRewards.OfflineRewardManager.Claim(), "기본 보상 정산 성공");
}

KingdomIdle.UGUI.OfflineRewardPopupController.Hide();
Check(Receipt() == null, "강제 Hide는 LayerPopups 아래의 기존 결과도 닫음");
NextSettlement();
var first = Choice();
var firstReward = Snapshot(first);
long gold = KingdomIdle.Balance.LocalProgression.Balance(eCurrency.Gold);
first.GetType().GetField("Earned").SetValue(first, true);
KingdomIdle.Balance.LocalProgression.TestFailedCommit(() => { Collect(); return false; });
Check(KingdomIdle.Balance.LocalProgression.Balance(eCurrency.Gold) == gold && Receipt() == null && KingdomIdle.UGUI.OfflineRewardPopupController.IsOpen,
    "광고 완료 후 저장 실패는 추가 지급·완료 팝업 없이 선택창에 남음");
var view = UnityEngine.Object.FindFirstObjectByType<KingdomIdle.UGUI.OfflineRewardPopupView>();
var instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var ad = (UnityEngine.UI.Button)view.GetType().GetField("adButton", instanceFlags).GetValue(view);
var collect = (UnityEngine.UI.Button)view.GetType().GetField("confirmButton", instanceFlags).GetValue(view);
Check(!ad.interactable && collect.interactable, "저장 실패 후 광고는 비활성화하고 보상 수령으로 지급만 재시도");
NextSettlement();
Check(ReferenceEquals(first, Choice()), "새 정산이 들어와도 미지급 광고 완료 상태를 덮어쓰지 않음");
long beforeRetry = KingdomIdle.Balance.LocalProgression.Balance(eCurrency.Gold);
collect.onClick.Invoke();
Check(KingdomIdle.Balance.LocalProgression.Balance(eCurrency.Gold) == beforeRetry + firstReward.GoldGained &&
    Receipt() != null && Receipt().description.text == "보상 2배 획득" && !KingdomIdle.Ads.RewardedAdService.IsBusy,
    "광고 재시청 없이 최초 보너스를 저장하고 2배 결과 표시");
Receipt().confirmButton.onClick.Invoke();
Check(KingdomIdle.UGUI.OfflineRewardPopupController.IsOpen && Snapshot(Choice()).ClaimId != firstReward.ClaimId,
    "앞선 결과를 확인하면 대기하던 새 정산 선택창 표시");
long beforeNormal = KingdomIdle.Balance.LocalProgression.Balance(eCurrency.Gold);
Collect();
Check(KingdomIdle.Balance.LocalProgression.Balance(eCurrency.Gold) == beforeNormal && Receipt() != null && !Receipt().descriptionArea.activeSelf,
    "대기 정산의 일반 수령은 재지급 없이 안내 문구를 숨김");
KingdomIdle.UGUI.OfflineRewardPopupController.Hide();
Check(Receipt() == null && !KingdomIdle.UGUI.OfflineRewardPopupController.IsOpen,
    "강제 정리 시 결과·선택 팝업과 대기 동작이 남지 않음");
KingdomIdle.UGUI.RewardPopupView.ShowRewards(KingdomIdle.UGUI.UIManager.Instance.LayerPopups,
    new[] { new KingdomIdle.Rewards.RewardGrant(eCurrency.Ruby, 5) });
Check(Receipt() != null && Receipt().closeButton.gameObject.activeSelf && !Receipt().confirmButton.gameObject.activeSelf,
    "기존 재화 결과 팝업은 원래 닫기 방식 유지");
Receipt().Close();
var report = new { passed = checks.Count, checks, fixture = KingdomIdle.Balance.LocalProgression.AccountKey };
System.IO.File.WriteAllText("AI/validation/offline-ad-20261028/ui-checks.json",
    Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
return report;
