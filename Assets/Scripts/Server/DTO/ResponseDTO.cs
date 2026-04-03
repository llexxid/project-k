using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PlayerSkill;

namespace Scripts.Server.DTO
{
	public class UserOnSignUpInitResponseDTO
	{
		public string SessionGUID { get; set; }
		public int Level { get; set; }
		public long Exp { get; set; }
		public long KillScore { get; set; }

		public eStage CurrentStage { get; set; }
		//강화 수치
		public long EnchantHPCount { get; set; }
		public long EnchantATKCount { get; set; }

		public List<CharacterDataQuery> CharacterDatas { get; set; }
	}

	public class UserInitResponseDTO
	{
		public string SessionGUID { get; set; }
		public int Level { get; set; }
		public long Exp { get; set; }
		public long KillScore { get; set; }

		public eStage CurrentStage { get; set; }
		//강화 수치
		public long EnchantHPCount { get; set; }
		public long EnchantATKCount { get; set; }

		public List<CharacterDataQuery> CharacterDatas { get; set; }
		//캐릭터가 장착한 장비정보
		public List<CharacterEquipmentQuery> CharacterEquipmentDatas { get; set; }
		//SkillTree
		public List<SkillCode> SkillTreeDatas { get; set; }
		//Inventory
		public List<ItemCode> Inventory { get; set; }
	}
	public struct OnAuthInitResponseDTO
	{
		public string SessionID { get; set; }
	}

	public class OnHuntResponseDTO
	{ 
		public int Level { get; set; }
		public long Exp { get; set; }
		public long KillScore { get; set; }
		public int Gold { get; set; }
		public int AncientCoin { get; set; }
	}
}

