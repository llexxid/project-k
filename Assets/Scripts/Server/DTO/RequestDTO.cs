using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Server.DTO
{
	public struct HuntResult
	{
		public eMonsterType MonsterType { get; set; }
		public short Count { get; set; }
	}
	public class InitUserRequestDTO
	{

	}

	public struct AuthRequestDTO
	{
		public string SessinID { get; set; }
	}
	public class OnRewardRequestDTO
	{
		public string SessionID { get; set; }
		public List<HuntResult> Loots { get; set; }
	}

	public class OnStageClearRequestDTO
	{
		public string SessionID { get; set; }
	}

	public class OnGachaRequestDTO
	{
		public string SessionID { get; set; }
		public int Count { get; set; }
	}

	public class OnEnchantRequestDTO
	{
		public string SessionID { get; set; }
		public int Count { get; set; }
	}

	public class OnEnchantEquipmentRequestDTO
	{
		public string SessionID { get; set; }
		public ItemCode ItemCode { get; set; }
	}

	public class OnEnchantSkillBaseRequest
	{
		public string SessionID { get; set; }
		public SkillCode SkillCode { get; set; }
	}

	public class OnEnchantSkillRequestDTO : OnEnchantSkillBaseRequest
	{

	}

	public class OnAwakeningSkillRequestDTO : OnEnchantSkillBaseRequest
	{

	}
	public class OnJobRequestBaseDTO
	{
		public string SessionID { get; set; }
		public int Index { get; set; }
		public ulong JobCode { get; set; }
	}
	public class OnGetJobRequestDTO : OnJobRequestBaseDTO
	{

	}

}

