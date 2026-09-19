#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Gacha;
using KingdomIdle.MageTower;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Balance
{
    /// <summary>기획 데이터의 불변조건과 실제 저장 거래를 분리해 검사하는 개발 전용 인수 검사다.</summary>
    public static class QuestAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            using var scope = LocalProgression.BeginTestSession();
            var checks = new List<string>();
            void Check(bool result, string label)
            {
                if (!result) throw new InvalidOperationException("QUEST ASSERT: " + label);
                checks.Add(label);
            }

            QuestCatalog catalog = QuestCatalog.Instance;
            PureChecks(catalog, Check);
            int pureCount = checks.Count;
            // 일요일 KST 23:59:59. 1초 뒤 일일·주간 경계를 동시에 통과한다.
            long sunday = new DateTimeOffset(2026, 9, 20, 14, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds();
            string runId = "quests-" + Guid.NewGuid().ToString("N");
            void Open(string suffix)
            {
                LocalProgression.TestUtcNow = sunday;
                LocalProgression.OpenTestAccount(runId + suffix);
            }
            void Commit(string label, Action<ProgressionState> change) =>
                Check(LocalProgression.Execute(label, state => { change(state); return true; }), label);
            QuestClaimToken Token(long id) => new QuestClaimToken(LocalProgression.AccountGeneration, id,
                QuestEconomy.Period(catalog.Get(id), LocalProgression.State));

            Open("-unlock");
            Commit("해금 전 동일 기간 이벤트를 승인한다", CompleteFreeDaily);
            Check(LocalProgression.State.PendingQuests.Count == 0 &&
                QuestEconomy.Progress(catalog.Get(20001), LocalProgression.State) == 0,
                "미해금 상태는 기록을 모으되 보상 권리를 만들지 않는다");
            Check(QuestEconomy.TryClaim(Token(20001)).Status == QuestClaimStatus.Locked,
                "해금 전 누적이 충분해도 직접 수령 명령은 잠금으로 거절한다");
            Commit("1-11 최초 클리어를 확정한다", state => state.MainClears.Add(0x20001000B));
            Check(QuestEconomy.Progress(catalog.Get(20001), LocalProgression.State) == 200 &&
                QuestEconomy.Progress(catalog.Get(30001), LocalProgression.State) == 200 &&
                QuestEconomy.Progress(catalog.Get(20010), LocalProgression.State) == 5,
                "해금 전 같은 일일·주간 기록을 소급하고 미수령 기본 목표 5개로 완료한다");
            long revision = LocalProgression.State.Revision;
            string bytes = File.ReadAllText(LocalProgression.SnapshotPath);
            string period = LocalProgression.State.QuestDay;
            LocalProgression.TestUtcNow = sunday + 1;
            for (int repeat = 0; repeat < 3; repeat++)
                foreach (eQuestCategory category in Enum.GetValues(typeof(eQuestCategory))) QuestEconomy.GetSnapshot(category);
            Check(LocalProgression.State.Revision == revision && LocalProgression.State.QuestDay == period &&
                File.ReadAllText(LocalProgression.SnapshotPath) == bytes,
                "UI 반복 조회는 자정을 지나도 저장·기간 변경·revision 증가를 하지 않는다");

            Open("-guide");
            Commit("미래 가이드 조건과 업적을 먼저 달성한다", state =>
            {
                state.AttackLevel = 50;
                state.MageSkills[0] = new MageSave { Enhance = 50 };
            });
            Check(!LocalProgression.State.CompletedQuests.Contains(10003) && !LocalProgression.State.CompletedQuests.Contains(10018) &&
                LocalProgression.State.CompletedQuests.Contains(40402) && LocalProgression.State.CompletedQuests.Contains(40802),
                "미래 가이드 현재값은 latch하지 않고 잠긴 업적 단계의 달성은 보존한다");
            Commit("현재값을 낮추고 가이드 첫 두 목표의 근거를 채운다", state =>
            {
                state.AttackLevel = 0;
                state.MageSkills[0].Enhance = 0;
                state.MainClears.Add(0x200010001);
                state.Kills = 20;
            });
            Check(QuestEconomy.Progress(catalog.Get(40802), LocalProgression.State) == 50,
                "마탑 초기화 뒤에도 업적의 달성 및 최고 합계가 유지된다");
            Check(QuestEconomy.Claim(10001) && QuestEconomy.Claim(10002), "선행 가이드를 수령하면 다음 단계가 활성화된다");
            Check(QuestEconomy.ActiveGuide(LocalProgression.State)?.QuestId == 10003 &&
                QuestEconomy.Progress(catalog.Get(10003), LocalProgression.State) == 0,
                "늦게 열린 가이드는 과거 강화 대신 실제 현재값으로 시작한다");
            Commit("활성 가이드 강화 조건을 달성한다", state => state.AttackLevel = 3);
            Commit("수령 전에 활성 가이드의 현재값을 낮춘다", state => state.AttackLevel = 0);
            Check(QuestEconomy.Claim(10003), "활성 단계에서 한번 달성한 가이드의 수령 권리는 유지된다");
            Check(QuestEconomy.TryClaim(Token(40402)).Status == QuestClaimStatus.Locked,
                "업적 후행 달성을 기록해도 선행 수령 전에는 청구하지 못한다");
            Check(QuestEconomy.Claim(40401) && QuestEconomy.Claim(40402),
                "선행 업적 수령 후 저장된 다음 달성을 다시 행동하지 않고 수령한다");

            Open("-period");
            Commit("일일·주간 처치 보상을 달성한다", state =>
            {
                state.MainClears.Add(0x20001000B);
                state.Kills = 6000;
                QuestEconomy.Count(state, eQuestObjectiveType.MonsterKill, 0, 6000);
                QuestEconomy.Count(state, eQuestObjectiveType.BossKill, 0, 14);
            });
            QuestClaimToken oldDaily = Token(20002), oldWeekly = Token(30002), expiring = Token(20001);
            long expiry = LocalProgression.State.PendingQuests[QuestEconomy.Key(expiring.QuestId, expiring.Period)].ExpiresUtc;
            LocalProgression.TestUtcNow = sunday + 1;
            Check(LocalProgression.SynchronizeQuests(), "월요일 자정을 거래로 동기화한다");
            Check(LocalProgression.State.QuestDay == "2026-09-21" && LocalProgression.State.QuestWeek == "2026-09-21" &&
                QuestEconomy.Progress(catalog.Get(20001), LocalProgression.State) == 0 &&
                QuestEconomy.Progress(catalog.Get(30001), LocalProgression.State) == 0 && LocalProgression.State.Kills == 6000,
                "일일·주간 카운터만 새 기간으로 바뀌고 계정 누적은 보존된다");
            Check(QuestEconomy.GetSnapshot(eQuestCategory.Daily).Rows.Any(x => x.Token == oldDaily && x.IsPending),
                "지난 기간의 미수령 행을 새 기간 진행과 별도로 조회한다");
            Check(QuestEconomy.TryClaim(oldDaily).Succeeded && QuestEconomy.TryClaim(oldWeekly).Succeeded,
                "이전 일일·주간 ClaimKey로 각각 한번 수령한다");
            Commit("새 기간의 같은 보스 목표를 달성한다", state => QuestEconomy.Count(state, eQuestObjectiveType.BossKill, 0, 1));
            Check(QuestEconomy.Claim(20002) && LocalProgression.Balance(eCurrency.ClassFragment) == 21,
                "같은 QuestId의 이전·새 기간 수령을 혼합하거나 중복 차단하지 않는다");
            LocalProgression.TestUtcNow = expiry - 1;
            Check(LocalProgression.SynchronizeQuests() &&
                LocalProgression.State.PendingQuests.ContainsKey(QuestEconomy.Key(expiring.QuestId, expiring.Period)),
                "보관 보상은 기간 종료 후 7일 직전까지 남는다");
            LocalProgression.TestUtcNow = expiry;
            Check(LocalProgression.SynchronizeQuests() && QuestEconomy.TryClaim(expiring).Status == QuestClaimStatus.Expired,
                "7일 만료 시각부터 과거 보상을 수령하지 못한다");
            string latestDay = LocalProgression.State.QuestDay;
            LocalProgression.TestUtcNow = sunday;
            Commit("기기 시각을 과거로 되돌린 뒤 거래한다", state => { });
            Check(LocalProgression.State.QuestDay == latestDay, "시각 역행으로 끝난 기간을 다시 열지 않는다");

            Open("-rewards");
            Commit("동적 골드를 낮은 기준 수입으로 달성한다", state =>
            {
                state.MainClears.Add(0x20001000B);
                state.OfflineStage = 0x200010001;
                state.OfflineKpm = 3.25m;
                state.RubyGoldLevel = 1;
                state.GoldRemainder = 250000;
                CompleteFreeDaily(state);
            });
            QuestClaimToken dynamicToken = Token(20001), multiToken = Token(20010);
            Check(!LocalProgression.State.PendingQuests[QuestEconomy.Key(dynamicToken.QuestId, dynamicToken.Period)].HasFixedGold,
                "신규 동적 보상은 달성 시점 골드 금액으로 고정되지 않는다");
            Commit("수령 전에 기준 수입을 올린다", state => { state.OfflineKpm = 4.25m; state.RubyGoldLevel = 2; });
            Check(QuestEconomy.TryClaim(dynamicToken).Succeeded && LocalProgression.Balance(eCurrency.Gold) == 88 &&
                LocalProgression.State.GoldRemainder == 650000,
                "동적 골드는 수령 시점 88.4와 기존 잔여 0.25를 합쳐 88 지급·0.65 보존한다");
            string committed = JsonConvert.SerializeObject(LocalProgression.State);
            Check(!LocalProgression.TestFailedCommit(() => QuestEconomy.TryClaim(multiToken).Succeeded) &&
                JsonConvert.SerializeObject(LocalProgression.State) == committed,
                "실제 수령 저장 실패는 다중 재화·원장·달성·revision을 전부 보존한다");
            Check(QuestEconomy.TryClaim(multiToken).Succeeded && LocalProgression.Balance(eCurrency.AncientCoin) == 80 &&
                LocalProgression.Balance(eCurrency.ArcaneKnowledge) == 20,
                "저장 실패 후 동일 ClaimKey 재시도로 두 재화를 함께 지급한다");
            revision = LocalProgression.State.Revision;
            Check(QuestEconomy.TryClaim(multiToken).Status == QuestClaimStatus.AlreadyClaimed &&
                QuestEconomy.TryClaim(dynamicToken).Status == QuestClaimStatus.AlreadyClaimed && LocalProgression.State.Revision == revision,
                "수령 완료 요청을 반복해도 재지급·소수 잔여·revision 변화가 없다");
            LocalProgression.OpenTestAccount(runId + "-rewards");
            Check(LocalProgression.Balance(eCurrency.AncientCoin) == 80 && LocalProgression.Balance(eCurrency.ArcaneKnowledge) == 20 &&
                LocalProgression.State.Claims.Contains(QuestEconomy.Key(multiToken.QuestId, multiToken.Period)),
                "계정을 다시 열어도 지갑과 수령 원장이 함께 복원된다");
            Check(QuestEconomy.TryClaim(multiToken).Status == QuestClaimStatus.StaleAccount,
                "이전 계정 실행 세대의 화면 토큰은 재접속 뒤 거절된다");

            Open("-legacy");
            // 실제 이전 JSON을 격리된 QA 계정 파일에 넣어 Open의 이관·내구 저장까지 검증한다.
            var legacy = new ProgressionState { QuestDay = "2026-09-20", QuestWeek = "2026-09-14" };
            legacy.MainClears.Add(0x20001000B);
            string legacyKey = QuestEconomy.Key(20001, legacy.QuestDay);
            legacy.PendingQuests.Add(legacyKey, new QuestPending { Id = 20001, ExpiresUtc = sunday + 1 + 7 * 86400L, Gold = 123.75m });
            legacy.Claims.Add(QuestEconomy.Key(10001, "permanent"));
            File.WriteAllText(LocalProgression.SnapshotPath, JsonConvert.SerializeObject(legacy));
            LocalProgression.OpenTestAccount(runId + "-legacy");
            Check(LocalProgression.State.QuestSchemaVersion == QuestEconomy.SchemaVersion &&
                LocalProgression.State.CompletedQuests.Contains(10001) && LocalProgression.State.PendingQuests[legacyKey].HasFixedGold &&
                LocalProgression.State.PendingQuests[legacyKey].Gold == 123.75m,
                "구버전 지급 이력과 달성 당시 확정 Gold를 보존하여 이관한다");
            Check(!QuestEconomy.Migrate(LocalProgression.State), "같은 저장의 퀘스트 이관은 한번만 적용한다");
            Commit("이관 뒤 현재 수입을 크게 변경한다", state => { state.OfflineStage = 0x20003000A; state.OfflineKpm = 30; });
            Check(QuestEconomy.Claim(20001, legacyKey) && LocalProgression.Balance(eCurrency.Gold) == 123 &&
                LocalProgression.State.GoldRemainder == 750000,
                "구버전 보관 Gold는 현재 동적 수입으로 덮어쓰지 않는다");

            GameplayApiChecks(runId, sunday, Check);

            return new Dictionary<string, object>
            {
                { "catalogVersion", catalog.Version }, { "checks", checks }, { "passed", checks.Count },
                { "pureChecks", pureCount }, { "transactionChecks", checks.Count - pureCount }
            };
        }

        /// <summary>
        /// 장면·원본 에셋 없이 실제 뽑기/보관 수령 API를 실행한다. 비활성 fixture는 Awake/Update를 시작하지 않으며,
        /// 동기 호출이 끝나면 운영 싱글턴·난수 상태를 복원하고 임시 객체를 모두 정리한다.
        /// </summary>
        private static void GameplayApiChecks(string runId, long nowUtc, Action<bool, string> check)
        {
            EquipmentManager previousEquipment = EquipmentManager.Instance;
            GachaManager previousGacha = GachaManager.Instance;
            MageTowerManager previousMage = MageTowerManager.Instance;
            StageManager previousStage = StageManager.Instance;
            UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
            var owned = new List<UnityEngine.Object>();
            try
            {
                // 실제 TryPull의 9 Normal/6 Rare/3 Epic 검증을 만족하는 테스트 전용 정의다.
                var database = ScriptableObject.CreateInstance<EquipmentDatabase>();
                owned.Add(database);
                int itemId = 1;
                foreach (var group in new[] { (eEquipmentRarity.Normal, 9), (eEquipmentRarity.Rare, 6), (eEquipmentRarity.Epic, 3) })
                {
                    for (int i = 0; i < group.Item2; i++)
                    {
                        var item = ScriptableObject.CreateInstance<EquipmentData>();
                        owned.Add(item);
                        item.equipmentName = "Quest QA weapon " + itemId;
                        item.rarity = group.Item1;
                        item.slot = eEquipmentSlot.Weapon;
                        SetFixtureField(item, "_itemId", itemId++);
                        database.equipmentList.Add(item);
                    }
                }
                database.Initialize();

                var equipmentObject = new GameObject("QuestAcceptance_Equipment");
                owned.Add(equipmentObject);
                equipmentObject.SetActive(false);
                EquipmentManager equipment = equipmentObject.AddComponent<EquipmentManager>();
                SetFixtureField(equipment, "_database", database);
                var gachaObject = new GameObject("QuestAcceptance_Gacha");
                owned.Add(gachaObject);
                gachaObject.SetActive(false);
                GachaManager gacha = gachaObject.AddComponent<GachaManager>();
                var table = ScriptableObject.CreateInstance<GachaTableSO>();
                owned.Add(table);
                table.gachaType = eGachaType.Equipment;
                table.costCurrency = eCurrency.AncientCoin;
                table.costAmount = 50;
                table.isImplemented = true;

                // 성공 후 RestoreEquipment는 fixture에만 적용한다. 운영 마탑 이벤트와 Stage 재개는 호출하지 않는다.
                EquipmentManager.Instance = equipment;
                StageManager.Instance = null;
                SetFixtureSingleton(typeof(MageTowerManager), null);
                UnityEngine.Random.InitState(7132026);
                LocalProgression.TestUtcNow = nowUtc;
                LocalProgression.OpenTestAccount(runId + "-gacha-api");
                check(LocalProgression.Execute("qa-gacha-api-setup", state =>
                {
                    state.Wallet[eCurrency.AncientCoin] = 1000;
                    state.MainClears.Add(0x20001000B);
                    return true;
                }), "실제 뽑기 API 검사에 주화와 해금 상태를 준비한다");

                List<GachaRewardEntry> results = null;
                string error = null;
                gacha.TryPull(table, 10, value => results = value, message => error = message);
                check(results?.Count == 10 && error == null && LocalProgression.Balance(eCurrency.AncientCoin) == 500,
                    "TryPull 10연차가 실제 결과 10개와 주화 500 차감을 함께 확정한다");
                check(EventTotal(eQuestObjectiveType.GachaUse) == 10 &&
                    QuestProgressEvaluator.Read(LocalProgression.State, "L", eQuestObjectiveType.GachaUse, 1) == 10 &&
                    QuestProgressEvaluator.Read(LocalProgression.State, "D" + LocalProgression.State.QuestDay, eQuestObjectiveType.GachaUse, 0) == 10 &&
                    QuestProgressEvaluator.Read(LocalProgression.State, "W" + LocalProgression.State.QuestWeek, eQuestObjectiveType.GachaUse, 0) == 10,
                    "실제 10연차는 영구·장비 대상·일일·주간에 각각 10회로 기록된다");

                // 비용 차감·결과 생성·Count까지 실행한 뒤 파일 교체만 실패시켜 API 전체의 원자성을 확인한다.
                string beforeFailedPull = JsonConvert.SerializeObject(LocalProgression.State);
                results = null;
                error = null;
                bool failedPullSucceeded = LocalProgression.TestFailedCommit(() =>
                {
                    gacha.TryPull(table, 10, value => results = value, message => error = message);
                    return results != null;
                });
                check(!failedPullSucceeded && results == null && error != null && !gacha.IsPulling &&
                    JsonConvert.SerializeObject(LocalProgression.State) == beforeFailedPull,
                    "TryPull 저장 실패는 결과·주화·천장·퀘스트 집계를 모두 미확정으로 유지한다");
                results = null;
                error = null;
                gacha.TryPull(table, 10, value => results = value, message => error = message);
                check(results?.Count == 10 && error == null && EventTotal(eQuestObjectiveType.GachaUse) == 20 &&
                    LocalProgression.Balance(eCurrency.AncientCoin) == 0,
                    "실패 뒤 실제 10연차 재시도는 성공분 10회만 추가한다");

                // 보유 장비 300개는 fixture 초기 상태다. 그 뒤 실제 Grant로 보관함에 승인한 1개만 획득으로 센다.
                LocalProgression.OpenTestAccount(runId + "-pending-equipment-api");
                int code = database.equipmentList[0].itemCode;
                check(LocalProgression.Execute("qa-pending-api-setup", state =>
                {
                    for (int i = 0; i < EquipmentManager.Capacity; i++)
                        state.Equipment.Add(new EquipmentSave { Id = "fixture-owned-" + i, Code = code });
                    return true;
                }), "보관 수령 API 검사에 가득 찬 가방을 준비한다");
                const string pendingId = "fixture-pending-reward";
                check(LocalProgression.Execute("qa-pending-api-grant", state => EquipmentManager.Grant(state,
                    new EquipmentSave { Id = pendingId, Code = code }, true)) &&
                    LocalProgression.State.PendingEquipment.Count == 1 && EventTotal(eQuestObjectiveType.EquipmentObtain) == 1,
                    "실제 Grant의 보관함 지급은 최초 획득 1회로 기록된다");
                check(LocalProgression.Execute("qa-pending-api-space", state =>
                {
                    state.Equipment.RemoveAt(0);
                    return true;
                }), "보관 장비 수령을 위해 가방 한 칸을 비운다");
                string beforeFailedClaim = JsonConvert.SerializeObject(LocalProgression.State);
                check(!LocalProgression.TestFailedCommit(() => equipment.ClaimPending(pendingId)) &&
                    JsonConvert.SerializeObject(LocalProgression.State) == beforeFailedClaim,
                    "ClaimPending 저장 실패는 보관 위치와 획득 집계를 보존한다");
                check(equipment.ClaimPending(pendingId) && LocalProgression.State.PendingEquipment.Count == 0 &&
                    LocalProgression.State.Equipment.Count == EquipmentManager.Capacity && EventTotal(eQuestObjectiveType.EquipmentObtain) == 1,
                    "실제 ClaimPending은 장비만 이동하고 획득 횟수를 다시 올리지 않는다");
                long revision = LocalProgression.State.Revision;
                check(!equipment.ClaimPending(pendingId) && LocalProgression.State.Revision == revision &&
                    EventTotal(eQuestObjectiveType.EquipmentObtain) == 1,
                    "보관 장비 수령 재요청은 저장이나 획득 집계를 반복하지 않는다");
            }
            finally
            {
                // OnDestroy가 운영 참조를 지우지 못하도록 객체 정리보다 먼저 원래 싱글턴을 복원한다.
                EquipmentManager.Instance = previousEquipment;
                StageManager.Instance = previousStage;
                SetFixtureSingleton(typeof(MageTowerManager), previousMage);
                SetFixtureSingleton(typeof(GachaManager), previousGacha);
                UnityEngine.Random.state = previousRandom;
                for (int i = owned.Count - 1; i >= 0; i--)
                {
                    if (owned[i] == null) continue;
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(owned[i]);
#else
                    UnityEngine.Object.Destroy(owned[i]);
#endif
                }
            }
        }

        /// <summary>검사 입력을 원본 에셋에 저장하지 않고 새 fixture의 직렬화 필드에만 설정한다.</summary>
        private static void SetFixtureField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }

        /// <summary>운영 public API를 넓히지 않고 테스트 범위에서만 private singleton setter를 사용한다.</summary>
        private static void SetFixtureSingleton(Type type, object value)
        {
            MethodInfo setter = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true);
            if (setter == null) throw new MissingMethodException(type.Name, "set_Instance");
            setter.Invoke(null, new[] { value });
        }

        /// <summary>UI나 현재 카테고리에 좌우되지 않는 실제 승인 이벤트 누적값을 읽는다.</summary>
        private static long EventTotal(eQuestObjectiveType type) => QuestProgressEvaluator.Read(LocalProgression.State, "L", type, 0);

        private static void CompleteFreeDaily(ProgressionState state)
        {
            state.Kills += 200;
            QuestEconomy.Count(state, eQuestObjectiveType.MonsterKill, 0, 200);
            QuestEconomy.Count(state, eQuestObjectiveType.BossKill, 0, 1);
            QuestEconomy.Count(state, eQuestObjectiveType.BattleTime, 0, 420);
            QuestEconomy.Count(state, eQuestObjectiveType.SkillCast, 0, 5);
            QuestEconomy.Count(state, eQuestObjectiveType.DungeonEnter, 0, 1);
        }

        private static void PureChecks(QuestCatalog catalog, Action<bool, string> check)
        {
            check(catalog.Definitions.Count == 92 && catalog.Definitions.Count(x => x.Category == eQuestCategory.Guide) == 28 &&
                catalog.Definitions.Count(x => x.Category == eQuestCategory.Daily) == 8 &&
                catalog.Definitions.Count(x => x.Category == eQuestCategory.Weekly) == 7 &&
                catalog.Definitions.Count(x => x.Category == eQuestCategory.Achievement) == 49, "활성 카탈로그 28/8/7/49를 등록한다");
            check(catalog.Get(10026) == null && catalog.Get(10027) == null && catalog.Get(20008) == null &&
                catalog.Get(40104) == null && catalog.RawJson.Contains("DivineEquip"), "폐기·보류 행은 원문에 남고 활성 정의에서 제외된다");
            check(catalog.Get(10025).NextQuestId == 10028 && catalog.GetPredecessor(10028) == 10025 &&
                catalog.GetFamilyRoot(10030) == 10001 && catalog.GetFamilyRoot(40505) == 40501,
                "FamilyId 컬럼 없이 명시 NextQuestId에서 계열과 선행을 얻는다");
            check(catalog.Get(10003).PresentationType == eQuestPresentationType.HighlightButton &&
                (int)eQuestObjectiveType.PlayerLevel == 12 && (int)eQuestObjectiveType.SkillCast == 25,
                "가이드 연출과 기존 목표 enum 번호를 유지한다");
            check(catalog.GetRewards(2001).Single().IsDynamicGold && catalog.GetRewards(2005).Count == 2,
                "동적 0골드 표식과 다중 재화를 typed 보상으로 읽는다");
            check(QuestCatalog.Parse(JObject.Parse(catalog.RawJson).ToString(Formatting.None)).Version == catalog.Version,
                "JSON 공백 변경은 카탈로그 버전을 바꾸지 않는다");

            void Reject(Action<JObject> mutate, string label)
            {
                JObject source = JObject.Parse(catalog.RawJson);
                mutate(source);
                bool rejected = false;
                try { QuestCatalog.Parse(source.ToString()); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, label);
            }
            Reject(source => ((JArray)source["guide"]).Add(source["guide"][0].DeepClone()), "중복 QuestId를 전체 검증에서 거절한다");
            Reject(source => ((JObject)source["guide"][0]).Remove("targetid"), "필수 Stage Target 누락을 0으로 보정하지 않는다");
            Reject(source => source["guide"][0]["type"] = "23", "숫자 문자열을 목표 enum 이름으로 받지 않는다");
            Reject(source => source["guide"][0]["reward"] = 999999, "없는 보상 그룹을 참조한 정의를 거절한다");
            Reject(source => source["guide"][0]["next"] = 40101, "카테고리를 넘는 후행 체인을 거절한다");
            Reject(source => source["guide"][0]["next"] = 10001, "후행 순환 체인을 거절한다");
            Reject(source => source["guide"][0]["next"] = 10003, "한 목표에 선행 두 개가 생기는 체인을 거절한다");
            Reject(source => source["rewards"][0]["amount1"] = 0, "일반 보상의 실제 0원 지급을 거절한다");
            Reject(source => source["guide"][0]["presentation"] = "Unknown", "알 수 없는 Presentation을 거절한다");
            Reject(source =>
            {
                source["quests"][0]["type"] = "EquipmentObtain";
                source["quests"][0]["targetid"] = 2;
            }, "등급별 입력이 없는 장비 획득 누적 목표를 등록하지 않는다");

            long edge = new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            QuestPeriod before = QuestPeriod.At(edge - 1), after = QuestPeriod.At(edge);
            check(before.Day == "2026-09-20" && before.Week == "2026-09-14" && before.DayEndUtc == edge &&
                before.WeekEndUtc == edge && after.Day == "2026-09-21" && after.Week == "2026-09-21",
                "KST 월요일 0시의 두 기간을 같은 UTC로 판정한다");
            var state = new ProgressionState();
            state.Equipment.Add(new EquipmentSave { Id = "normal", Code = 1, Player = 0 });
            state.Equipment.Add(new EquipmentSave { Id = "epic", Code = 3, Player = 1 });
            var rarityGoal = new QuestDefinition { ObjectiveType = eQuestObjectiveType.EquipmentEquip,
                ProgressMode = eQuestProgressMode.CurrentState, TargetId = 2 };
            check(QuestProgressEvaluator.Evaluate(rarityGoal, state, catalog, code => code) == 1,
                "장비 레어도 목표는 해당 등급 이상을 외부 메타데이터로 판정한다");
            string initial = JsonConvert.SerializeObject(state);
            QuestProgressEvaluator.Evaluate(catalog.Get(10011), state, catalog, _ => 1);
            check(JsonConvert.SerializeObject(state) == initial, "순수 목표 조회는 전달한 상태를 변경하지 않는다");

            var corrupt = new ProgressionState { QuestSchemaVersion = QuestEconomy.SchemaVersion, CompletedQuests = null };
            bool migratedCorrupt = QuestEconomy.Migrate(corrupt);
            bool rejectedCorrupt = false;
            try { LocalProgression.Validate(corrupt); }
            catch (InvalidDataException) { rejectedCorrupt = true; }
            check(!migratedCorrupt && corrupt.CompletedQuests == null && rejectedCorrupt,
                "최신 저장의 null 달성 이력은 이관이 빈집합으로 고치지 않고 저장 검증에서 거절한다");
            bool rejectedFuture = false;
            try { QuestEconomy.Migrate(new ProgressionState { QuestSchemaVersion = QuestEconomy.SchemaVersion + 1 }); }
            catch (InvalidDataException) { rejectedFuture = true; }
            check(rejectedFuture, "지원하지 않는 미래 퀘스트 스키마는 현재 버전으로 낮추지 않고 거절한다");
        }
    }
}
#endif
