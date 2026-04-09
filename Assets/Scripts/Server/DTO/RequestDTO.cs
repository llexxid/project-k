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
}

