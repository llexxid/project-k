using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Scripts.Server.DTO
{
	public class UserOnSignUpInitResponseDTO
	{
		public string SessionGUID { get; set; }
		public int Level { get; set; }
		public long Exp { get; set; }
		public long KillScore { get; set; }

		public eStage CurrentStage { get; set; }
		//��ȭ ��ġ
		public long EnchantHPCount { get; set; }
		public long EnchantATKCount { get; set; }

		public CurrencyQueryDTO Currency { get; set; }

		public List<CharacterDataQuery> CharacterDatas { get; set; }

		public List<JobTreeQuery> JobTrees { get; set; }
	}
	//.
	public class UserInitResponseDTO
	{
		public string SessionGUID { get; set; }
		public int Level { get; set; }
		public long Exp { get; set; }
		public long KillScore { get; set; }

		public eStage CurrentStage { get; set; }
		//��ȭ ��ġ
		public long EnchantHPCount { get; set; }
		public long EnchantATKCount { get; set; }

		public List<CharacterDataQuery> CharacterDatas { get; set; }
		//ĳ���Ͱ� ������ �������
		public List<CharacterEquipmentQuery> CharacterEquipmentDatas { get; set; }
		//��ȭ 
		public CurrencyQueryDTO Currency { get; set; }

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
		public long Gold { get; set; }
		public long AncientCoin { get; set; }
	}

	public class OnStageClearResponseDTO
	{
		//For Debugging
		public eStage Stage { get; set; }
	}

	public class OnGachaEquipmentClassFragmentResponseDTO
	{
		//For Debugging
		public List<ItemCode> GachaList { get; set; }
		public int TotalCount { get; set; }
	}

	public class OnGachaSkillArcaneKnowledgeResponseDTO
	{
		//For Debugging
		public List<SkillCode> GachaList { get; set; }
		public int TotalCount { get; set; }
	}

	public class OnEnchantResponseDTO
	{
		public long CurrentLevel { get; set; }
	}

	public class OnEnchantEquipmentResponseDTO
	{
		public ItemCode ItemCode { get; set; }
	}

	public class OnEnchantSkillResponseDTO
	{
		public SkillCode SkillCode { get; set; }
	}

	public class OnAwakeningSkillResponseDTO
	{
		public SkillCode SkillCode { get; set; }
	}
	public class OnGetJobResponseDTO
	{
		public int Index { get; set; }
		public ulong JobCode { get; set; }
	}

	public class OnChangeJobResponseDTO
	{
		public int Index { get; set; }
		public ulong JobCode { get; set; }
	}
}

