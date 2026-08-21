using System.Collections.Generic;
using Core.Stage.Action.Tasks;
using Core.Stage.Presentation;
using Scripts.Core;
using Scripts.Monster.SO;

namespace Core.Stage.Action
{
    /// <summary>
    /// StageDefinition에 맞는 Action 순서를 조립한다.
    /// 보스 입장·중간 기믹·클리어 연출은 이 조합 지점에 Task로 추가한다.
    /// </summary>
    public static class StageActionSequenceFactory
    {
        public static StageActionSequence Create(
            StageSession session,
            MonsterSpawnLocationSO spawnLocation)
        {
            var context = new StageActionContext(
                session,
                new StageSpawnController(),
                spawnLocation,
                StageCameraDirector.Instance);

            var tasks = new List<StageActionTask>
            {
                new SpawnStageMonstersActionTask()
            };

            // 보스가 실제 초기 스폰 목록에 있는 스테이지만 입장 연출을 실행한다.
            // Main 보스와 던전 보스를 콘텐츠 종류와 무관하게 같은 규칙으로 처리할 수 있다.
            if (HasBossSpawnEntry(session.Definition))
                tasks.Add(new BossIntroActionTask());

            tasks.Add(new RunStageBattleActionTask());

            return new StageActionSequence(context, tasks);
        }

        /// <summary>현재 스테이지 데이터에 보스 단계로 지정된 몬스터가 있는지 확인한다.</summary>
        private static bool HasBossSpawnEntry(StageDefinition definition)
        {
            foreach (StageMonsterEntry entry in definition.MonsterEntries)
            {
                if (entry.SpawnPhase == eMonsterSpawnPhase.Boss && entry.Count > 0)
                    return true;
            }

            return false;
        }
    }
}
