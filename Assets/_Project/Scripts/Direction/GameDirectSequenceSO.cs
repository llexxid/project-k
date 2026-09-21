using System;
using System.Collections.Generic;
using UnityEngine;

namespace Direction
{
    /// <summary>화면 이름이나 하이어라키 경로 대신 사용하는 안정적인 안내 대상이다.</summary>
    public enum GameDirectTarget
    {
        Development, KingdomArmy, Dungeon, Gacha, Reincarnation,
        GoldGrowthTab, AttackOnce, ArmyMember, ArmyStats, ArmySkills,
        ArmyEquipmentTab, ArmyEquipment, ArmyJobsTab, ArmyJobCard, ArmyJobStats, ArmyJobSkills,
        GoldDungeonCard, RubyDungeonCard, DungeonDetail, DungeonDetailClose,
        EquipmentGachaTab, SkillGachaTab, EquipmentPullOnce, SkillPullOnce, GachaResultClose,
        MageTower, MageSkillList, MageEquippedSlots, MageEquipAction, MageUnequipAction
    }
    public enum GuideCompletion { Confirm, Click, Action }
    public enum GuideContext { Main, Development, ArmyCharacter, ArmyEquipment, ArmyJobs, ArmyJobDetail, Dungeon, GoldDungeonDetail, RubyDungeonDetail, EquipmentGacha, SkillGacha, MageTower }
    public enum GuideAction { None, AttackOnce, EquipmentPullOnce, SkillPullOnce }
    public enum GuideCardPlacement { Auto, Above, Below }
    public enum GameDirectResult { Confirmed, Skipped, Deferred }

    /// <summary>기획자가 편집하는 한 단계의 정의다. 실행 위치와 완료 여부는 이 에셋에 쓰지 않는다.</summary>
    [Serializable]
    public sealed class GameDirectStepData
    {
        public string id;
        public GameDirectTarget target;
        public string title;
        [TextArea(2, 5)] public string description;
        public GuideCardPlacement placement;
        public GuideCompletion completion;
        public GuideContext context;
        public GuideContext destination;
        public GuideAction action;
        public GameDirectTarget[] allowedTargets = Array.Empty<GameDirectTarget>();
        public bool allowScroll;
        public bool grantPracticeCoins;
    }

    /// <summary>Player에 전달하는 실행 전용 복사본이다. PlayMode 중 원본 SO를 수정하지 않게 분리한다.</summary>
    public sealed class GameDirectStep
    {
        public string Id { get; }
        public GameDirectTarget Target { get; }
        public string Title { get; }
        public string Description { get; }
        public GuideCardPlacement Placement { get; }
        public int Number { get; }
        public int Count { get; }
        public GuideCompletion Completion { get; }
        public GuideContext Context { get; }
        public GuideContext Destination { get; }
        public GuideAction Action { get; }
        public GameDirectTarget[] AllowedTargets { get; }
        public bool AllowScroll { get; }
        public bool GrantPracticeCoins { get; }
        public bool Interactive => Completion != GuideCompletion.Confirm || Context != GuideContext.Main || GrantPracticeCoins;

        /// <summary>정의의 표시 정보와 이번 실행의 순번을 복사한다. 원본 에셋 참조는 보관하지 않는다.</summary>
        public GameDirectStep(GameDirectStepData data, int number, int count)
        {
            Id = data.id; Target = data.target; Title = data.title;
            Description = data.description; Placement = data.placement;
            Number = number; Count = count;
            Completion = data.completion; Context = data.context; Destination = data.destination;
            Action = data.action; AllowedTargets = (GameDirectTarget[])(data.allowedTargets ?? Array.Empty<GameDirectTarget>()).Clone();
            AllowScroll = data.allowScroll; GrantPracticeCoins = data.grantPracticeCoins;
        }
    }

    /// <summary>수동 요청으로 재생할 연출 정의다. 자동 발생 조건은 이후 별도 트리거가 Manager에 요청한다.</summary>
    [CreateAssetMenu(menuName = "KingdomIdle/Direction/Sequence", fileName = "DirectSequence")]
    public sealed class GameDirectSequenceSO : ScriptableObject
    {
        public string sequenceId;
        public GameDirectStepData[] steps = Array.Empty<GameDirectStepData>();
        public bool IsInteractive => Array.Exists(steps, s => s != null &&
            (s.completion != GuideCompletion.Confirm || s.context != GuideContext.Main || s.grantPracticeCoins));

        /// <summary>실행 전에 저장 키와 단계 정의를 검증한다. 잘못된 데이터는 입력을 막기 전에 거절한다.</summary>
        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(sequenceId) || steps == null || steps.Length == 0)
                error = "연출 ID와 한 개 이상의 단계가 필요합니다.";
            else
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var step in steps)
                    if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id) ||
                        string.IsNullOrWhiteSpace(step.title) || string.IsNullOrWhiteSpace(step.description) ||
                        !Enum.IsDefined(typeof(GameDirectTarget), step.target) ||
                        !Enum.IsDefined(typeof(GuideCardPlacement), step.placement) ||
                        !Enum.IsDefined(typeof(GuideCompletion), step.completion) ||
                        !Enum.IsDefined(typeof(GuideContext), step.context) ||
                        !Enum.IsDefined(typeof(GuideContext), step.destination) ||
                        !Enum.IsDefined(typeof(GuideAction), step.action) ||
                        (step.completion == GuideCompletion.Action && step.action == GuideAction.None) ||
                        (step.completion != GuideCompletion.Confirm && (step.allowedTargets == null || Array.IndexOf(step.allowedTargets, step.target) < 0)) ||
                        (step.grantPracticeCoins && step.completion != GuideCompletion.Action) ||
                        (step.grantPracticeCoins && step.action != GuideAction.EquipmentPullOnce && step.action != GuideAction.SkillPullOnce) ||
                        Array.Exists(step.allowedTargets ?? Array.Empty<GameDirectTarget>(), t => !Enum.IsDefined(typeof(GameDirectTarget), t)))
                    { error = "단계 ID 중복 또는 필수 표시 정보 누락입니다."; break; }
            }
            return error == null;
        }
    }
}
