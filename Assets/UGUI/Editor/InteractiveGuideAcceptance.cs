using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Direction;
using KingdomIdle.Balance;
using KingdomIdle.Gacha;
using KingdomIdle.KingdomArmy;
using KingdomIdle.MageTower;
using Scripts.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>격리된 PlayMode 검사 안에서 실제 SO·프리팹·경제 API를 사용한다. 운영 계정·빌드·기기에는 접근하지 않는다.</summary>
    public static class InteractiveGuideAcceptance
    {
        private const string Output = "AI/validation/game-direct-reset-20260921";

        /// <summary>기존 빈 씬 검사에서 호출한다. 경제 관리자와 캐릭터 fixture만 생성하고 성공·실패 모두 finally에서 해제한다.</summary>
        public static async UniTask RunAsync(GameObject root, UIManager ui, GameDirectManager manager, Action<bool, string> check)
        {
            if (!Application.isPlaying || !(LocalProgression.AccountKey?.StartsWith("balance-qa-") ?? false)) throw new InvalidOperationException("격리 PlayMode 계정이 필요합니다.");
            var owned = new List<GameObject>();
            var random = UnityEngine.Random.state;
            try
            {
                string account = "interactive-" + Guid.NewGuid().ToString("N");
                LocalProgression.OpenTestAccount(account);
                var equipment = Create<EquipmentManager>(owned);
                Set(equipment, "_database", Asset<EquipmentDatabase>("0cabf66954ae3c2448ff5eba03efac0c"));
                var mage = Create<MageTowerManager>(owned);
                Set(mage, "skillRegistry", Asset<MageTowerSkillRegistrySO>("58fd89c7110460d408086c4e3f3de327"));
                var gacha = Create<GachaManager>(owned);
                var tables = new List<GachaTableSO> {
                    AssetDatabase.LoadAssetAtPath<GachaTableSO>("Assets/Gacha/SO/GachaTable_Equipment.asset"),
                    AssetDatabase.LoadAssetAtPath<GachaTableSO>("Assets/Gacha/SO/GachaTable_MageTowerSkill.asset") };
                Set(gacha, "gachaTables", tables);
                var army = Create<KingdomArmyManager>(owned);
                Set(army, "jobDatabase", Asset<JobDatabase>("9e03a3b2e50f9024c94a0cb0b2ec46e4"));
                Set(army, "equipmentDatabase", Asset<EquipmentDatabase>("0cabf66954ae3c2448ff5eba03efac0c"));
                var user = Create<UserManager>(owned);
                Set(user, "playerPrefab", Asset<GameObject>("86f977addf54f644eb91c4f42d97f642"));
                var enhancement = Create<StatEnhanceManager>(owned);
                user.CreateCharacter();
                foreach (var player in user.GetPlayers()) { owned.Add(player.gameObject); player.enabled = false; }
                LocalProgression.Execute("guide-fixture-wallet", s => { s.Wallet[eCurrency.Gold] = 100000; s.Wallet[eCurrency.AncientCoin] = 0; return true; });
                // 빈 인벤토리로는 긴 콘텐츠 하이라이트 회귀를 검출하지 못한다. 격리 계정에만 실제 장비 48개를 준비한다.
                int code = equipment.GetByRarity(eEquipmentRarity.Normal).First().itemCode;
                LocalProgression.Execute("guide-long-inventory-fixture", s => {
                    for (int i = 0; i < 48; i++) s.Equipment.Add(new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = code });
                    return true;
                });
                equipment.RestoreEquipment();
                var definitions = new[] { "guide_development", "guide_kingdom_army", "guide_dungeon", "guide_gacha", "guide_mage_tower" }
                    .Select(id => AssetDatabase.LoadAssetAtPath<GameDirectSequenceSO>($"{GameDirectPreparation.DataRoot}/{id}.asset")).ToArray();
                foreach (var definition in definitions) check(definition != null && definition.TryValidate(out _), "실습 SO 검증: " + definition?.sequenceId);
                // GameTest의 ID 요청도 운영과 같은 SO 카탈로그를 통과하도록 격리 Manager에만 연결한다.
                Set(manager, "sequences", definitions);
                await ExerciseHiddenGeometry(ui, check);
                check(!manager.RequestPlay(definitions[0], true), "실습형 preview 거절");
                int attackBefore = LocalProgression.State.AttackLevel;
                float timeScale = Time.timeScale;
                foreach (var definition in definitions) check(manager.RequestPlay(definition), "FIFO 실습 요청: " + definition.sequenceId);
                check(!manager.RequestPlay(definitions[3]) && manager.PendingCount == 4, "대기 ID 중복 거절");
                foreach (var definition in definitions)
                {
                    if (definition.sequenceId == "guide_mage_tower")
                        LocalProgression.Execute("guide-empty-mage-fixture", s => {
                            s.MageSkills.Clear(); s.MageSlots = new[] { -1, -1, -1, -1, -1 }; return true;
                        });
                    foreach (var data in definition.steps)
                    {
                        await Until(() => manager.ActiveSequenceId == definition.sequenceId && GameDirectInteraction.Step?.Id == data.id && ui.ActiveFeatureGuide != null && ui.ActiveFeatureGuide.IsVisible, definition.sequenceId + "/" + data.id);
                        await Settle();
                        if (data.id == "attack_once")
                        {
                            ui.SetLoading(true, "실습 로딩 검사"); await Settle();
                            check(!ui.ActiveFeatureGuide.IsVisible && !GameDirectInteraction.Armed, "실습 로딩 중 입력 해제");
                            ui.SetLoading(false);
                            await Until(() => ui.ActiveFeatureGuide.IsVisible, "로딩 복귀");
                            await Settle();
                            ui.GuideTargets.TryGet(GameDirectTarget.KingdomArmy, out var blocked);
                            string blockedHit = Click(blocked);
                            await Settle();
                            check(ui.ActiveTabPanelId == KingdomIdle.UI.UIPanelId.Development && GameDirectInteraction.Step?.Id == "attack_once", "실습 외 하단 메뉴 차단: " + blockedHit);
                            check(enhancement.TryEnhanceEx(StatEnhanceManager.EnhanceType.Attack, 10) == StatEnhanceManager.EnhanceResult.Busy, "강화 10회 직접 호출도 실습 중 거절");
                        }
                        if (data.completion == GuideCompletion.Action && data.grantPracticeCoins)
                            check(LocalProgression.Balance(eCurrency.AncientCoin) == 50, "실습 직전 정확히 50개 지급: " + data.id);
                        if (data.id == "equipment") await CheckEquipmentViewport(root, ui, check);
                        if (definition.sequenceId == "guide_mage_tower" && data.id == "skills")
                        {
                            var slots = (int[])LocalProgression.State.MageSlots.Clone();
                            manager.CancelCurrent();
                            await Until(() => manager.ActiveSequenceId == null, "마탑 중간 종료");
                            MageTowerPopupController.Hide(); await Settle();
                            check(manager.RequestPlay(definition), "마탑 미확인 설명 재개 요청");
                            await Until(() => GameDirectInteraction.Step?.Id == "skills" && ui.ActiveFeatureGuide.IsVisible && MageTowerPopupController.IsOpen, "마탑 팝업 복원");
                            ui.SetLoading(true, "마탑 중단 검사"); await Settle();
                            check(!ui.ActiveFeatureGuide.IsVisible && !GameDirectInteraction.Armed, "마탑 로딩 중 입력 해제");
                            ui.SetLoading(false); await Until(() => ui.ActiveFeatureGuide.IsVisible, "마탑 로딩 복귀"); await Settle();
                            check(slots.SequenceEqual(LocalProgression.State.MageSlots), "마탑 설명 재개는 장착 상태 보존");
                        }
                        if (data.id == "job_stats" || data.id == "attack_once" || data.id == "ruby_info" || data.id == "equipment_once" || definition.sequenceId == "guide_mage_tower")
                            await Capture(root, ui, definition.sequenceId + "-" + data.id, check);
                        if (data.id == "job_stats") CheckScroll(ui, data.target, check);
                        if (data.completion == GuideCompletion.Confirm) Click(ui.ActiveFeatureGuide.nextButton.transform as RectTransform);
                        else
                        {
                            check(ui.GuideTargets.TryGet(data.target, out var target), "실제 대상 등록: " + data.target);
                            string hit = Click(target);
                            check(hit != "InputBlocker", "실제 대상 입력 통과: " + data.target + " -> " + hit);
                            if (data.completion == GuideCompletion.Action)
                            {
                                await Until(() => GameDirectInteraction.HasReceipt(definition.sequenceId, data.id), "경제 성공 영수증: " + data.id);
                                if (data.action != GuideAction.AttackOnce)
                                {
                                    await Until(() => ui.GuideTargets.TryGet(GameDirectTarget.GachaResultClose, out _) && ui.ActiveFeatureGuide.IsVisible, "결과 창 안내");
                                    await Settle();
                                    long coins = LocalProgression.Balance(eCurrency.AncientCoin);
                                    bool extra = false;
                                    gacha.TryPull(tables[data.action == GuideAction.EquipmentPullOnce ? 0 : 1], 1, _ => extra = true, _ => { });
                                    check(!extra && LocalProgression.Balance(eCurrency.AncientCoin) == coins, "결과 확인 전 재뽑기 거절");
                                    ui.GuideTargets.TryGet(GameDirectTarget.GachaResultClose, out var done);
                                    check(Click(done) != "InputBlocker", "실제 결과 확인 버튼 허용");
                                }
                            }
                        }
                    }
                    await Until(() => Read(definition.sequenceId).Completed, "완료 저장: " + definition.sequenceId);
                    check(!manager.RequestPlay(definition), "완료 후 재요청 억제: " + definition.sequenceId);
                }
                await Until(() => manager.ActiveSequenceId == null, "FIFO 종료");
                check(LocalProgression.State.AttackLevel == attackBefore + 1, "공격력 실제 1회만 강화");
                check(LocalProgression.Balance(eCurrency.AncientCoin) == 0 && LocalProgression.State.Claims.Count(x => x.StartsWith("game-direct-grant:")) == 2, "100개 일회 지급과 두 번의 실제 소비");
                check(!ui.HasActiveTabPanel && !MageTowerPopupController.IsOpen && !ui.ActiveFeatureGuide.IsVisible && Time.timeScale == timeScale, "완료 후 메인 복귀·입력 해제·시간 배율 유지");
                check(LocalProgression.State.MageSkills.Count == 0, "미보유 스킬로 마탑 설명 완료 가능");
                await ExerciseGameTestResume(ui, manager, owned, check);
                await ExerciseTestReset(ui, manager, owned, definitions, check);
                await ExerciseMageEquipped(ui, manager, mage, definitions[4], check);
                await ExerciseRecovery(ui, manager, definitions, enhancement, gacha, tables, check);
            }
            finally
            {
                manager.CancelCurrent(); ui.FinishFeatureGuide();
                GameDirectInteraction.End();
                foreach (var go in owned) if (go != null) Object.Destroy(go);
                await Settle();
                UserManager.Instance = null;
                UnityEngine.Random.state = random;
            }
        }

        /// <summary>격리 계정에서 실제 초기화 메뉴·실패 롤백·재지급을 검사한다. 보유 경제 상태와 무관한 안내 기록이 유지되는지도 확인한다.</summary>
        private static async UniTask ExerciseTestReset(UIManager ui, GameDirectManager manager, List<GameObject> owned,
            GameDirectSequenceSO[] definitions, Action<bool, string> check)
        {
            var ids = definitions.Select(x => x.sequenceId).ToArray();
            LocalProgression.Execute("guide-reset-preservation-fixture", s => {
                s.Modules["game-direct:unrelated"] = "keep";
                s.Modules["game-direct-action:guide_gacha_extra:sample"] = "keep";
                s.Claims.Add("game-direct-grant:guide_gacha_extra:sample");
                return true;
            });
            var before = LocalProgression.State.DeepClone();
            var store = new GameDirectProgressStore();
            check(!LocalProgression.TestFailedCommit(() => store.ResetForTesting(ids, LocalProgression.AccountGeneration)), "테스트 초기화 저장 실패 반환");
            check(before.Modules.OrderBy(x => x.Key).SequenceEqual(LocalProgression.State.Modules.OrderBy(x => x.Key)) && before.Claims.SetEquals(LocalProgression.State.Claims), "실패한 초기화는 진행·실습·지급 모두 롤백");
            check(manager.RequestPlay(definitions[0]), "초기화 전 육성 실행");
            await Until(() => ui.ActiveFeatureGuide.IsVisible, "초기화 전 안내 표시");
            var passive = AssetDatabase.LoadAssetAtPath<GameDirectSequenceSO>(GameDirectPreparation.DataRoot + "/first_start_menus.asset");
            check(manager.RequestPlay(passive, true) && manager.PendingCount == 1, "초기화 전 대기열 준비");
            var test = Create<Scripts.Test.GameTest>(owned);
            test.ResetGuideTestRecords();
            await Until(() => manager.ActiveSequenceId == null && ids.All(id => !LocalProgression.State.Modules.ContainsKey("game-direct:" + id)), "실제 GameTest 초기화 메뉴 완료");
            await Settle();
            var after = LocalProgression.State;
            check(manager.PendingCount == 0 && !ui.ActiveFeatureGuide.IsVisible && !GameDirectInteraction.Armed, "초기화는 실행·대기열·입력을 정리");
            check(ids.All(id => !after.Modules.Keys.Any(k => k.StartsWith("game-direct-action:" + id + ":")) && !after.Claims.Any(k => k.StartsWith("game-direct-grant:" + id + ":"))), "선택한 다섯 안내의 실습·지급 이력 초기화");
            check(after.Modules["game-direct:unrelated"] == "keep" && after.Modules["game-direct-action:guide_gacha_extra:sample"] == "keep" && after.Claims.Contains("game-direct-grant:guide_gacha_extra:sample"), "다른 안내와 유사 ID는 보존");
            check(before.Wallet.OrderBy(x => x.Key).SequenceEqual(after.Wallet.OrderBy(x => x.Key)) && before.AttackLevel == after.AttackLevel &&
                Newtonsoft.Json.JsonConvert.SerializeObject(before.Equipment) == Newtonsoft.Json.JsonConvert.SerializeObject(after.Equipment) &&
                Newtonsoft.Json.JsonConvert.SerializeObject(before.MageSkills) == Newtonsoft.Json.JsonConvert.SerializeObject(after.MageSkills) && before.MageSlots.SequenceEqual(after.MageSlots), "초기화는 재화·강화·장비·스킬 보유 내역 보존");
            check(manager.RequestPlay(definitions[3]), "초기화 후 완료했던 뽑기 재요청 접수");
            await Until(() => GameDirectInteraction.Step?.Id == "open" && ui.ActiveFeatureGuide.IsVisible, "뽑기 첫 단계부터 재실행"); await Settle();
            ui.GuideTargets.TryGet(GameDirectTarget.Gacha, out var button); Click(button);
            await Until(() => GameDirectInteraction.Step?.Id == "equipment_once" && ui.ActiveFeatureGuide.IsVisible, "초기화 후 체험 주화 지급");
            check(LocalProgression.Balance(eCurrency.AncientCoin) == before.Wallet[eCurrency.AncientCoin] + 50 && !GameDirectInteraction.Succeeded && GameDirectInteraction.CanPerform(GuideAction.EquipmentPullOnce, 1), "체험 주화 재지급·실제 뽑기 재시도 허용");
            manager.CancelCurrent(); await Until(() => manager.ActiveSequenceId == null, "초기화 검증 종료");
            ui.FinishFeatureGuide(); await Settle();
        }

        /// <summary>격리 계정에 육성만 미완료로 준비하고 실제 GameTest 순차 메뉴를 호출한다. 이전 완료 이력은 유지하며 새 소비 없이 취소한다.</summary>
        private static async UniTask ExerciseGameTestResume(UIManager ui, GameDirectManager manager, List<GameObject> owned, Action<bool, string> check)
        {
            LocalProgression.Execute("guide-resume-menu-fixture", s => {
                s.Modules.Remove("game-direct:guide_development");
                s.Modules.Remove("game-direct-action:guide_development:attack_once");
                return true;
            });
            check(new GameDirectProgressStore().TrySave("guide_development", LocalProgression.AccountGeneration,
                new GameDirectProgress { ConfirmedSteps = new List<string> { "open", "gold" } }), "육성만 미확인 실습 상태 준비");
            var test = Create<Scripts.Test.GameTest>(owned);
            test.TestFirstStartGuide();
            await Until(() => GameDirectInteraction.Step?.Id == "attack_once" && ui.ActiveFeatureGuide.IsVisible, "GameTest 순차 메뉴로 육성 재개");
            check(manager.ActiveSequenceId == "guide_development" && manager.PendingCount == 0 && string.IsNullOrEmpty(manager.LastError), "GameTest는 완료 4개를 정상 생략하고 육성만 재개");
            manager.CancelCurrent(); await Until(() => manager.ActiveSequenceId == null, "GameTest 재개 검증 취소");
            check(!Read("guide_development").Completed && !Read("guide_development").Skipped && !ui.ActiveFeatureGuide.IsVisible, "재개 검증 취소 후 미확인 단계·입력 복구");
            ui.FinishFeatureGuide(); await Settle();
        }

        /// <summary>실제 안내 프리팹으로 화면 밖·마스크 밖 대상의 최초 표시와 복귀를 검사한다. 임시 대상과 View는 성공·실패 모두 정리한다.</summary>
        private static async UniTask ExerciseHiddenGeometry(UIManager ui, Action<bool, string> check)
        {
            var overlay = Object.Instantiate(ui.Catalog.overlayFeatureGuide, ui.LayerOverlays, false);
            var view = overlay.GetComponent<FeatureGuideView>();
            var viewport = new GameObject("GuideGeometryViewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            var target = new GameObject("GuideGeometryTarget", typeof(RectTransform));
            try
            {
                var clip = (RectTransform)viewport.transform; clip.SetParent(ui.LayerScreens, false); clip.sizeDelta = new Vector2(200, 200);
                var rect = (RectTransform)target.transform; rect.SetParent(clip, false); rect.sizeDelta = new Vector2(100, 100);
                var step = new GameDirectStep(new GameDirectStepData { id = "geometry", target = GameDirectTarget.Development, title = "표시 복구 검사", description = "대상이 보일 때 다시 표시합니다." }, 1, 1);
                await Settle();
                foreach (var offset in new[] { new Vector2(100000, 0), new Vector2(300, 0) })
                {
                    rect.anchoredPosition = offset;
                    view.Show(step, rect, () => true);
                    check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "최초 표시에서 화면·마스크 밖 대상은 예외 없이 입력 해제: " + offset.x);
                    rect.anchoredPosition = Vector2.zero;
                    view.Show(step, rect, () => true); await Settle();
                    check(view.IsVisible && view.HighlightRect.width > 0, "대상 복귀 후 같은 View·단계 재표시: " + offset.x);
                    rect.anchoredPosition = offset; await Settle();
                    check(!view.IsVisible && !view.inputGroup.blocksRaycasts, "표시 중 대상 이탈도 입력 해제: " + offset.x);
                }
            }
            finally { view.Hide(); Object.Destroy(overlay); Object.Destroy(viewport); await Settle(); }
        }

        /// <summary>격리 계정의 실제 장착 스킬로 설명 중 해제 입력 차단과 뒤로가기 정리를 검사한다. 운영 소유 상태는 접근하지 않는다.</summary>
        private static async UniTask ExerciseMageEquipped(UIManager ui, GameDirectManager manager, MageTowerManager mage,
            GameDirectSequenceSO definition, Action<bool, string> check)
        {
            LocalProgression.OpenTestAccount("interactive-mage-equipped-" + Guid.NewGuid().ToString("N"));
            int id = mage.GetAllSkills().First().id;
            mage.Unlock(id); check(mage.Equip(0, id), "마탑 보유·장착 fixture 저장");
            long coins = LocalProgression.Balance(eCurrency.AncientCoin);
            check(manager.RequestPlay(definition), "장착 계정 마탑 요청");
            foreach (var data in definition.steps)
            {
                await Until(() => GameDirectInteraction.Step?.Id == data.id && ui.ActiveFeatureGuide.IsVisible, "장착 마탑 " + data.id); await Settle();
                if (data.completion == GuideCompletion.Click) { ui.GuideTargets.TryGet(data.target, out var target); Click(target); }
                else if (data.id == "unequip")
                {
                    ui.GuideTargets.TryGet(GameDirectTarget.MageUnequipAction, out var button);
                    check(button.GetComponent<UnityEngine.UI.Button>().interactable, "실제 해제 버튼 활성 fixture");
                    check(Click(button) == "InputBlocker" && mage.GetEquippedSkillId(0) == id, "설명 중 실제 해제 조작 차단");
                    check(ui.ActiveFeatureGuide.HandleBack(), "마탑 뒤로가기 처리");
                }
                else Click(ui.ActiveFeatureGuide.nextButton.transform as RectTransform);
            }
            await Until(() => manager.ActiveSequenceId == null, "마탑 뒤로가기 종료");
            check(Read(definition.sequenceId).Skipped && !MageTowerPopupController.IsOpen && !ui.ActiveFeatureGuide.IsVisible, "마탑 건너뛰기 기록·팝업·입력 정리");
            check(mage.GetEquippedSkillId(0) == id && mage.IsOwned(id) && coins == LocalProgression.Balance(eCurrency.AncientCoin), "마탑 설명은 소유·장착·재화 보존");
        }

        /// <summary>재진입·지급 실패·소비 실패·계정 세대의 경계를 실제 저장 API로 검사한다. 테스트 계정만 변경한다.</summary>
        private static async UniTask ExerciseRecovery(UIManager ui, GameDirectManager manager, GameDirectSequenceSO[] definitions,
            StatEnhanceManager enhancement, GachaManager gacha, List<GachaTableSO> tables, Action<bool, string> check)
        {
            LocalProgression.OpenTestAccount("interactive-recovery-" + Guid.NewGuid().ToString("N"));
            var definition = definitions[3];
            var data = definition.steps[1];
            var step = new GameDirectStep(data, 2, definition.steps.Length);
            long generation = LocalProgression.AccountGeneration;
            check(!LocalProgression.TestFailedCommit(() => GameDirectInteraction.GrantCoins(definition.sequenceId, step, generation)), "지급 저장 실패 반환");
            check(LocalProgression.Balance(eCurrency.AncientCoin) == 0 && LocalProgression.State.Claims.All(x => !x.StartsWith("game-direct-grant:")), "실패한 지급은 주화·청구 모두 미확정");
            check(GameDirectInteraction.GrantCoins(definition.sequenceId, step, generation) && GameDirectInteraction.GrantCoins(definition.sequenceId, step, generation) && LocalProgression.Balance(eCurrency.AncientCoin) == 50, "지급 재호출은 정확히 한 번");
            GameDirectInteraction.Begin(definition.sequenceId, step, generation, () => true); GameDirectInteraction.Arm();
            bool pulled = false;
            LocalProgression.TestFailedCommit(() => { gacha.TryPull(tables[0], 1, _ => pulled = true, _ => { }); return pulled; });
            check(!pulled && LocalProgression.Balance(eCurrency.AncientCoin) == 50 && !GameDirectInteraction.Succeeded, "소비 저장 실패는 보상·영수증까지 롤백");
            GameDirectInteraction.Begin(definition.sequenceId, step, generation, () => true); GameDirectInteraction.Arm();
            gacha.TryPull(tables[0], 1, _ => pulled = true, _ => { });
            check(pulled && GameDirectInteraction.Succeeded && LocalProgression.Balance(eCurrency.AncientCoin) == 0, "소비와 성공 영수증 동시 확정");
            GameDirectInteraction.End();
            var progress = new GameDirectProgress { ConfirmedSteps = new List<string> { "open" } };
            check(new GameDirectProgressStore().TrySave(definition.sequenceId, generation, progress), "성공 후 단계 확인 전 종료 상태 준비");
            check(manager.RequestPlay(definition), "성공 후 재개 요청");
            await Until(() => GameDirectInteraction.Step?.Id == "equipment_done" && ui.ActiveFeatureGuide.IsVisible, "소비 영수증으로 재뽑기 생략");
            check(LocalProgression.Balance(eCurrency.AncientCoin) == 0, "재개 시 중복 지급·소비 없음");
            manager.CancelCurrent(); await Until(() => manager.ActiveSequenceId == null, "재개 취소"); ui.FinishFeatureGuide(); await Settle();
            LocalProgression.Execute("guide-max-fixture", s => { s.AttackLevel = BalanceMath.GoldCap; return true; });
            check(manager.RequestPlay(definitions[0]), "최대 강화 안내 요청");
            foreach (var item in definitions[0].steps)
            {
                await Until(() => GameDirectInteraction.Step?.Id == item.id && ui.ActiveFeatureGuide.IsVisible, "최대 강화 단계 " + item.id);
                await Settle();
                if (item.completion == GuideCompletion.Click) { ui.GuideTargets.TryGet(item.target, out var target); Click(target); }
                else Click(ui.ActiveFeatureGuide.nextButton.transform as RectTransform);
            }
            await Until(() => manager.ActiveSequenceId == null, "최대 강화 완료");
            check(Read(definitions[0].sequenceId).Completed, "최대 강화는 추가 소비 없이 확인으로 완료");

            LocalProgression.OpenTestAccount("interactive-poor-" + Guid.NewGuid().ToString("N"));
            LocalProgression.Execute("guide-poor-fixture", s => { s.Wallet[eCurrency.Gold] = 0; return true; });
            check(manager.RequestPlay(definitions[0]), "골드 부족 실습 요청");
            await Until(() => GameDirectInteraction.Step?.Id == "open" && ui.ActiveFeatureGuide.IsVisible, "부족 계정 메뉴"); await Settle();
            ui.GuideTargets.TryGet(GameDirectTarget.Development, out var development); Click(development);
            await Until(() => GameDirectInteraction.Step?.Id == "gold" && ui.ActiveFeatureGuide.IsVisible, "골드 안내"); await Settle();
            Click(ui.ActiveFeatureGuide.nextButton.transform as RectTransform);
            await Until(() => GameDirectInteraction.Step?.Id == "attack_once" && ui.ActiveFeatureGuide.IsVisible, "부족 계정 실습"); await Settle();
            check(enhancement.TryEnhanceEx(StatEnhanceManager.EnhanceType.Attack) == StatEnhanceManager.EnhanceResult.NotEnoughGold && !GameDirectInteraction.Succeeded, "골드 부족은 성공으로 인정하지 않음");
            Click(ui.ActiveFeatureGuide.nextButton.transform as RectTransform);
            await Until(() => manager.ActiveSequenceId == null, "나중에 계속 종료");
            check(!Read(definitions[0].sequenceId).Completed && !Read(definitions[0].sequenceId).Skipped && !ui.ActiveFeatureGuide.IsVisible && LocalProgression.Balance(eCurrency.Gold) == 0, "나중에 계속은 추가 지급·완료·건너뛰기 없이 입력 해제");
            LocalProgression.Execute("guide-fund-fixture", s => { s.Wallet[eCurrency.Gold] = 1000; return true; });
            check(manager.RequestPlay(definitions[0]), "미확인 강화 단계 재개");
            await Until(() => GameDirectInteraction.Step?.Id == "attack_once" && ui.ActiveFeatureGuide.IsVisible, "실습 재개"); await Settle();
            var pathField = typeof(LocalProgression).GetField("_path", BindingFlags.NonPublic | BindingFlags.Static);
            string path = LocalProgression.SnapshotPath;
            try
            {
                pathField.SetValue(null, path + "/fail-write.json");
                ui.GuideTargets.TryGet(GameDirectTarget.AttackOnce, out var attack); Click(attack);
                await Until(() => manager.ActiveSequenceId == null, "실제 강화 저장 실패 정리");
                check(!ui.ActiveFeatureGuide.IsVisible && !GameDirectInteraction.Armed && LocalProgression.State.AttackLevel == 0, "실제 저장 실패 후 입력·강화·단계 모두 복구");
            }
            finally { pathField.SetValue(null, path); }
            check(manager.RequestPlay(definitions[0]) && manager.RequestPlay(definitions[1]), "계정 전환 전 실행·대기 요청");
            await Until(() => ui.ActiveFeatureGuide.IsVisible, "계정 전환 전 표시");
            long epoch = GameDirectInteraction.Epoch;
            LocalProgression.OpenTestAccount("interactive-next-" + Guid.NewGuid().ToString("N"));
            await Until(() => manager.ActiveSequenceId == null, "계정 전환 취소");
            check(manager.PendingCount == 0 && !ui.ActiveFeatureGuide.IsVisible && GameDirectInteraction.Epoch != epoch && !Read(definitions[0].sequenceId).Completed, "계정 전환은 대기열·표시·늦은 실행 세대를 함께 폐기");
        }

        /// <summary>긴 장비 목록의 상단·중간·하단에서 강조 범위와 스크롤을 검사한다. 시험용 위치만 바꾸며 계정에는 쓰지 않는다.</summary>
        private static async UniTask CheckEquipmentViewport(GameObject root, UIManager ui, Action<bool, string> check)
        {
            ui.GuideTargets.TryGet(GameDirectTarget.ArmyEquipment, out var target);
            var scroll = target.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            check(target == scroll.viewport && scroll.content.rect.height > scroll.viewport.rect.height * 2, "긴 장비 목록 대신 실제 뷰포트 등록");
            foreach (float position in new[] { 1f, .5f, 0f })
            {
                scroll.StopMovement(); scroll.verticalNormalizedPosition = position; await Settle();
                AssertViewport(ui, check);
                await Capture(root, ui, "equipment-scroll-" + position.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), check);
            }
            CheckScroll(ui, GameDirectTarget.ArmyEquipment, check);
        }

        /// <summary>현재 화면 비율에서 강조 테두리가 뷰포트를 벗어나지 않는지 확인한다. 계층이나 레이아웃은 수정하지 않는다.</summary>
        private static void AssertViewport(UIManager ui, Action<bool, string> check)
        {
            ui.GuideTargets.TryGet(GameDirectTarget.ArmyEquipment, out var target);
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var view = ui.ActiveFeatureGuide;
            var low = view.transform.InverseTransformPoint(corners[0]); var high = view.transform.InverseTransformPoint(corners[2]);
            var hole = view.HighlightRect;
            check(hole.width > 0 && hole.height > 0 && hole.xMin >= low.x - 1 && hole.yMin >= low.y - 1 && hole.xMax <= high.x + 1 && hole.yMax <= high.y + 1,
                "장비 강조는 현재 뷰포트 안에만 표시");
            check(view.highlight.localScale == Vector3.one, "뷰포트 테두리 점멸 확대 없음");
        }

        /// <summary>실제 안내·패널을 세 비율로 렌더한다. 기기 측정과 구별하고 카드·문자 잘림 및 UI 메모리를 기록한다.</summary>
        private static async UniTask Capture(GameObject root, UIManager ui, string name, Action<bool, string> check)
        {
            var canvas = root.GetComponent<Canvas>(); var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            var cameraGo = new GameObject("GuideRatioCamera", typeof(Camera)); var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true; cameraGo.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .12f, .08f);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                foreach (var size in new[] { new Vector2Int(360, 800), new Vector2Int(540, 960), new Vector2Int(900, 1200) })
                {
                    var rt = new RenderTexture(size.x, size.y, 24); var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var previous = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = rt; await Settle(); camera.Render(); RenderTexture.active = rt;
                        if (GameDirectInteraction.Step?.Target == GameDirectTarget.ArmyEquipment) AssertViewport(ui, check);
                        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                        Directory.CreateDirectory(Output); File.WriteAllBytes($"{Output}/{name}-{size.x}x{size.y}.png", image.EncodeToPNG());
                        check(!ui.ActiveFeatureGuide.descriptionLabel.isTextOverflowing, "실습 본문 잘림 없음: " + name + size);
                    }
                    finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(image); }
                }
                File.AppendAllText(Output + "/interactive-render.txt", $"{name}: batches={UnityStats.batches}, drawCalls={UnityStats.drawCalls}, allocated={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()} (Editor simulated ratios)\n");
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = oldCamera; Object.Destroy(cameraGo); await Settle(); }
        }

        /// <summary>계정 진행을 읽어 확인한다. 손상되거나 다른 계정이면 실패시켜 검증을 중단한다.</summary>
        private static GameDirectProgress Read(string id) => new GameDirectProgressStore().TryLoad(id, LocalProgression.AccountGeneration, out var p) ? p : throw new InvalidOperationException(id);
        /// <summary>검증 대상 GUID의 기존 에셋을 읽는다. 복제·저장·임포트 설정 변경은 하지 않는다.</summary>
        private static T Asset<T>(string guid) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        /// <summary>fixture 관리자 생성과 정리 소유권을 기록한다. 운영 singleton이 없는 빈 검사 씬에서만 사용한다.</summary>
        private static T Create<T>(List<GameObject> owned) where T : Component { var go = new GameObject("GuideFixture_" + typeof(T).Name); owned.Add(go); return go.AddComponent<T>(); }
        /// <summary>fixture 직렬화 참조를 실제 카탈로그에 연결한다. 운영 오브젝트에는 사용하지 않는다.</summary>
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        /// <summary>한 프레임 이상의 레이아웃·지연 파괴를 기다린다. 게임 시간 배율을 변경하지 않는다.</summary>
        private static async UniTask Settle() { await UniTask.NextFrame(); await UniTask.NextFrame(); Canvas.ForceUpdateCanvases(); }
        /// <summary>실제 게임 루프에서 조건을 기다리며 무한 대기 대신 현재 단계와 함께 오류를 기록한다.</summary>
        private static async UniTask Until(Func<bool> predicate, string label)
        { float end = Time.realtimeSinceStartup + 12; while (!predicate()) { if (Time.realtimeSinceStartup > end) throw new TimeoutException(label + " current=" + GameDirectInteraction.Step?.Id); await UniTask.NextFrame(); } }
        /// <summary>EventSystem 최상단 히트에 실제 클릭을 전달한다. 원본 버튼 이벤트를 직접 호출해서 입력 차단 검사를 우회하지 않는다.</summary>
        private static string Click(RectTransform target)
        {
            if (target == null) throw new InvalidOperationException("클릭 대상 없음");
            var canvas = target.GetComponentInParent<Canvas>();
            var point = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, target.TransformPoint(target.rect.center));
            if (GameDirectInteraction.Step?.Target == GameDirectTarget.MageTower)
            {
                var region = FeatureGuideAnchor.FocusRegion(target, GameDirectTarget.MageTower);
                point = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null,
                    target.TransformPoint(target.rect.min + Vector2.Scale(target.rect.size, region.center)));
            }
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0) throw new InvalidOperationException("레이캐스트 없음: " + target.name);
            var hit = hits[0].gameObject;
            // 실제 마탑은 짧은 누름/뗌을 UILongPressButton으로 받는다. 같은 포인터 경로로 열기 동작을 검사한다.
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
            if (hit.name == "InputBlocker" && GameDirectInteraction.Step?.Target == GameDirectTarget.MageTower)
                throw new InvalidOperationException("마탑 입력 가림: " + string.Join(",", hits.Select(x => x.gameObject.name)) + " point=" + point + " target=" + target.name);
            return hit.name;
        }

        /// <summary>실제 레이캐스트로 통과하는 읽기 영역에서 드래그한다. 버튼 호출이나 스크롤 위치 직접 대입으로 입력 필터를 우회하지 않는다.</summary>
        private static void CheckScroll(UIManager ui, GameDirectTarget targetId, Action<bool, string> check)
        {
            ui.GuideTargets.TryGet(targetId, out var target);
            var scroll = target.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            check(scroll != null, "전직 정보 읽기용 ScrollRect 연결");
            var camera = scroll.GetComponentInParent<Canvas>().worldCamera;
            var viewport = scroll.viewport;
            var hits = new List<RaycastResult>();
            for (int y = 1; y < 10; y++)
                for (int x = 1; x < 10; x++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(new Vector2(
                        Mathf.Lerp(viewport.rect.xMin, viewport.rect.xMax, x / 10f), Mathf.Lerp(viewport.rect.yMin, viewport.rect.yMax, y / 10f))));
                    var data = new PointerEventData(EventSystem.current) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left };
                    hits.Clear(); EventSystem.current.RaycastAll(data, hits);
                    if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject) != scroll.gameObject) continue;
                    float before = scroll.content.anchoredPosition.y;
                    data.pointerPressRaycast = hits[0];
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.beginDragHandler);
                    data.position += new Vector2(0, scroll.verticalNormalizedPosition > .5f ? 45 : -45);
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.dragHandler);
                    ExecuteEvents.Execute(scroll.gameObject, data, ExecuteEvents.endDragHandler);
                    check(Mathf.Abs(scroll.content.anchoredPosition.y - before) > 1, "안내 중 실제 드래그로 정보 스크롤 가능");
                    return;
                }
            check(false, "입력 필터를 통과하는 읽기 스크롤 영역 없음");
        }
    }
}
