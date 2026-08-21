using System.Collections.Generic;
using Core.Stage.Action.Tasks;
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
                spawnLocation);

            var tasks = new List<StageActionTask>
            {
                new SpawnStageMonstersActionTask()
            };

            tasks.Add(new RunStageBattleActionTask());

            return new StageActionSequence(context, tasks);
        }
    }
}
