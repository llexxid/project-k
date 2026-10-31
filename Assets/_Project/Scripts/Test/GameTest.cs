using Direction;
using KingdomIdle.Balance;
#if UNITY_EDITOR
using KingdomIdle.CheckIn;
using KingdomIdle.Mailbox;
using KingdomIdle.Rewards;
using KingdomIdle.UGUI;
using UnityEditor;
#endif
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Test
{
    public class GameTest : MonoBehaviour
    {
    #if UNITY_EDITOR
        private static readonly string[] GuideTestIds = {
            "guide_development", "guide_kingdom_army", "guide_dungeon", "guide_gacha", "guide_mage_tower"
        };

        [MenuItem("GameTest/Stage Test/Clear Stage")]
        public static void TestClearStage()
        {
            var manager = StageManager.Instance;
            if (manager == null) return;

            if (!manager.TestClearStage())
                Debug.LogWarning("[GameTest] 현재 스테이지를 클리어할 수 없습니다.");
        }

        /// <summary>Play 중 현재 계정의 골드·루비 던전 티켓을 일일 기본 수량으로 복원하고 오늘 기준으로 저장한다.</summary>
        [MenuItem("GameTest/Dungeon Test/입장 티켓 충전")]
        public static void RefillDungeonTickets()
        {
            if (!Application.isPlaying || !LocalProgression.IsReady)
            {
                Debug.LogWarning("[GameTest] bootstrap부터 Play 후 계정이 준비되면 티켓을 충전하세요.");
                return;
            }

            int dailyTickets = 0;
            bool saved = LocalProgression.Execute("test-refill-dungeon-tickets", state =>
            {
                dailyTickets = StageCatalogRules.Database.DailyTickets;
                state.GoldTickets = state.RubyTickets = dailyTickets;
                state.TicketDay = LocalProgression.KstDay;
                return true;
            });
            if (!saved)
            {
                Debug.LogWarning("[GameTest] 던전 티켓 충전 실패: " +
                    (LocalProgression.LastError ?? "저장을 처리할 수 없습니다. 잠시 후 다시 시도하세요."));
                return;
            }

            Debug.Log($"[GameTest] 골드·루비 던전 티켓을 각각 {dailyTickets}장으로 충전하고 현재 계정에 저장했습니다.");
        }

        [MenuItem("GameTest/CheckIn Test/서버 연동 준비 중")]
        public static void CheckInPending() => Debug.Log("서버 출석 개편 전까지 로컬 출석 지급을 중지합니다.");

        [MenuItem("GameTest/Shop Ad Test/오늘 광고 수령 초기화 (재지급 허용)")]
        public static void ResetShopAds()
        {
            if (!Application.isPlaying || !LocalProgression.IsReady)
            { Debug.LogWarning("[GameTest] bootstrap부터 Play 후 로그인하여 상점 광고를 초기화하세요."); return; }
            if (ShopPopupController.HasPendingAdForTesting)
            { Debug.LogWarning("[GameTest] 광고 진행 또는 완료 보상 저장이 대기 중입니다. 먼저 광고·보상 수령을 마치세요."); return; }

            var status = ShopAdRewardService.ResetTodayForTesting();
            if (status != RewardStatus.Success)
            { Debug.LogWarning($"[GameTest] 상점 광고 초기화 실패: {status} {LocalProgression.LastError}"); return; }
            Debug.Log("[GameTest] 오늘의 장비·마탑 광고 수령 기록을 초기화하고 저장했습니다. 보유 뽑기권은 유지되며, 광고를 다시 완료하면 뽑기권이 추가 지급됩니다.");
        }

        [MenuItem("GameTest/Mailbox Test/격리 로컬 지급 검증")]
        public static void TestLocalMailbox() => Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(KingdomIdle.Mailbox.Testing.MailboxAcceptance.RunLocal()));

        [MenuItem("GameTest/Mailbox Test/서버 응답 시나리오 검증")]
        public static void TestServerMailbox() => Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(KingdomIdle.Mailbox.Testing.MailboxAcceptance.Run()));

        [MenuItem("GameTest/Mailbox Test/서버 응답 실제 저장 검증")]
        public static void TestServerMailboxApply() => Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(KingdomIdle.Mailbox.Testing.MailboxAcceptance.RunServerApply()));

        /// <summary>현재 계정에서 육성 실습을 요청한다. 실제 골드를 소비하고 강화·완료를 저장하며 정리는 Manager에 맡긴다.</summary>
        [MenuItem("GameTest/Guide Test/실제 계정 저장/1 육성")]
        public static void TestDevelopmentGuide() => RequestGuide("guide_development", false);

        /// <summary>현재 계정에서 왕국군 정보 열람 실습을 요청한다. 장착·전직 없이 안내 진행을 저장한다.</summary>
        [MenuItem("GameTest/Guide Test/실제 계정 저장/2 왕국군")]
        public static void TestArmyGuide() => RequestGuide("guide_kingdom_army", false);

        /// <summary>현재 계정에서 던전 정보 안내를 요청한다. 실제 입장 없이 확인한 단계만 저장한다.</summary>
        [MenuItem("GameTest/Guide Test/실제 계정 저장/3 던전")]
        public static void TestDungeonGuide() => RequestGuide("guide_dungeon", false);

        /// <summary>현재 계정에서 뽑기 실습을 요청한다. 50개씩 일회 지급·실제 소비·획득·완료가 모두 저장된다.</summary>
        [MenuItem("GameTest/Guide Test/실제 계정 저장/4 뽑기")]
        public static void TestGachaGuide() => RequestGuide("guide_gacha", false);

        /// <summary>현재 계정에서 마탑 창을 열고 장착·해제를 설명한다. 실제 장착을 바꾸지 않고 안내 확인만 저장한다.</summary>
        [MenuItem("GameTest/Guide Test/실제 계정 저장/5 마탑 스킬")]
        public static void TestMageTowerGuide() => RequestGuide("guide_mage_tower", false);

        /// <summary>독립 SO 다섯 개를 FIFO로 요청한다. 이미 완료한 안내는 거절되고 나머지만 순서대로 실행된다.</summary>
        [MenuItem("GameTest/Guide Test/실제 계정 저장/다섯 안내 순차 실행")]
        public static void TestFirstStartGuide()
        { TestDevelopmentGuide(); TestArmyGuide(); TestDungeonGuide(); TestGachaGuide(); TestMageTowerGuide(); }

        /// <summary>
        /// 상단 GameTest 메뉴에서 다섯 안내의 테스트 기록만 초기화한다. 실행·대기열 정리는 Manager가 맡고 성공한 저장 뒤에만 완료 로그를 남긴다.
        /// 현재 보유 내역은 유지하지만 실습·지급 이력도 지우므로 이후 다시 강화·뽑기를 수행하고 체험 주화를 받을 수 있다.
        /// </summary>
        [MenuItem("GameTest/Guide Test/실제 계정 저장/다섯 안내 테스트 기록 초기화 (재지급 허용)")]
        public static async void ResetGuideTestRecords()
        {
            var direct = GameDirectManager.Instance;
            if (!Application.isPlaying || direct == null)
            { Debug.LogWarning("[GameTest] bootstrap부터 Play 후 계정이 준비되면 초기화하세요."); return; }
            bool reset = await direct.ResetForTestingAsync(generation => new GameDirectProgressStore().ResetForTesting(GuideTestIds, generation));
            if (!reset) { Debug.LogWarning("[GameTest] 안내 초기화 실패: " + direct.LastError); return; }
            Debug.Log("[GameTest] 다섯 안내의 진행·실습 성공·주화 지급 기록을 초기화했습니다. 보유 내역은 유지됩니다. 개별 또는 순차 실행으로 다시 테스트하세요.");
        }

        /// <summary>현재 안내를 미완료로 중단한다. 확인한 단계와 이미 지급·소비한 재화는 보존한다.</summary>
        [MenuItem("GameTest/Guide Test/현재 안내 나중에 계속")]
        public static void DeferGuide() => GameDirectManager.Instance?.CancelCurrent();

        /// <summary>PlayMode의 상단 GameTest 메뉴에서 환생 버튼 안내를 요청한다. 실제 환생은 실행하지 않고 안내만 미리본다.</summary>
        [MenuItem("GameTest/Guide Test/환생 안내")]
        public static void TestReincarnationGuide() => RequestGuide("first_reincarnation_guide", true);

        /// <summary>
        /// 실습·미리보기 메뉴의 공통 요청 창구다. 연출 ID와 preview 여부를 받아 준비 상태를 검사하며 반환값은 없다.
        /// preview=true는 설명만 표시한다. false는 현재 계정에 지급·소비·진행을 저장하며 중복 방지·화면 정리는 Manager에 맡긴다.
        /// 완료한 안내는 정상적인 생략으로 로그를 남긴다. 테스트 메뉴도 기존 완료·지급 이력을 지우지 않는다.
        /// </summary>
        private static void RequestGuide(string sequenceId, bool preview)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[GameTest] bootstrap부터 Play 후 메인 화면에서 안내 테스트를 실행하세요.");
                return;
            }

            var directManager = GameDirectManager.Instance;
            if (directManager == null)
            {
                Debug.LogWarning("[GameTest] GameDirectManager가 준비되지 않았습니다. bootstrap부터 실행하세요.");
                return;
            }

            if (!preview && new GameDirectProgressStore().TryLoad(sequenceId, LocalProgression.AccountGeneration, out var progress) &&
                (progress.Completed || progress.Skipped))
            {
                Debug.Log("[GameTest] 안내 생략: " + sequenceId + " (이미 완료하거나 건너뛴 안내)");
                return;
            }

            if (!directManager.RequestPlay(sequenceId, preview))
            {
                Debug.LogWarning("[GameTest] 안내 요청 실패: " + sequenceId + " / " + directManager.LastError);
                return;
            }

            Debug.Log("[GameTest] 안내 요청: " + sequenceId + (preview ? " (설명 미리보기)" : " (현재 계정에 실습 결과와 진행 저장)"));
        }
        [MenuItem("GameTest/ReincarnationTest/Reincarnation")]
        public static void Reincarnation()
        {
            var game = GameManager.Instance;
            if (!Application.isPlaying || game == null || game.Reincarnation == null)
            { Debug.LogWarning("[GameTest] bootstrap부터 Play 후 계정이 준비되면 환생 테스트를 실행하세요."); return; }
            game.Reincarnation.TryReincarnate();
        }

        [MenuItem("GameTest/Stage Test/Clear Stage", true)]
        [MenuItem("GameTest/Dungeon Test/입장 티켓 충전", true)]
        [MenuItem("GameTest/CheckIn Test/1일 진행", true)]
        [MenuItem("GameTest/CheckIn Test/출석 보상판 초기화", true)]
        [MenuItem("GameTest/Shop Ad Test/오늘 광고 수령 초기화 (재지급 허용)", true)]
        [MenuItem("GameTest/Mailbox Test/골드 우편 보내기", true)]
        [MenuItem("GameTest/Mailbox Test/보석(비전지식) 우편 보내기", true)]
        [MenuItem("GameTest/Mailbox Test/고대주화 우편 보내기", true)]
        [MenuItem("GameTest/Mailbox Test/전직 파편 우편 보내기", true)]
        [MenuItem("GameTest/Mailbox Test/4종 전부 우편 보내기", true)]
        [MenuItem("GameTest/Guide Test/실제 계정 저장/1 육성", true)]
        [MenuItem("GameTest/Guide Test/실제 계정 저장/2 왕국군", true)]
        [MenuItem("GameTest/Guide Test/실제 계정 저장/3 던전", true)]
        [MenuItem("GameTest/Guide Test/실제 계정 저장/4 뽑기", true)]
        [MenuItem("GameTest/Guide Test/실제 계정 저장/5 마탑 스킬", true)]
        [MenuItem("GameTest/Guide Test/실제 계정 저장/다섯 안내 순차 실행", true)]
        [MenuItem("GameTest/Guide Test/실제 계정 저장/다섯 안내 테스트 기록 초기화 (재지급 허용)", true)]
        [MenuItem("GameTest/Guide Test/현재 안내 나중에 계속", true)]
        [MenuItem("GameTest/Guide Test/환생 안내", true)]
        [MenuItem("GameTest/ReincarnationTest/Reincarnation", true)]
        private static bool ValidatePlayMode() => Application.isPlaying;
    #endif
    }
}
