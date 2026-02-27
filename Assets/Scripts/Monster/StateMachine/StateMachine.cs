using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core.StateMachine
{
	public class StateMachine<T>
	{
		EntityState<T> _currentState;
		public EntityState<T> currentState
		{
			get { return _currentState; }
		}
		public void BeginMachine(EntityState<T> state)
		{
			_currentState = state;
			_currentState.OnEnter();
		}

		public void ChangeState(EntityState<T> state)
		{
			_currentState.OnExit();
			_currentState = state;
			_currentState.OnEnter();
		}
	}
}
