using Newtonsoft.Json;
using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static PlayerSkill;

namespace Scripts.Server.DTO
{
	public struct JobStat
	{
		public int HP { get; set; }
		public int ATK { get; set; }
	}

	/* 스킬트리 LayOut 
 * 스킬트리 코드
	[63 - 52] 예약 공간 (12bit)
	[51 - 36] 스킬 코드 (16bit)
	[35 - 28] 강화수치 (8bit)
	[27 - 16] 각성수치 (12bit)
	[15 - 0] 갯수 (16bit)
*/
	public struct SkillCode : IEquatable<SkillCode>
	{
		public ulong Code { get; set; }

		public static SkillCode GenerateSkillCode(eSkillCode skillID)
		{
			SkillCode ret = new SkillCode
			{
				Code = (ulong)skillID << 36
			};
			return ret;
		}

		public override int GetHashCode()
		{
			// ulong 내부에 구현된 GetHashCode를 그대로 사용합니다.
			return Code.GetHashCode();
		}

		//기존 Equals override
		public override bool Equals(object obj)
		{
			if (obj is SkillCode other)
			{
				return Equals(other);
			}
			return false;
		}
		//IEquatable Equals 구현
		public bool Equals(SkillCode other)
		{
			return this.Code == other.Code;
		}

		public static bool operator ==(SkillCode lhs, SkillCode rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(SkillCode lhs, SkillCode rhs)
		{
			return !(lhs.Equals(rhs));
		}

		public void IncreaseCount(int gap = 1)
		{
			ulong count = GetEnchantCount();
			if (count + (ulong)gap >= 0x0000000000010000)
			{
				return;
			}
			Code = Code + (ulong)gap;
		}
		public ulong GetSkillId()
		{
			ulong skillcodeMask = 0x000FFFF000000000;
			return (Code & skillcodeMask) >> 36;
		}
		public ulong GetEnchantCount()
		{
			ulong EnchantCountMask = 0x0000000FF0000000;
			return (Code & EnchantCountMask) >> 28;
		}
		public ulong GetAwakeningCount()
		{
			ulong AwakeCountMask = 0x000000000FFF0000;
			return (Code & AwakeCountMask) >> 16;
		}
		public ulong GetItemAmount()
		{
			ulong ItemAmountMask = 0x000000000000FFFF;
			return (Code & ItemAmountMask);
		}
	}
	/*
	 아이템 코드
[63 - 56]  예약공간   (8bit)
[55 - 40]  아이템 ID (16bit)
[39 - 36]  레어도    (4bit) → eEquipmentRarity
[35 - 32]   슬롯      (4bit) → eEquipmentSlot
[31 - 24] 직업 마스크(8bit) → eJobFlag  (0 = 모든 직업 공용) 
[23 - 16]  강화 수치    (8bit)
[15 - 0]  갯수 (16bit)
	 */
	public struct ItemCode : IEquatable<ItemCode>
	{
		public ulong Code { get; set; }
		public ulong ExpireTimesc { get; set; } // 0인경우 무제한

		public override int GetHashCode()
		{
			// ulong 내부에 구현된 GetHashCode를 그대로 사용합니다.
			return Code.GetHashCode();
		}

		//기존 Equals override
		public override bool Equals(object obj)
		{
			if (obj is ItemCode other)
			{
				return Equals(other);
			}
			return false;
		}
		//IEquatable Equals 구현
		public bool Equals(ItemCode other)
		{
			ulong itemCode = GetItemCode();
			ulong otherItemCode = other.GetItemCode();
			return itemCode == otherItemCode;
		}

		public static bool operator ==(ItemCode lhs, ItemCode rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(ItemCode lhs, ItemCode rhs)
		{
			return !(lhs.Equals(rhs));
		}

		public static ItemCode GenerateItemCode(int itemCode)
		{
			ItemCode ret = new ItemCode();
			ret.Code = (ulong)itemCode << 24;
			ret.ExpireTimesc = 0;
			return ret;
		}

		public void IncreaseCount(int gap = 1)
		{
			ulong count = GetItemAmount();
			if (count + (ulong)gap >= 0x0000000000010000)
			{
				return;
			}
			Code = Code + (ulong)gap;
		}

		public ulong GetItemCode()
		{
			ulong ItemIDMask = 0x00FFFFFFFF000000;
			return (Code & ItemIDMask) >> 24;
		}

		public ulong GetItemRarity()
		{
			ulong ItemRarityMask = 0x000000F000000000;
			return (Code & ItemRarityMask) >> 36;
		}
		public ulong GetItemEquipSlot()
		{
			ulong ItemEquipSlotMask = 0x0000000F00000000;
			return (Code & ItemEquipSlotMask) >> 32;
		}
		public ulong GetItemJobCode()
		{
			ulong ItemJobCodeMask = 0x00000000FF000000;
			return (Code & ItemJobCodeMask) >> 24;
		}
		public ulong GetItemEnchantCount()
		{
			ulong ItemEnchantCountMask = 0x0000000000FF0000;
			return (Code & ItemEnchantCountMask) >> 16;
		}
		public ulong GetItemAmount()
		{
			ulong ItemEnchantCountMask = 0x000000000000FFFF;
			return (Code & ItemEnchantCountMask);
		}
	}


	public class UserCurrentStageQuery
	{
		public eStage CurrentStage { get; set; }
	}

	public class UserDataQuery
	{
		public UserDataQuery(int level, long exp, long monsterkilled)
		{
			Level = level;
			Exp = exp;
			MonsterKilled = monsterkilled;

			dtLastLoginTime = DateTime.UtcNow;
			LastLogInTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			LastRewardTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		}

		public int Level { get; set; }
		public long Exp { get; set; }
		public long MonsterKilled { get; set; }
		//Time
		public DateTime dtLastLoginTime { get; set; }
		public long LastLogInTime { get; set; }
		public long LastRewardTime { get; set; }

		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	public class UserEnhanceMentQuery
	{
		public UserEnhanceMentQuery(ulong enhancementHp, ulong enhancementatk)
		{
			EnhancementHp = enhancementHp;
			EnhancementAtk = enhancementatk;
		}

		public ulong EnhancementHp { get; set; }
		public ulong EnhancementAtk { get; set; }

		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	public class CharacterDataQuery
	{
		public CharacterDataQuery(string nickname, ulong jobcode, long hp, long atk, int index)
		{
			NickName = nickname;
			JobCode = jobcode;
			Hp = hp;
			Atk = atk;
			Index = index;
		}
		public int Index { get; set; }
		public string NickName { get; set; }
		public ulong JobCode { get; set; }
		public long Hp { get; set; }
		public long Atk { get; set; }
		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
	//캐릭터가 장착한 장비 정보
	public class CharacterEquipmentQuery
	{
		public CharacterEquipmentQuery(int characternum, ulong itemcode)
		{
			CharacterNum = characternum;
			ItemCode = itemcode;
		}
		public int CharacterNum { get; set; }
		public ulong ItemCode { get; set; }
		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}

	public class CurrencyQueryDTO
	{
		public CurrencyQueryDTO(long gold, long ancientCoin, long kingdomSupply, long arcaneKnowledge, long classFragment)
		{
			Gold = gold;
			AncientCoin = ancientCoin;
			KingdomSupply = kingdomSupply;
			ArcaneKnowledge = arcaneKnowledge;
			ClassFragment = classFragment;
		}

		public long Gold { get; set; } //기본적인 강화
		public long AncientCoin { get; set; } //유료재화
		public long KingdomSupply { get; set; } //장비뽑기
		public long ArcaneKnowledge { get; set; } //마탑스킬강화
		public long ClassFragment { get; set; } // 전직재화

		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}

	public class InventoryQueryDTO
	{
		public List<ItemCode> Items { get; set; }
		public string PasreToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}
