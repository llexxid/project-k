using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.SO
{
	[Serializable]
	public struct MonsterAnimData
	{
		public eMonsterAction actionType;
		public AnimationClip clip;     
	}

	[CreateAssetMenu(fileName = "MonsterAnimationDataSO", menuName = "ScriptableObjects/MonsterAnimationDataSO")]
	public class MonsterAnimationSO : ScriptableObject
	{
		[SerializeField]
		private List<MonsterAnimData> _datas;
		private Dictionary<eMonsterAction, float> _animationClipDic;
		private void OnEnable()
		{
			_animationClipDic = new Dictionary<eMonsterAction, float>();

			foreach (var data in _datas)
			{
				_animationClipDic.Add(data.actionType, data.clip.length);
			}
		}

		public float GetAnimationLength(eMonsterAction action)
		{
			return _animationClipDic[action];
		}
	}
}

