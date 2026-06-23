using System;
using System.Collections;
using System.Collections.Generic;
using Scripts.Monster;
using UnityEngine;

namespace Core.Stage
{
    public interface IStageRule
    {
        event Action Clear;
        event Action Fail;

        void Enter();
        void OnMonsterKilled(Monster monster,int remainCount);
        void Exit();
    }

    public sealed class KillAllRule : IStageRule
    {
        public event Action Clear;
        public event Action Fail;

        public void OnMonsterKilled(Monster monster, int remainCount)
        {
            if (remainCount == 0)
            {
                Clear?.Invoke();
            }
        }
        public void Enter(){}
        public void Exit(){}
    }
}