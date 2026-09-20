using KingdomIdle.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>가이드 HUD와 퀘스트 카드가 함께 쓰는 목표별 화면 이동이다. 퀘스트 진행·보상은 변경하지 않는다.</summary>
    public static class QuestNavigation
    {
        /// <summary>메뉴에서 수행하는 목표의 목적지를 반환한다. 전투 목표는 별도 화면이 없다.</summary>
        public static UIPanelId? Destination(eQuestObjectiveType objective) => objective switch
        {
            eQuestObjectiveType.GachaUse or eQuestObjectiveType.EquipmentObtain => UIPanelId.Gacha,
            eQuestObjectiveType.DungeonEnter or eQuestObjectiveType.DungeonClear => UIPanelId.Dungeon,
            eQuestObjectiveType.LevelUp or eQuestObjectiveType.StatEnhance => UIPanelId.Development,
            eQuestObjectiveType.EquipmentEquip or eQuestObjectiveType.JobChange or
                eQuestObjectiveType.EquipmentEnhance or eQuestObjectiveType.Enhance => UIPanelId.KingdomArmy,
            eQuestObjectiveType.SkillObtain => UIPanelId.Gacha,
            eQuestObjectiveType.ItemUse => UIPanelId.Inventory,
            _ => null
        };

        /// <summary>별도 목적지가 없는 목표 중 전투 복귀로 수행할 수 있는 종류를 명시한다.</summary>
        public static bool IsBattle(eQuestObjectiveType objective) => objective is
            eQuestObjectiveType.StageClear or eQuestObjectiveType.MainWaveClear or eQuestObjectiveType.MonsterKill or
            eQuestObjectiveType.BossKill or eQuestObjectiveType.BattleTime or eQuestObjectiveType.PlayerLevel or
            eQuestObjectiveType.SkillCast;

        /// <summary>이동할 목적지가 있는지 UI 버튼의 활성 판정에 사용한다.</summary>
        public static bool CanNavigate(eQuestObjectiveType objective) => Destination(objective).HasValue || IsBattle(objective) ||
            objective is eQuestObjectiveType.SkillEquip or eQuestObjectiveType.SkillEnhance or eQuestObjectiveType.SkillAwaken or
                eQuestObjectiveType.Reincarnate or eQuestObjectiveType.ReincarnationLevel;

        /// <summary>메뉴를 스택에 쌓아 뒤로가기로 퀘스트에 복귀한다. 전투 목표는 패널을 닫는다.</summary>
        public static void Navigate(eQuestObjectiveType objective)
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            // 마탑과 환생은 실제 구현된 팝업 진입점을 사용한다. 미구현 panel enum으로 이동하지 않는다.
            if (objective is eQuestObjectiveType.SkillEquip or eQuestObjectiveType.SkillEnhance or eQuestObjectiveType.SkillAwaken)
                MageTowerPopupController.Show();
            else if (objective is eQuestObjectiveType.Reincarnate or eQuestObjectiveType.ReincarnationLevel)
                ReincarnationPopupController.Show();
            else if (Destination(objective) is UIPanelId panel) ui.PushPanel(panel);
            else if (IsBattle(objective)) ui.ClearPanels();
        }
    }
}
