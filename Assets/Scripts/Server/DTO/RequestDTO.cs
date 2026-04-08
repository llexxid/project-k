using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Server.DTO
{
	/*
		[48 - 16] 몬스터 ID (33bit)
		[15 - 0] 몬스터의 수 (16bit)
	 */
	public struct RewardCode
	{
		public ulong Code { get; set; }
	}

	public class InitUserRequestDTO
	{
	}

	public class AuthRequestDTO
	{
		public string SessinID { get; set; }
	}

	public class OnRewardRequestDTO
	{
		public string SessionID { get; set; }
		public List<RewardCode> Loots { get; set; }
	}
}

