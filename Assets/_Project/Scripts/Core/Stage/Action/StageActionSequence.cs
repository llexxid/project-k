using System;
using System.Collections.Generic;

namespace Core.Stage.Action
{
    public enum eStageActionSequenceState
    {
        Waiting,
        Running,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>
    /// 한 StageSession의 Action 목록을 순서대로 실행하고 현재 Task의 생명주기를 소유한다.
    /// </summary>
    public sealed class StageActionSequence
    {
        public eStageActionSequenceState State { get; private set; } = eStageActionSequenceState.Waiting;
        public StageActionTask CurrentTask =>
            _currentIndex < _tasks.Count ? _tasks[_currentIndex] : null;
        public Exception Failure { get; private set; }

        private readonly StageActionContext _context;
        private readonly IReadOnlyList<StageActionTask> _tasks;
        private int _currentIndex;

        public StageActionSequence(
            StageActionContext context,
            IReadOnlyList<StageActionTask> tasks)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));

            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i] == null)
                    throw new ArgumentException($"Task at index {i} is null.", nameof(tasks));
            }
        }

        /// <summary>현재 Task를 갱신하고 완료되면 같은 프레임에 다음 즉시 완료 Task까지 진행한다.</summary>
        public void Tick(StageActionTime time)
        {
            if (State == eStageActionSequenceState.Waiting)
                State = eStageActionSequenceState.Running;

            if (State != eStageActionSequenceState.Running)
                return;

            StageActionTime currentTime = time;
            int transitionGuard = _tasks.Count + 1;

            while (_currentIndex < _tasks.Count && transitionGuard-- > 0)
            {
                StageActionTask task = _tasks[_currentIndex];
                task.Tick(_context, currentTime);

                if (task.State == eStageActionTaskState.Failed)
                {
                    Failure = task.Failure ?? new InvalidOperationException($"{task.Name} failed.");
                    State = eStageActionSequenceState.Failed;
                    _context.Stop();
                    return;
                }

                if (task.State != eStageActionTaskState.Completed)
                    return;

                _currentIndex++;

                // 같은 프레임에 다음 Task를 시작하되 프레임 시간을 두 번 소비하지 않는다.
                currentTime = StageActionTime.Zero;
            }

            if (_currentIndex >= _tasks.Count)
            {
                State = eStageActionSequenceState.Completed;
                _context.Stop();
            }
        }

        /// <summary>스테이지 전환이나 재시작 시 현재 실행 중인 Task를 취소한다.</summary>
        public void Cancel()
        {
            if (State != eStageActionSequenceState.Waiting &&
                State != eStageActionSequenceState.Running)
            {
                return;
            }

            CurrentTask?.Cancel(_context);
            if (CurrentTask?.State == eStageActionTaskState.Failed)
            {
                Failure = CurrentTask.Failure;
                State = eStageActionSequenceState.Failed;
            }
            else
            {
                State = eStageActionSequenceState.Cancelled;
            }

            _context.Stop();
        }
    }
}
