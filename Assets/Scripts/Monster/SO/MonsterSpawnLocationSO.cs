using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.SO
{

	[CreateAssetMenu(fileName = "MonsterSpawnLocation", menuName = "ScriptableObjects/Monster/MonsterSpawnLocation")]
	public class MonsterSpawnLocationSO : ScriptableObject
	{
		[SerializeField]
		List<Vector2> _locations;

		public bool TryGetPos(int index, out Vector2 location)
		{
			if (index >= _locations.Count)
			{
				location = default;
				return false;
			}
			location = _locations[index];
			return true;
		}

		public int GetLocationCount()
		{
			return _locations.Count;
		}
	}

}
