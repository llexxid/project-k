using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core.StateMachine
{
	public abstract class EntityState<T>
	{
		protected T _owner;
		protected bool _canRetriggered;
		public EntityState(T owner)
		{
			_owner = owner;

		}
		public bool GetTrigger()
		{
			return _canRetriggered;
		}

		public virtual void OnEnter()
		{

		}

		public virtual void OnUpdate()
		{

		}
		public virtual void OnExit()
		{
			
		}
	}

}