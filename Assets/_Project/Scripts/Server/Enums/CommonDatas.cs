using IdleKingdomFunction.Common.Dto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IdleKingdomFunction.Common.enums
{
	public struct DropResult
	{
		public long Gold { get; set; }
		public long AncientCoin { get; set; }
	}

	public enum eDropTable : long
	{
		SKELETON_DROPTABLE = 1,
		ORC_DROPTABLE = 2,
		BANDIT_ARCHER_DROPTABLE = 3,
		BANDIT_LEADER_DROPTABLE = 4,
		BANDIT_SHIELD_DROPTABLE = 5,
		BANDIT_KING_DROPTABLE = 6,
		AMORED_ORC_DROPTABLE = 7,
		ORC_KING_DROPTABLE = 8,
	}

	public enum eErrorCode
	{
		eDuplicated_Request_InitUser = 1,
		eDuplicated_SessionId = 2,
		eRequest_NoExistSession = 3,
		eDuplicated_Nickname = 4,
		eInvalied_MonsterID = 5,
		eTooMuchMonsterCount = 6,
		eTooMuchGachaCount = 7,
		eNotEnoughAncientCoin = 8,
		eNotEnoughGold = 9,

		eNotExistItemCodeInput = 10,
		eNotEnoughItemCount = 11,
		eNotEnoughArcaneKnowledge = 12,
		
		eNotExistSkillCodeInput = 13,
		eNotEnoughSkillCount = 14,

		eEnchantMaxLevelSKILL = 15,
		eEnchantMaxLevelItem = 16,

		eInvalideJobCode = 17,
		eInvalideCharacterIndex = 18,
		eNotEnoughClassFragment = 19,

		eRequestTooFreqentReward = 20,
		eRequestNotExistJobCodeInJobTree = 21,

		// 호출 문맥에서 Player ID를 얻지 못한 경우.
		InvalidPlayerId = 1001,
		// 호출 문맥에 해당 API의 요청 DTO가 없는 경우.
		InvalidFunctionArgument = 1002,
        // 지원하지 않는 프로토콜 버전.
        UnsupportedProtocolVersion = 1003,
        // 강화 횟수/스테이지/보상 순서 등 비즈니스 입력 오류를 구분한다.
        InvalidEnchantCount = 1004,
        InvalidStage = 1005,
        InvalidRewardSequence = 1006,
        InvalidLoot = 1007,
        ItemQuantityLimit = 1008,
        SkillQuantityLimit = 1009,
        AwakeningLevelLimit = 1010,
        JobAlreadyUnlocked = 1011,
        PlayerNotInitialized = 1012,
        CharacterDataMissing = 1013,
        // 저장소가 오류 결과를 반환한 경우(PlayFab 오류 응답, Redis 쓰기 미확인 등).
        StorageFailure = 1014,
        // 저장된 플레이어 데이터나 기준 데이터가 손상되었거나 누락된 경우.
        InvalidStoredData = 1015,
        // 호출 문맥에서 PlayFab 타이틀 ID를 얻지 못한 경우.
        InvalidTitleId = 1016,
	}

	public enum eConst
	{
		//사냥 요청 1회(3초 주기)의 총 처치 수가 이 값 이상이면 거절한다.
		//추후 전투력에 따른 초당 처치 수 데이터를 모아 플레이어별 검증으로 바꾼다.
		HUNT_KILL_LIMIT = 150,
		//직전 사냥 보상을 수락한 뒤 이 시간(ms) 이하로 다시 호출하면 처리하지 않는다.
		HUNT_MIN_INTERVAL_MS = 2000,
		MAX_EXP = 150,
		LAST_WAVE = 10,

		GACHA_PER_ANCIENTCOIN = 50,
		ENCHANT_AMOUNT = 3,

		SKILL_ENCHANT_MAX = 9,
		ITEM_ENCHANT_MAX = 9,

		GET_JOB_FRAGMENT = 40,
	}

	namespace Scripts.Core
	{
		public enum eMonsterType : ulong
		{
			MON_SKELETON = 4294967297,
			MON_GOBLIN = 4294967298,
			MON_ORC = 4294967299,
			MON_BANDIT_ARCHER = 4294967300,
			MON_BANDIT_LEADER = 4294967301,
			MON_BANDIT_SHIELD = 4294967302,
			BANDIT_KING = 4294967303,
			AMORED_ORC = 4294967304,
			ORC = 4294967305,
			ORC_KING = 4294967306,
		}
	}

	public enum eStage : long
	{
		Stage1 = 8590000128,
		Stage1_1 = 8590000129,
		Stage1_2 = 8590000130,
		Stage1_3 = 8590000131,
		Stage1_4 = 8590000132,
		Stage1_5 = 8590000133,
		Stage1_6 = 8590000134,
		Stage1_7 = 8590000135,
		Stage1_8 = 8590000136,
		Stage1_9 = 8590000137,
		Stage1_10 = 8590000138,
		Stage1_11 = 8590000139,
		Stage2 = 8590065664,
		Stage2_1 = 8590065665,
		Stage2_2 = 8590065666,
		Stage2_3 = 8590065667,
		Stage2_4 = 8590065668,
		Stage2_5 = 8590065669,
		Stage2_6 = 8590065670,
		Stage2_7 = 8590065671,
		Stage2_8 = 8590065672,
		Stage2_9 = 8590065673,
		Stage2_10 = 8590065674,
		Stage2_11 = 8590065675,
	}

	public enum eJobCode
	{
		Mage = 1,
		Archer = 2,
		Knight = 4,
		Spearman = 8,
		EliteMage = 16,
		EliteKnight = 32,
		EliteArcher = 64
	}

	public enum eSkillCode
	{ 
		LIGTENING = 0,
		ICESPIKE = 1,
		FIRETORNADO = 2,
	}
	public enum eEquipmentItemNumber
	{
		WoodenGrateSword = 0,
		BasicGreatSword,
		BasicWoodenGreatSword ,

		WoodenBow,
		BasicBow,
		BasicLongBow,

		WoodenStaff,
		StoneStaff,
		StealStaff,
		//Rare
		GoldenGreatSword,
		GoldenStealAxe,
		GoldenBow,
		GoldenStealBow,
		GoldenStaff,
		GoldenStealStaff,

		//Epic
		Excalibur,
		Evil_Tracer,
		Air,
	}

	public enum eEquipmentRarity
	{ 
		Normal = 0,
		Rare = 1,
		Epic = 2,
	}


	public struct MonsterInfo
	{
		public MonsterInfo(string name, ulong exp, long baseHp, ulong baseAtk, double baseMoveSpeed, double baseAtkSpeed, long dropTable)
		{
			_name = name;
			_exp = exp;
			_baseHp = baseHp;
			_baseAtk = baseAtk;
			_baseMoveSpeed = baseMoveSpeed;
			_baseAtkSpeed = baseAtkSpeed;
			_dropTableNumber = dropTable;
		}

		public readonly string _name;
		public readonly ulong _exp;
		public readonly long _baseHp;
		public readonly ulong _baseAtk;

		public readonly double _baseMoveSpeed;
		public readonly double _baseAtkSpeed;

		public readonly long _dropTableNumber;
	}
	public enum eCurrency
	{
		Gold,
		AncientCoin,
		KingdomSupply,
		TrainingTome,
		ClassFragment,
		ArcaneKnowledge
	}
}
