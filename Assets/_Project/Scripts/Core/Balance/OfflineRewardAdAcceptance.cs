#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Reflection;
using KingdomIdle.OfflineRewards;
using KingdomIdle.Rewards;
using KingdomIdle.UGUI;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.Balance
{
    public static class OfflineRewardAdAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            if (Application.isPlaying || NetworkManager.Instance != null || UserManager.Instance != null ||
                UIManager.Instance != null || StageManager.Instance != null)
                throw new InvalidOperationException("게임을 중지하고 서버 연결이 없는 Editor에서 실행해 주세요.");

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var managerType = typeof(OfflineRewardManager);
            var popupType = typeof(OfflineRewardPopupController);
            FieldInfo Field(Type type, string name) => type.GetField(name, flags);
            PropertyInfo Property(Type type, string name) => type.GetProperty(name, flags);
            object Call(Type type, object target, string name, params object[] args) => type.GetMethod(name, flags).Invoke(target, args);
            var previousClaim = OfflineRewardManager.LastClaimResult;
            bool previousBusy = OfflineRewardManager.AdBonusBusy;
            var previousView = Field(popupType, "_view").GetValue(null);
            var previousChoice = Field(popupType, "_choice").GetValue(null);
            var previousResult = Field(popupType, "_resultView").GetValue(null);
            var previousHost = UIManager.Instance;
            var previousAds = Field(typeof(ShopAdRewardService), "_server").GetValue(null);
            int previousAdRevision = ShopAdRewardService.Revision;
            var checks = new List<string>();
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("OFFLINE AD ASSERT: " + label);
                checks.Add(label);
            }
            OfflineRewardClaimResult completedResult = null;
            RewardStatus completedStatus = RewardStatus.NotReady;
            void Completed(OfflineRewardClaimResult result, RewardStatus status, string message)
            { completedResult = result; completedStatus = status; }

            using var scope = LocalProgression.BeginTestSession();
            var owner = new GameObject("OfflineRewardAdAcceptance");
            owner.SetActive(false);
            var manager = owner.AddComponent<OfflineRewardManager>();
            // 비활성 부모 아래에서 표시 상태만 검사하며 실제 UI, 광고 SDK, 서버를 실행하지 않는다.
            T Child<T>(string name) where T : Component
            {
                var child = new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(owner.transform, false);
                return typeof(T) == typeof(RectTransform) ? child.GetComponent<T>() : child.AddComponent<T>();
            }
            try
            {
                var view = Child<OfflineRewardPopupView>("Popup");
                view.durationLabel = Child<TextMeshProUGUI>("Duration");
                view.killCountLabel = Child<TextMeshProUGUI>("Kills");
                view.goldRow = Child<RectTransform>("GoldRow");
                view.goldValueLabel = Child<TextMeshProUGUI>("Gold");
                view.ancientCoinRow = Child<RectTransform>("CoinRow");
                view.ancientCoinValueLabel = Child<TextMeshProUGUI>("Coin");
                view.progressLabel = Child<TextMeshProUGUI>("Progress");
                view.adButton = Child<Button>("Ad");
                view.adButtonLabel = Child<TextMeshProUGUI>("AdLabel");
                view.confirmButton = Child<Button>("Confirm");
                var host = owner.AddComponent<UIManager>();
                Property(typeof(UIManager), "Instance").SetValue(null, host);
                var loading = Child<LoadingOverlayView>("Loading");
                loading.lblLoading = Child<TextMeshProUGUI>("LoadingLabel");
                loading.progressBar = Child<Slider>("LoadingProgress");
                loading.gameObject.SetActive(false);
                Field(typeof(UIManager), "_loading").SetValue(host, loading);
                Field(popupType, "_view").SetValue(null, view);
                Field(popupType, "_choice").SetValue(null, null);
                Field(popupType, "_resultView").SetValue(null, null);
                Property(managerType, "AdBonusBusy").SetValue(null, false);
                OfflineRewardManager.AdBonusCompleted += Completed;
                LocalProgression.TestUtcNow = 1793100000;
                LocalProgression.OpenTestAccount("offline-ad-" + Guid.NewGuid().ToString("N"));
                Check(LocalProgression.Execute("offline-ad-fixture", s =>
                {
                    s.Kills = 77;
                    s.Wallet[eCurrency.Gold] = 123;
                    return true;
                }), "격리 계정 준비");

                int finishes = 0;
                Action finished = () => finishes++;
                void Apply(long balance, long gold, long exp = 0, long kills = 10)
                {
                    Property(managerType, "IsBusy").SetValue(manager, true);
                    Call(managerType, manager, "ApplyServerResult", 1m, new OnClaimOfflineRewardResponseDTO
                    {
                        ElapsedSeconds = 600, AppliedSeconds = gold > 0 || exp > 0 ? 600 : 0,
                        KillCount = kills, GainedGold = gold, GainedExp = exp,
                        Level = 1, Exp = exp, Currency = new CurrencyQueryDTO(balance, 0, 0, 0)
                    }, finished);
                }

                Apply(223, 100);
                var first = OfflineRewardManager.LastClaimResult;
                var oldChoice = Field(popupType, "_choice").GetValue(null);
                Check(first != null && OfflineRewardManager.IsLatestClaim(first) &&
                    LocalProgression.Balance(eCurrency.Gold) == 223 && OfflineRewardPopupController.IsOpen,
                    "기본 보상은 확인 버튼 전에 서버 잔액으로 적용됨");
                Check(!loading.gameObject.activeSelf, "미시청 기본 보상 표시에는 지급 로딩을 열지 않음");
                long beforeLoading = LocalProgression.State.Revision;
                Field(oldChoice.GetType(), "Earned").SetValue(oldChoice, true);
                Field(oldChoice.GetType(), "Busy").SetValue(oldChoice, true);
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Check(loading.gameObject.activeSelf && loading.lblLoading.text == "보상 지급 중…",
                    "시청 완료 지급 대기는 기존 로딩 오버레이를 표시함");
                host.SetLoading(true, "테스트 씬 로딩");
                host.SetLoadingProgress(0.7f);
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Call(popupType, null, "CompleteChoice", oldChoice, RewardStatus.SaveFailed, null);
                Check(loading.gameObject.activeSelf && loading.lblLoading.text == "테스트 씬 로딩" &&
                    (bool)Field(oldChoice.GetType(), "Earned").GetValue(oldChoice) &&
                    LocalProgression.State.Revision == beforeLoading && LocalProgression.Balance(eCurrency.Gold) == 223,
                    "지급 오류는 시청 완료와 기존 지급을 보존하고 씬 로딩을 종료하지 않음");
                Property(managerType, "AdBonusBusy").SetValue(null, false);
                Check(loading.gameObject.activeSelf && Mathf.Approximately(loading.progressBar.value, 0.7f),
                    "지급 대기 종료는 진행 중인 씬 로딩과 진행률을 보존함");
                host.SetLoading(false);
                Check(!loading.gameObject.activeSelf, "마지막 로딩이 끝나면 입력 차단을 해제함");
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                host.SetLoading(true, "테스트 씬 로딩");
                host.SetLoading(false);
                Check(loading.gameObject.activeSelf && loading.lblLoading.text == "보상 지급 중…",
                    "씬 로딩 완료는 진행 중인 지급 로딩을 종료하지 않음");
                OfflineRewardPopupController.Hide();
                Check(loading.gameObject.activeSelf, "팝업을 닫아도 실제 지급 대기 동안 로딩을 유지함");
                OfflineRewardPopupController.Show(first);
                Check(loading.gameObject.activeSelf, "팝업 교체는 진행 중인 지급 로딩을 종료하지 않음");
                Property(managerType, "AdBonusBusy").SetValue(null, false);
                oldChoice = Field(popupType, "_choice").GetValue(null);
                Field(oldChoice.GetType(), "Earned").SetValue(oldChoice, true);

                Apply(273, 50);
                var latest = OfflineRewardManager.LastClaimResult;
                var latestChoice = Field(popupType, "_choice").GetValue(null);
                Check(!ReferenceEquals(first, latest) && !ReferenceEquals(oldChoice, latestChoice) &&
                    LocalProgression.Balance(eCurrency.Gold) == 273 && view.goldValueLabel.text == "+50" && finishes == 2,
                    "새 정산은 이전 기본 지급을 보존하고 팝업과 광고 기회만 교체함");
                Check(OfflineRewardManager.IsCurrentAccount(first) && !OfflineRewardManager.IsLatestClaim(first) &&
                    OfflineRewardManager.TryGrantAdBonus(first) == RewardStatus.Stale &&
                    OfflineRewardManager.IsLatestClaim(latest) && OfflineRewardManager.TryGrantAdBonus(latest) == RewardStatus.NotReady,
                    "같은 계정이어도 이전 정산은 거절하고 최신 정산만 연결 준비 검사로 진입함");
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Call(popupType, null, "CompleteChoice", oldChoice, RewardStatus.SaveFailed, null);
                Check(loading.gameObject.activeSelf && ReferenceEquals(Field(popupType, "_choice").GetValue(null), latestChoice),
                    "이전 보상의 늦은 완료는 현재 지급 로딩을 종료하지 않음");
                Property(managerType, "AdBonusBusy").SetValue(null, false);
                long revision = LocalProgression.State.Revision;
                Call(popupType, null, "FinishChoice", oldChoice);
                Call(popupType, null, "CloseResult", oldChoice);
                Check(ReferenceEquals(Field(popupType, "_choice").GetValue(null), latestChoice) &&
                    OfflineRewardPopupController.IsOpen && LocalProgression.State.Revision == revision && !OfflineRewardManager.AdBonusBusy,
                    "교체된 팝업의 광고 완료·결과 닫기 콜백은 현재 보상에 영향을 주지 않음");

                Apply(273, 0);
                Check(ReferenceEquals(OfflineRewardManager.LastClaimResult, latest) &&
                    ReferenceEquals(Field(popupType, "_choice").GetValue(null), latestChoice),
                    "처치 수가 있어도 실제 보상이 0이면 기존 팝업과 광고 기회를 유지함");

                Property(managerType, "IsBusy").SetValue(manager, true);
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Field(managerType, "_pendingAdBonus").SetValue(manager, first);
                Call(managerType, manager, "FinishClaim", finished);
                Check(!manager.IsBusy && !OfflineRewardManager.AdBonusBusy &&
                    Field(managerType, "_pendingAdBonus").GetValue(manager) == null &&
                    ReferenceEquals(completedResult, first) && completedStatus == RewardStatus.Stale && finishes == 4,
                    "새 정산 전에 대기하던 이전 광고는 요청 없이 폐기하고 대기 상태를 해제함");

                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Call(managerType, null, "ApplyAdBonus", first, new OnClaimAdRewardResponseDTO
                {
                    AdType = Protocol.Message.Data.eAdType.OfflineBonus, Level = 1, Exp = 0,
                    Currency = new CurrencyQueryDTO(373, 0, 0, 0),
                    GrantedCurrencies = new List<CurrencyAmountDTO>(), AdRewards = new AdRewardStateDTO()
                });
                Check(LocalProgression.Balance(eCurrency.Gold) == 373 && LocalProgression.State.Kills == 77 &&
                    ReferenceEquals(OfflineRewardManager.LastClaimResult, latest) &&
                    ReferenceEquals(Field(popupType, "_choice").GetValue(null), latestChoice) && completedStatus == RewardStatus.Success &&
                    !loading.gameObject.activeSelf,
                    "이미 지급된 이전 광고 응답은 잔액에 반영하고 최신 팝업은 보존함");

                revision = LocalProgression.State.Revision;
                LocalProgression.TestFailedCommit(() => { Apply(400, 27); return false; });
                Check(OfflineRewardManager.LastClaimResult == null && !OfflineRewardPopupController.IsOpen &&
                    !manager.IsBusy && LocalProgression.Balance(eCurrency.Gold) == 373 && LocalProgression.State.Revision == revision,
                    "새 정산 저장 실패는 기존 광고를 무효화하고 저장 전 잔액을 보존함");

                Apply(373, 0, 1, 0);
                latest = OfflineRewardManager.LastClaimResult;
                Check(latest != null && latest.ExperienceGained == 1 && latest.GoldGained == 0,
                    "골드 없이 경험치만 있는 새 보상도 팝업을 교체함");
                Property(managerType, "IsBusy").SetValue(manager, true);
                Call(managerType, manager, "RejectServerResult", 1m, NetworkManager.CreateError("InvalidRequest", "acceptance"), finished);
                Check(ReferenceEquals(OfflineRewardManager.LastClaimResult, latest) && !manager.IsBusy,
                    "정산하지 않은 확정 거절은 기존 광고 기회를 유지함");
                Property(managerType, "IsBusy").SetValue(manager, true);
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Field(managerType, "_pendingAdBonus").SetValue(manager, latest);
                Call(managerType, manager, "RejectServerResult", 1m, NetworkManager.CreateError("Timeout", "acceptance"), finished);
                Check(OfflineRewardManager.LastClaimResult == null && !OfflineRewardPopupController.IsOpen &&
                    !manager.IsBusy && !OfflineRewardManager.AdBonusBusy && completedStatus == RewardStatus.Stale,
                    "성공 여부가 불명확한 정산은 이전 광고와 대기를 함께 해제함");

                LocalProgression.OpenTestAccount("offline-ad-other-" + Guid.NewGuid().ToString("N"));
                revision = LocalProgression.State.Revision;
                Call(managerType, null, "ApplyAdBonus", latest, new OnClaimAdRewardResponseDTO());
                Check(!OfflineRewardManager.IsCurrentAccount(latest) && completedStatus == RewardStatus.Stale &&
                    LocalProgression.State.Revision == revision, "다른 계정으로 전환한 뒤의 지급 응답은 적용하지 않음");
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Call(managerType, manager, "ResetClaims");
                Check(!loading.gameObject.activeSelf, "계정 정리는 지급 로딩을 남기지 않음");
                Property(managerType, "AdBonusBusy").SetValue(null, true);
                Field(managerType, "_pendingAdBonus").SetValue(manager, latest);
                Call(managerType, manager, "OnDestroy");
                Check(!loading.gameObject.activeSelf && !OfflineRewardManager.AdBonusBusy,
                    "대기 요청 담당의 소멸은 지급 로딩을 해제함");
                return new Dictionary<string, object> { { "passed", checks.Count }, { "checks", checks } };
            }
            finally
            {
                OfflineRewardManager.AdBonusCompleted -= Completed;
                Field(managerType, "_pendingAdBonus").SetValue(manager, null);
                UnityEngine.Object.DestroyImmediate(owner);
                Property(typeof(UIManager), "Instance").SetValue(null, previousHost);
                Field(popupType, "_view").SetValue(null, previousView);
                Field(popupType, "_choice").SetValue(null, previousChoice);
                Field(popupType, "_resultView").SetValue(null, previousResult);
                Field(typeof(ShopAdRewardService), "_server").SetValue(null, previousAds);
                Property(typeof(ShopAdRewardService), "Revision").SetValue(null, previousAdRevision);
                Property(managerType, "LastClaimResult").SetValue(null, previousClaim);
                Property(managerType, "AdBonusBusy").SetValue(null, previousBusy);
            }
        }
    }
}
#endif
