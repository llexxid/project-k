using Direction;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Test
{
    public class GameTest : MonoBehaviour
    {
    #if UNITY_EDITOR
        private StageManager manager;
        private static readonly string[] GuideTestIds = {
            "guide_development", "guide_kingdom_army", "guide_dungeon", "guide_gacha", "guide_mage_tower"
        };

        [ContextMenu("Stage Test/Clear Stage")] 
        public void TestClearStage()
        {
            if (!TryGetStageManager())
                return;

            if (!manager.TestClearStage())
                Debug.LogWarning("[GameTest] 현재 스테이지를 클리어할 수 없습니다.");
        }

        /// <summary>현재 계정에서 육성 실습을 요청한다. 실제 골드를 소비하고 강화·완료를 저장하며 정리는 Manager에 맡긴다.</summary>
        [ContextMenu("Guide Test/실제 계정 저장/1 육성")]
        public void TestDevelopmentGuide() => RequestGuide("guide_development", false);

        /// <summary>현재 계정에서 왕국군 정보 열람 실습을 요청한다. 장착·전직 없이 안내 진행을 저장한다.</summary>
        [ContextMenu("Guide Test/실제 계정 저장/2 왕국군")]
        public void TestArmyGuide() => RequestGuide("guide_kingdom_army", false);

        /// <summary>현재 계정에서 던전 정보 안내를 요청한다. 실제 입장 없이 확인한 단계만 저장한다.</summary>
        [ContextMenu("Guide Test/실제 계정 저장/3 던전")]
        public void TestDungeonGuide() => RequestGuide("guide_dungeon", false);

        /// <summary>현재 계정에서 뽑기 실습을 요청한다. 50개씩 일회 지급·실제 소비·획득·완료가 모두 저장된다.</summary>
        [ContextMenu("Guide Test/실제 계정 저장/4 뽑기")]
        public void TestGachaGuide() => RequestGuide("guide_gacha", false);

        /// <summary>현재 계정에서 마탑 창을 열고 장착·해제를 설명한다. 실제 장착을 바꾸지 않고 안내 확인만 저장한다.</summary>
        [ContextMenu("Guide Test/실제 계정 저장/5 마탑 스킬")]
        public void TestMageTowerGuide() => RequestGuide("guide_mage_tower", false);

        /// <summary>독립 SO 다섯 개를 FIFO로 요청한다. 이미 완료한 안내는 거절되고 나머지만 순서대로 실행된다.</summary>
        [ContextMenu("Guide Test/실제 계정 저장/다섯 안내 순차 실행")]
        public void TestFirstStartGuide()
        { TestDevelopmentGuide(); TestArmyGuide(); TestDungeonGuide(); TestGachaGuide(); TestMageTowerGuide(); }

        /// <summary>
        /// ContextMenu에서 다섯 안내의 테스트 기록만 초기화한다. 실행·대기열 정리는 Manager가 맡고 성공한 저장 뒤에만 완료 로그를 남긴다.
        /// 현재 보유 내역은 유지하지만 실습·지급 이력도 지우므로 이후 다시 강화·뽑기를 수행하고 체험 주화를 받을 수 있다.
        /// </summary>
        [ContextMenu("Guide Test/실제 계정 저장/다섯 안내 테스트 기록 초기화 (재지급 허용)")]
        public async void ResetGuideTestRecords()
        {
            var direct = GameDirectManager.Instance;
            if (!Application.isPlaying || direct == null)
            { Debug.LogWarning("[GameTest] bootstrap부터 Play 후 계정이 준비되면 초기화하세요.", this); return; }
            bool reset = await direct.ResetForTestingAsync(generation => new GameDirectProgressStore().ResetForTesting(GuideTestIds, generation));
            if (!reset) { Debug.LogWarning("[GameTest] 안내 초기화 실패: " + direct.LastError, this); return; }
            Debug.Log("[GameTest] 다섯 안내의 진행·실습 성공·주화 지급 기록을 초기화했습니다. 보유 내역은 유지됩니다. 개별 또는 순차 실행으로 다시 테스트하세요.", this);
        }

        /// <summary>현재 안내를 미완료로 중단한다. 확인한 단계와 이미 지급·소비한 재화는 보존한다.</summary>
        [ContextMenu("Guide Test/현재 안내 나중에 계속")]
        public void DeferGuide() => GameDirectManager.Instance?.CancelCurrent();

        /// <summary>PlayMode의 ContextMenu에서 환생 버튼 안내를 요청한다. 실제 환생은 실행하지 않고 안내만 미리본다.</summary>
        [ContextMenu("Guide Test/환생 안내")]
        public void TestReincarnationGuide() => RequestGuide("first_reincarnation_guide", true);

        /// <summary>
        /// 실습·미리보기 메뉴의 공통 요청 창구다. 연출 ID와 preview 여부를 받아 준비 상태를 검사하며 반환값은 없다.
        /// preview=true는 설명만 표시한다. false는 현재 계정에 지급·소비·진행을 저장하며 중복 방지·화면 정리는 Manager에 맡긴다.
        /// 완료한 안내는 정상적인 생략으로 로그를 남긴다. 테스트 메뉴도 기존 완료·지급 이력을 지우지 않는다.
        /// </summary>
        private void RequestGuide(string sequenceId, bool preview)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[GameTest] bootstrap부터 Play 후 메인 화면에서 안내 테스트를 실행하세요.", this);
                return;
            }

            var directManager = GameDirectManager.Instance;
            if (directManager == null)
            {
                Debug.LogWarning("[GameTest] GameDirectManager가 준비되지 않았습니다. bootstrap부터 실행하세요.", this);
                return;
            }

            if (!preview && new GameDirectProgressStore().TryLoad(sequenceId, LocalProgression.AccountGeneration, out var progress) &&
                (progress.Completed || progress.Skipped))
            {
                Debug.Log("[GameTest] 안내 생략: " + sequenceId + " (이미 완료하거나 건너뛴 안내)", this);
                return;
            }

            if (!directManager.RequestPlay(sequenceId, preview))
            {
                Debug.LogWarning("[GameTest] 안내 요청 실패: " + sequenceId + " / " + directManager.LastError, this);
                return;
            }

            Debug.Log("[GameTest] 안내 요청: " + sequenceId + (preview ? " (설명 미리보기)" : " (현재 계정에 실습 결과와 진행 저장)"), this);
        }
        private bool TryGetStageManager()
        {
            if (StageManager.Instance == null)
                return false;
            if (manager != null) return true;
            manager = StageManager.Instance;
            return true;
        }

        [ContextMenu("ReincarnationTest/Reincarnation")]
        public void Reincarnation()
        {
            GameManager.Instance.Reincarnation.TryReincarnate();
        }
    #endif
    }
}
