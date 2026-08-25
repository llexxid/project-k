using System;

namespace Core.Stage.Action.Tasks
{
    /// <summary>지정한 scaled 또는 unscaled 시간 동안 다음 Action 진행을 대기한다.</summary>
    public sealed class StageDelayActionTask : StageActionTask
    {
        private readonly float _duration;
        private readonly bool _useUnscaledTime;
        private float _elapsedTime;

        public StageDelayActionTask(float duration, bool useUnscaledTime = false)
        {
            if (duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));

            _duration = duration;
            _useUnscaledTime = useUnscaledTime;
        }

        protected override void OnStart(StageActionContext context)
        {
            _elapsedTime = 0f;
            if (_duration <= 0f)
                Complete();
        }

        protected override void OnUpdate(StageActionContext context, StageActionTime time)
        {
            _elapsedTime += _useUnscaledTime
                ? time.UnscaledDeltaTime
                : time.DeltaTime;

            if (_elapsedTime >= _duration)
                Complete();
        }
    }
}
