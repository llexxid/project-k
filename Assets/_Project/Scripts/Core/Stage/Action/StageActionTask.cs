using System;

namespace Core.Stage.Action
{
    /// <summary>StageActionTask의 현재 생명주기 상태다.</summary>
    public enum eStageActionTaskState
    {
        Waiting,
        Running,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>스테이지 Action에 scaled/unscaled 프레임 시간을 함께 전달한다.</summary>
    public readonly struct StageActionTime
    {
        public static StageActionTime Zero => new StageActionTime(0f, 0f);

        public float DeltaTime { get; }
        public float UnscaledDeltaTime { get; }

        public StageActionTime(float deltaTime, float unscaledDeltaTime)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
        }
    }

    /// <summary>
    /// 스테이지 진행 중 순서대로 실행되는 순수 C# 작업의 기반 클래스다.
    /// Sequence가 시작·갱신·종료·취소 호출을 각각 한 번만 보장한다.
    /// </summary>
    public abstract class StageActionTask
    {
        public eStageActionTaskState State { get; private set; } = eStageActionTaskState.Waiting;
        public Exception Failure { get; private set; }
        public string Name => GetType().Name;

        internal void Tick(StageActionContext context, StageActionTime time)
        {
            if (State == eStageActionTaskState.Waiting)
            {
                State = eStageActionTaskState.Running;
                InvokeSafely(() => OnStart(context));
            }

            if (State == eStageActionTaskState.Running)
                InvokeSafely(() => OnUpdate(context, time));

            if (State == eStageActionTaskState.Completed)
                InvokeSafely(() => OnEnd(context));
        }

        internal void Cancel(StageActionContext context)
        {
            if (State != eStageActionTaskState.Running)
                return;

            State = eStageActionTaskState.Cancelled;
            try
            {
                OnCancel(context);
            }
            catch (Exception exception)
            {
                Failure = exception;
                State = eStageActionTaskState.Failed;
            }
        }

        /// <summary>현재 Task를 정상 완료 상태로 변경한다.</summary>
        protected void Complete()
        {
            if (State == eStageActionTaskState.Running)
                State = eStageActionTaskState.Completed;
        }

        /// <summary>복구할 수 없는 오류로 현재 Task를 실패시킨다.</summary>
        protected void Fail(Exception exception)
        {
            if (State != eStageActionTaskState.Running)
                return;

            Failure = exception ?? new InvalidOperationException($"{Name} failed.");
            State = eStageActionTaskState.Failed;
        }

        protected virtual void OnStart(StageActionContext context)
        {
        }

        protected virtual void OnUpdate(StageActionContext context, StageActionTime time)
        {
        }

        protected virtual void OnEnd(StageActionContext context)
        {
        }

        protected virtual void OnCancel(StageActionContext context)
        {
        }

        private void InvokeSafely(System.Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Failure = exception;
                State = eStageActionTaskState.Failed;
            }
        }
    }
}
