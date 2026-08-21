using System;

namespace Core.Stage.Action.Tasks
{
    /// <summary>현재 Definition의 초기 몬스터를 스폰하고 세션에 등록한다.</summary>
    public sealed class SpawnStageMonstersActionTask : StageActionTask
    {
        protected override void OnStart(StageActionContext context)
        {
            if (!context.SpawnController.Begin(context.Session, context.SpawnLocation))
            {
                Fail(new InvalidOperationException(
                    $"Failed to initialize monster spawning for stage {context.Definition.Id}."));
                return;
            }

            Complete();
        }
    }
}
