namespace Core.Stage.Action.Tasks
{
    /// <summary>Rule 결과가 생길 때까지 전투 규칙과 반복 스폰을 갱신한다.</summary>
    public sealed class RunStageBattleActionTask : StageActionTask
    {
        protected override void OnStart(StageActionContext context)
        {
            context.BeginBattle();
            if (context.HasPendingResult)
                Complete();
        }

        protected override void OnUpdate(StageActionContext context, StageActionTime time)
        {
            context.TickBattle(time);
            if (context.HasPendingResult)
                Complete();
        }
    }
}
