using System;

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

		/// <summary>
		/// state´Â Ä³½ÌµÈ »óÅÂ·Î ¾¹´Ï´Ù.
		/// </summary>
		/// <param name="state"></param>
		public void ChangeState(EntityState<T> state)
		{
			if (_currentState == state && state.GetTrigger() == false)
			{
				return;
			}
			_currentState.OnExit();
			_currentState = state;
			_currentState.OnEnter();
		}
	}
}
