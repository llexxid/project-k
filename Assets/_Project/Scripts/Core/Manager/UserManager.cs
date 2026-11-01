using KingdomIdle.Balance;
using Cysharp.Threading.Tasks.Triggers;
using KingdomIdle.KingdomArmy;
using Scripts.Core;
using Protocol.Message.Dto;
using Scripts.Users;
using System;
using System.Collections;
using System.Collections.Generic;
using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEngine;

namespace Scripts.Core
{
	public class UserManager : MonoBehaviour
	{
		public static UserManager Instance;
		private User _user;
		internal User CurrentUser => _user;

		//ForTest
		[SerializeField]
		GameObject playerPrefab;
		List<CharacterDataQuery> _characterDataFromServer;
		List<Protocol.Message.Dto.ItemCode> _inventoryDataFromServer;
		List<JobTreeQuery> _jobTreeDataFromServer;
		List<Protocol.Message.Dto.SkillCode> _skillTreeDataFromServer;
		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
				Init();
				DontDestroyOnLoad(gameObject);
				return;
			}
			Destroy(gameObject);
			return;
		}

		private void Init()
		{
			_user = new User();

			// ── [Login 우회 폴백] 기본 UserData를 미리 세팅 ──
			// 로그인 없이 진입 시 GetUserCurrentStage() 등이 NRE를 일으키지 않도록.
			// 정식 로그인 흐름에서 SetUserData가 다시 호출되면 덮어쓰므로 영향 없음.
			_user.SetUserData(new UserData("Guest", 0, 0, eStage.Stage1_1, 1, 0, 0));
			// ── [Login 우회 폴백 끝] ──
		}

		private void Start()
		{
			if (StageManager.Instance == null)
			{
				CustomLogger.LogWarning("[UserManager] StageManager가 초기화되지 않았습니다");
			}
			if (StageManager.Instance != null) StageManager.Instance.OnStageEnter += HandleStageStarted;
		}

		private void OnDisable()
		{
			if (StageManager.Instance != null) StageManager.Instance.OnStageEnter -= HandleStageStarted;
		}

		private void HandleStageStarted(StageDefinition definition)
		{
			if (definition.Type != eStageType.Main)
				return;
			CustomLogger.Log($"[UserManager] 현재 유저 스테이지 갱신 : {definition.Id}");
			SetUserCurrentStage(definition.Id);
		}

		private void SetUserCurrentStage(eStage stage)
		{
			_user.SetStage(stage);
		}
		//오프라인 테스트용 유저세팅
		public void SetupOfflineUser(eStage startStage)
		{
			CreateUser(
				"OfflineGuest",
				startStage,
				exp: 0,
				monsterkill: 0,
				level: 1,
				enchantHp: 0,
				enchantAtk: 0
			);

			SetCharacterData(new List<CharacterDataQuery>
			{
				new CharacterDataQuery("Knight", 0, 50, 10, 0),
				new CharacterDataQuery("Archer", 0, 50, 10, 1),
				new CharacterDataQuery("Mage", 0, 50, 10, 2),
			});

			SetWallet(new CurrencyQueryDTO(
				gold: 0,
				ancientCoin: 0,
				kingdomSupply: 0,
				arcaneKnowledge: 0,
				classFragment: 0
			));
			
			SetInventoryData(null);
			SetSkillTreeData(null);
			SetJobTreeData(null);
		}
		
		public UserData GetUserData()
		{
			return _user.GetData();
		}
		public int GetUserLevel()
		{
			return _user.GetLevel();
		}
		public string GetUserName()
		{
			return _user.GetNickName();
		}

		/// <summary>UI 등 읽기 전용 소비자가 현재 연결된 캐릭터를 조회한다.</summary>
		public IReadOnlyList<Player> GetPlayers()
		{
			if (_user == null) return Array.Empty<Player>();
			return _user._players;
		}

		public long GetUserCoin()
		{
			return _user.GetCoin();
		}
		public long GetUserAncientCoin()
		{
			return _user.GetAncientCoin();
		}
		public eStage GetUserCurrentStage()
		{
			return _user.GetStage();
		}

		public void SetGold(long amount)
		{
			_user.SetCoin(eCurrency.Gold, amount);
		}

		public void GainAracneKnowledge(long amount)
		{
			_user.GainArcaneKnowledge(amount);
		}

		public void GainClassFragment(long amount)
		{
			_user.GainClassFragment(amount);
		}

		public void GainExp(long exp)
		{
			_user.GainExp(exp);
		}

		public void CreateUser(string name, eStage stage, UserDataQuery Userquery, UserEnhanceMentQuery EnchantQuery)
		{
			UserData userData = new UserData(
					name,
					Userquery.Exp,
					Userquery.MonsterKilled,
					stage,
					Userquery.Level,
					EnchantQuery.EnhancementHp,
					EnchantQuery.EnhancementAtk
				);
			_user.SetUserData(userData);
		}

		public void CreateUser(string name, eStage stage, long exp, long monsterkill, int level, ulong enchantHp, ulong enchantAtk)
		{
			UserData userData = new UserData(
				name,
				exp,
				monsterkill,
				stage,
				level,
				enchantHp,
				enchantAtk
				);
			_user.SetUserData(userData);
		}
		/// <summary>오프라인 테스트용 지갑 설정. 서버 로그인은 ApplyServerPlayerData가 처리한다.</summary>
		public void SetWallet(CurrencyQueryDTO query)
		{
			LocalProgression.Execute("set-wallet-snapshot", s => { ApplyCurrency(s, query); return true; });
		}

		/// <summary>서버 재화 절대값을 지갑에 덮어쓴다. 음수는 0으로 막고 로컬 잔액을 더하지 않는다.</summary>
		public static void ApplyCurrency(ProgressionState s, CurrencyQueryDTO currency)
		{
			if (currency == null) return;
			s.Wallet[eCurrency.Gold] = Math.Max(0, currency.Gold);
			s.Wallet[eCurrency.AncientCoin] = Math.Max(0, currency.AncientCoin);
			s.Wallet[eCurrency.KingdomSupply] = Math.Max(0, currency.KingdomSupply);
			s.Wallet[eCurrency.TrainingTome] = Math.Max(0, currency.TrainingTome);
			s.Wallet[eCurrency.ClassFragment] = Math.Max(0, currency.ClassFragment);
			s.Wallet[eCurrency.ArcaneKnowledge] = Math.Max(0, currency.ArcaneKnowledge);
			s.Wallet[eCurrency.Ruby] = Math.Max(0, currency.Ruby);
			s.Wallet[eCurrency.EquipmentStone] = Math.Max(0, currency.EquipmentStone);
			// 뽑기권도 서버 값이 진실이다(2026-11-01). 로컬에서만 받았던 뽑기권은 이 값으로 덮인다.
			s.Wallet[eCurrency.EquipmentTicket] = Math.Max(0, currency.EquipmentTicket);
			s.Wallet[eCurrency.MageTicket] = Math.Max(0, currency.MageTicket);
		}

		/// <summary>
		/// 광고 보상 응답(OnClaimAdReward, 상점 일일 광고·오프라인 2배 공통)의 레벨·경험치·재화 절대값을 쓴다.
		/// 처치 수는 바꾸지 않는다(서버도 세지 않는다). 레벨은 로그인과 같이 계정 상한으로 자른다.
		/// </summary>
		public static void ApplyAdReward(ProgressionState s, OnClaimAdRewardResponseDTO r)
		{
			if (r == null) return;
			s.AccountLevel = BalanceMath.Clamp(r.Level, 1, BalanceMath.AccountCap);
			s.Experience = Math.Max(0, r.Exp);
			BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, 0);
			ApplyCurrency(s, r.Currency);
		}

		/// <summary>
		/// 서버가 돌려준 장비 인벤토리를 플레이어 상태에 그대로 쓴다. 서버는 "코드+수량" 묶음으로 주므로 수량만큼 항목으로 펼치고,
		/// 같은 (Code, Level)인 기존 항목의 Id만 이어받아(열려 있는 상세·분해·수령 UI가 깨지지 않게) 값은 전부 서버 것으로 채운다.
		/// 예비 보관(ReserveStacks)은 (Code, Level)로 합친다. Validate를 깨는 값(용량·강화 단계)은 잘라내고 경고한다.
		/// </summary>
		public static void ApplyEquipment(ProgressionState s, EquipmentInventoryDTO inventory)
		{
			if (inventory == null) return;
			var previous = new List<EquipmentSave>(s.Equipment.Count + s.PendingEquipment.Count);
			previous.AddRange(s.Equipment);
			previous.AddRange(s.PendingEquipment);
			var used = new HashSet<string>();
			var equipped = new HashSet<int>();
			var items = new List<EquipmentSave>();
			var pending = new List<EquipmentSave>();
			ExpandStacks(inventory.Items, items, previous, used, equipped, EquipmentManager.Capacity, true, "Items");
			ExpandStacks(inventory.PendingItems, pending, previous, used, equipped, EquipmentManager.PendingCapacity, false, "PendingItems");
			var reserve = new List<LegacyEquipmentStack>();
			if (inventory.ReserveStacks != null)
			{
				foreach (var stack in inventory.ReserveStacks)
				{
					if (stack == null || stack.Count <= 0) continue;
					int code = (int)stack.ItemCode.GetItemCode();
					int level = ClampLevel(stack.ItemCode.GetItemEnchantCount());
					LegacyEquipmentStack existing = null;
					foreach (var entry in reserve) if (entry.Code == code && entry.Level == level) { existing = entry; break; }
					if (existing == null) reserve.Add(new LegacyEquipmentStack { Code = code, Level = level, Count = stack.Count, Locked = stack.IsLocked });
					else { existing.Count = (int)Math.Min(int.MaxValue, (long)existing.Count + stack.Count); existing.Locked |= stack.IsLocked; }
				}
			}
			s.Equipment = items;
			s.PendingEquipment = pending;
			s.LegacyEquipment = reserve;
			s.AutoDismantleMask = Math.Max(0, Math.Min(7, inventory.AutoDismantleRarityMask));
			// CreateCharacter의 옛 형식(Inventory 목록) 1회 import는 건너뛴다. 서버 값이 이미 들어왔다.
			s.Modules["inventory-imported"] = "server";
		}

		/// <summary>
		/// 서버가 돌려준 "바뀐 장비 묶음"만 플레이어 상태에 쓴다(출석 보상 수령). 묶음 수량은 지급 후 절대값이므로 같은 (Code, Level, 만료) 항목을
		/// 그 수량만큼 다시 펼치고 기존 Id를 이어받는다. 예비 보관은 (Code, Level) 묶음 수량을 서버 값으로 바꾼다. 응답에 없는 묶음은 건드리지 않는다.
		/// </summary>
		public static void ApplyEquipmentChanges(ProgressionState s, EquipmentChangesDTO changes)
		{
			if (changes == null) return;
			ReplaceStacks(s.Equipment, changes.Items, EquipmentManager.Capacity, true, "Items");
			ReplaceStacks(s.PendingEquipment, changes.PendingItems, EquipmentManager.PendingCapacity, false, "PendingItems");
			if (changes.ReserveStacks != null)
			{
				foreach (var stack in changes.ReserveStacks)
				{
					if (stack == null) continue;
					int code = (int)stack.ItemCode.GetItemCode();
					int level = ClampLevel(stack.ItemCode.GetItemEnchantCount());
					LegacyEquipmentStack existing = null;
					foreach (var entry in s.LegacyEquipment) if (entry.Code == code && entry.Level == level) { existing = entry; break; }
					if (existing == null) s.LegacyEquipment.Add(new LegacyEquipmentStack { Code = code, Level = level, Count = Math.Max(0, stack.Count), Locked = stack.IsLocked });
					else { existing.Count = Math.Max(0, stack.Count); existing.Locked = stack.IsLocked; }
				}
				s.LegacyEquipment.RemoveAll(entry => entry == null || entry.Count <= 0);
			}
			s.Modules["inventory-imported"] = "server";
		}

		// list에서 묶음과 같은 (Code, Level, 만료) 항목을 빼고 서버 수량만큼 다시 펼친다. 장착 인덱스는 남은 항목과 겹치지 않게 한다.
		private static void ReplaceStacks(List<EquipmentSave> list, List<EquipmentStackDTO> stacks, int capacity, bool allowEquip, string label)
		{
			if (list == null || stacks == null) return;
			foreach (var stack in stacks)
			{
				if (stack == null) continue;
				int code = (int)stack.ItemCode.GetItemCode();
				int level = ClampLevel(stack.ItemCode.GetItemEnchantCount());
				long expires = stack.ItemCode.ExpireTimesc > long.MaxValue ? long.MaxValue : (long)stack.ItemCode.ExpireTimesc;
				var previous = new List<EquipmentSave>();
				for (int i = list.Count - 1; i >= 0; i--)
				{
					var item = list[i];
					if (item != null && item.Code == code && item.Level == level && item.ExpiresUtc == expires) { previous.Insert(0, item); list.RemoveAt(i); }
				}
				var equipped = new HashSet<int>();
				foreach (var item in list) if (item?.Player is int index) equipped.Add(index);
				var expanded = new List<EquipmentSave>();
				ExpandStacks(new List<EquipmentStackDTO> { stack }, expanded, previous, new HashSet<string>(), equipped, Math.Max(0, capacity - list.Count), allowEquip, label);
				list.AddRange(expanded);
			}
		}

		private static void ExpandStacks(List<EquipmentStackDTO> stacks, List<EquipmentSave> target, List<EquipmentSave> previous,
			HashSet<string> used, HashSet<int> equipped, int capacity, bool allowEquip, string label)
		{
			if (stacks == null) return;
			foreach (var stack in stacks)
			{
				if (stack == null) continue;
				int code = (int)stack.ItemCode.GetItemCode();
				int level = ClampLevel(stack.ItemCode.GetItemEnchantCount());
				int count = (int)Math.Min((ulong)int.MaxValue, stack.ItemCode.GetItemAmount());
				var indexes = new List<int>();
				if (allowEquip && stack.EquippedCharacterIndexes != null)
				{
					foreach (int index in stack.EquippedCharacterIndexes)
					{
						if (index >= 0 && index <= 2 && equipped.Add(index)) indexes.Add(index);
						else Debug.LogWarning($"[PlayerData] 장비 {code} 장착 인덱스 {index}는 무시한다(중복 또는 범위 밖).");
					}
				}
				for (int i = 0; i < count; i++)
				{
					if (target.Count >= capacity)
					{
						Debug.LogWarning($"[PlayerData] 서버 {label}가 용량 {capacity}를 넘어 나머지는 잘라낸다.");
						return;
					}
					int? player = i < indexes.Count ? indexes[i] : (int?)null;
					EquipmentSave reuse = TakePrevious(previous, used, code, level, player);
					target.Add(new EquipmentSave
					{
						Id = reuse != null ? reuse.Id : Guid.NewGuid().ToString("N"),
						Code = code,
						Level = level,
						Player = player,
						Locked = stack.IsLocked,
						ExpiresUtc = stack.ItemCode.ExpireTimesc > long.MaxValue ? long.MaxValue : (long)stack.ItemCode.ExpireTimesc,
						EnhancementStonesSpent = Math.Max(0, stack.EnhancementStonesSpent)
					});
				}
			}
		}

		/// <summary>같은 (Code, Level)의 기존 항목을 하나 꺼낸다. 서버가 장착했다고 한 캐릭터에 이미 장착된 항목 → 장착 상태가 같은 항목 → 나머지 순서.</summary>
		private static EquipmentSave TakePrevious(List<EquipmentSave> previous, HashSet<string> used, int code, int level, int? player)
		{
			EquipmentSave fallback = null;
			foreach (var candidate in previous)
			{
				if (candidate == null || candidate.Code != code || candidate.Level != level || string.IsNullOrEmpty(candidate.Id) || used.Contains(candidate.Id)) continue;
				if (candidate.Player == player) { used.Add(candidate.Id); return candidate; }
				if (fallback == null) fallback = candidate;
			}
			if (fallback != null) used.Add(fallback.Id);
			return fallback;
		}

		private static int ClampLevel(ulong level) => (int)Math.Min(15UL, level);

		/// <summary>서버가 돌려준 마탑 상태를 플레이어 상태에 그대로 쓴다. 보유 스킬·슬롯을 통째로 교체한다(로컬 카탈로그에 없는 id 6·8은 건너뜀).</summary>
		public static void ApplyMageTower(ProgressionState s, MageTowerStateDTO state)
		{
			if (state == null) return;
			var skills = new Dictionary<int, MageSave>();
			if (state.Skills != null)
			{
				foreach (var skill in state.Skills)
				{
					if (skill == null) continue;
					if (!KingdomIdle.MageTower.MageSkillRules.IsAvailable(skill.SkillId))
					{
						Debug.LogWarning($"[PlayerData] 마탑 스킬 {skill.SkillId}은 클라이언트 카탈로그에 없어 건너뛴다.");
						continue;
					}
					int awaken = Math.Min(10, Math.Max(0, skill.AwakeningLevel));
					skills[skill.SkillId] = new MageSave
					{
						Enhance = Math.Min(100, Math.Max(0, skill.EnhancementLevel)),
						Awaken = awaken,
						Fragments = Math.Max(0, skill.Fragments),
						Spent = Math.Max(0, skill.KnowledgeSpent),
						BloomEnabled = skill.BloomEnabled && awaken >= KingdomIdle.MageTower.MageSkillRules.BloomAwakening
					};
				}
			}
			var slots = new[] { -1, -1, -1, -1, -1 };
			if (state.EquippedSkillIds != null)
			{
				for (int i = 0; i < slots.Length && i < state.EquippedSkillIds.Length; i++)
				{
					int id = state.EquippedSkillIds[i];
					if (id < 0 || !skills.ContainsKey(id) || Array.IndexOf(slots, id) >= 0) continue;
					slots[i] = id;
				}
			}
			s.MageSkills = skills;
			s.MageSlots = slots;
			s.Modules["mage-imported"] = "server";
		}

		/// <summary>로그·하네스용 마탑 요약 "보유 수/강화 합/각성 합/슬롯0.슬롯1.…". 공백·'|'를 넣지 않는다. 서버 응답과 상태를 같은 키로 적어 대조한다.</summary>
		public static string DescribeMage(MageTowerStateDTO state)
		{
			if (state == null) return "-";
			int count = 0, enhance = 0, awaken = 0;
			if (state.Skills != null)
			{
				foreach (var skill in state.Skills)
				{
					if (skill == null) continue;
					count++; enhance += skill.EnhancementLevel; awaken += skill.AwakeningLevel;
				}
			}
			return count + "/" + enhance + "/" + awaken + "/" + (state.EquippedSkillIds == null ? "-" : string.Join(".", state.EquippedSkillIds));
		}

		public static string DescribeMage(ProgressionState s)
		{
			int enhance = 0, awaken = 0;
			foreach (var pair in s.MageSkills) { enhance += pair.Value.Enhance; awaken += pair.Value.Awaken; }
			return s.MageSkills.Count + "/" + enhance + "/" + awaken + "/" + string.Join(".", s.MageSlots);
		}

		/// <summary>서버 가챠 천장(0..39)을 그대로 쓴다.</summary>
		public static void ApplyGachaState(ProgressionState s, GachaStateDTO state)
		{
			if (state == null) return;
			int pity = state.EquipmentPity;
			if (pity < 0 || pity > 39)
			{
				Debug.LogWarning($"[PlayerData] 서버 천장 {pity}가 범위 밖이라 0..39로 자른다.");
				pity = Math.Min(39, Math.Max(0, pity));
			}
			s.EquipmentPity = pity;
		}

		/// <summary>서버 환생 상태를 그대로 쓴다. 로그인·OnGetUserState·환생 응답이 같은 함수를 쓴다. 쿨다운 기준은 서버 LastUtc 하나다.</summary>
		public static void ApplyReincarnation(ProgressionState s, ReincarnationStateDTO state)
		{
			if (state == null) return;
			int level = state.Level;
			if (level < 0 || level > 300)
			{
				Debug.LogWarning($"[PlayerData] 서버 환생 레벨 {level}이 범위 밖이라 0..300으로 자른다.");
				level = Math.Min(300, Math.Max(0, level));
			}
			s.ReincarnationLevel = level;
			s.ReincarnationCount = (int)Math.Min((long)int.MaxValue, Math.Max(0L, state.Count));
			s.ReincarnationsToday = Math.Max(0, state.TodayCount);
			s.ReincarnationDay = state.Day ?? "";
			s.LastReincarnationUtc = Math.Max(0L, state.LastUtc);
			s.CycleStartedUtc = s.LastReincarnationUtc;
			s.PendingReincarnation = false;
		}

		/// <summary>서버 규칙과 같은 이번 주기 보스 클리어 수(현재 장 − 1). 환생 미리보기에만 쓴다.</summary>
		public static int CycleBossClears(long mainStage)
		{
			if (StageParser.GetStageType((eStage)mainStage) != eStageType.Main) return 0;
			return Math.Max(0, StageParser.GetStageNumber((eStage)mainStage) - 1);
		}

		/// <summary>서버 직업 코드(eJobCode) → 클라이언트 직업 이름(JobData.jobName). 모르는 코드는 Spearman.</summary>
		public static string JobName(ulong code) => (eJobCode)code switch
		{
			eJobCode.Mage => "Mage", eJobCode.Archer => "Archer", eJobCode.Knight => "Knight", eJobCode.Spearman => "Spearman",
			eJobCode.EliteMage => "Elite_Mage", eJobCode.EliteKnight => "Elite_Knight", eJobCode.EliteArcher => "Elite_Archer", _ => "Spearman"
		};
		/// <summary>클라이언트 직업 이름 → 서버 직업 코드. 모르는 이름은 Spearman(8).</summary>
		public static ulong JobCode(string name) => name switch
		{
			"Mage" => (ulong)eJobCode.Mage, "Archer" => (ulong)eJobCode.Archer, "Knight" => (ulong)eJobCode.Knight, "Spearman" => (ulong)eJobCode.Spearman,
			"Elite_Mage" => (ulong)eJobCode.EliteMage, "Elite_Knight" => (ulong)eJobCode.EliteKnight, "Elite_Archer" => (ulong)eJobCode.EliteArcher, _ => (ulong)eJobCode.Spearman
		};

		/// <summary>
		/// 서버가 돌려준 직업 상태를 플레이어 상태에 그대로 쓴다. JobTrees는 캐릭터별 해금 목록(UnlockedJobs)을 전체 교체하고,
		/// CharacterDatas는 캐릭터별 현재 직업(Jobs)을 덮어쓴다. 로그인·OnGetUserState·전직 응답이 같은 함수를 쓴다. null 인자는 건너뛴다.
		/// </summary>
		public static void ApplyJobs(ProgressionState s, List<CharacterDataQuery> characters, List<JobTreeQuery> trees)
		{
			if (trees != null)
			{
				var unlocked = new Dictionary<int, HashSet<string>>();
				foreach (var tree in trees)
				{
					if (tree == null || tree.Index < 0 || tree.Index > 2) continue;
					unlocked[tree.Index] = UnlockedNames(tree.JobList);
				}
				s.UnlockedJobs = unlocked;
				s.Modules["jobs-imported"] = "server";
			}
			if (characters != null)
			{
				foreach (var character in characters)
				{
					if (character == null || character.Index < 0 || character.Index > 2) continue;
					s.Jobs[character.Index] = JobName(character.JobCode);
				}
			}
		}

		/// <summary>전직 응답(OnGetJob·OnChangeJob)의 캐릭터 직업과 해금 트리를 쓴다.</summary>
		public static void ApplyJob(ProgressionState s, int index, ulong jobCode, List<JobTreeQuery> trees)
		{
			ApplyJobs(s, null, trees);
			if (index >= 0 && index <= 2) s.Jobs[index] = JobName(jobCode);
		}

		private static HashSet<string> UnlockedNames(List<ulong> codes)
		{
			var names = new HashSet<string>();
			if (codes != null) foreach (ulong code in codes) names.Add(JobName(code));
			if (names.Count == 0) names.Add("Spearman");
			return names;
		}

		/// <summary>캐릭터별 직업 코드 요약 "0:1/1:8/2:8". 하네스가 서버 값과 상태를 대조한다.</summary>
		public static string DescribeJobs(List<CharacterDataQuery> characters)
		{
			if (characters == null) return "-";
			var parts = new List<string>();
			foreach (var character in characters) if (character != null) parts.Add($"{character.Index}:{character.JobCode}");
			return parts.Count == 0 ? "-" : string.Join("/", parts);
		}
		public static string DescribeJobs(ProgressionState s)
		{
			var parts = new List<string>();
			for (int i = 0; i < 3; i++) if (s.Jobs.TryGetValue(i, out var name)) parts.Add($"{i}:{JobCode(name)}");
			return parts.Count == 0 ? "-" : string.Join("/", parts);
		}
		/// <summary>해금 직업 수 합계(캐릭터별 이름 집합의 크기, 상태와 같은 규칙).</summary>
		public static int CountUnlocked(List<JobTreeQuery> trees)
		{
			if (trees == null) return -1;
			int count = 0;
			foreach (var tree in trees) if (tree != null && tree.Index >= 0 && tree.Index <= 2) count += UnlockedNames(tree.JobList).Count;
			return count;
		}
		public static int CountUnlocked(ProgressionState s)
		{
			int count = 0;
			foreach (var pair in s.UnlockedJobs) count += pair.Value.Count;
			return count;
		}

		/// <summary>서버 인벤토리 수량 요약 "보유/대기/예비".</summary>
		public static string DescribeInventory(EquipmentInventoryDTO inventory)
		{
			if (inventory == null) return "-";
			long items = 0, pending = 0, reserve = 0;
			if (inventory.Items != null) foreach (var stack in inventory.Items) if (stack != null) items += (long)stack.ItemCode.GetItemAmount();
			if (inventory.PendingItems != null) foreach (var stack in inventory.PendingItems) if (stack != null) pending += (long)stack.ItemCode.GetItemAmount();
			if (inventory.ReserveStacks != null) foreach (var stack in inventory.ReserveStacks) if (stack != null) reserve += stack.Count;
			return $"{items}/{pending}/{reserve}";
		}

		/// <summary>플레이어 상태의 인벤토리 수량 요약 "보유/대기/예비".</summary>
		public static string DescribeInventory(ProgressionState s)
		{
			long reserve = 0;
			foreach (var stack in s.LegacyEquipment) reserve += stack.Count;
			return $"{s.Equipment.Count}/{s.PendingEquipment.Count}/{reserve}";
		}

		/// <summary>
		/// 서버에서 받은 플레이어 데이터(로그인·OnGetUserState 응답)를 플레이어 상태에 그대로 쓴다. 레벨·경험치·처치·재화·골드 강화·
		/// 현재 스테이지·클리어 기록·장비 인벤토리·마탑 상태·가챠 천장 모두 서버 값이며 로컬 계산을 더하지 않는다. 퀘스트 상태(Quests)는 QuestManager가 그대로 보관한다.
		/// </summary>
		public bool ApplyServerPlayerData(UserInitResponseDTO response)
		{
			if (response == null) return false;
			long stage = (long)response.StageState.CurrentStage;
			// 이번 주기 최고 도달은 서버 HighestStage이되 현재 스테이지보다 뒤로 가지 않는다. 영구 최고 클리어는 서버 MainClear다.
			long frontier = Math.Max(stage, (long)response.StageState.HighestStage);
			long highest = (long)response.StageState.MainClear;
			bool ok = LocalProgression.Execute("server-player-data", s =>
			{
				if (!s.Modules.ContainsKey("imported"))
				{
					// 첫 적용: 서버 값으로 덮기 전의 로컬 상태를 보존하고 기존 게이트(imported)를 유지한다.
					s.Modules["imported"] = "server-player-data";
					s.Modules["legacy-user"] = Newtonsoft.Json.JsonConvert.SerializeObject(new
					{
						s.AccountLevel, s.Experience, s.Kills, s.MainStage, s.HighestMainClear, s.AttackLevel, s.HealthLevel, s.Wallet
					});
					if (PlayerPrefs.HasKey("reincarnation.progress.v1"))
						s.Modules["legacy-unassigned-reincarnation"] = PlayerPrefs.GetString("reincarnation.progress.v1");
				}
				ApplyCurrency(s, response.Currency);
				ApplyGachaState(s, response.GachaState);
				ApplyEquipment(s, response.Equipment);
				ApplyMageTower(s, response.MageTowerState);
				ApplyJobs(s, response.CharacterDatas, response.JobTrees);
				ApplyReincarnation(s, response.Reincarnation);
				s.AccountLevel = BalanceMath.Clamp(response.Level, 1, BalanceMath.AccountCap);
				s.Experience = Math.Max(0, response.Exp);
				BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, 0);
				s.Kills = Math.Max(0, response.KillScore);
				s.AttackLevel = (int)Math.Min(BalanceMath.GoldCap, Math.Max(0, response.EnchantATKCount));
				s.HealthLevel = (int)Math.Min(BalanceMath.GoldCap, Math.Max(0, response.EnchantHPCount));
				if (StageParser.GetStageType((eStage)stage) == eStageType.Main)
				{
					s.MainStage = stage;
					// 환생 주기 파생값은 서버 규칙(현재 장 − 1)으로 맞춘다. 로컬 보스 클리어 카운트는 쓰지 않는다.
					s.CycleBossStage = CycleBossClears(stage);
					s.CycleFrontierStage = frontier;
				}
				// 클리어 판정은 HighestMainClear 기준이다. MainClears 집합은 서버 적용에서 더 채우지 않는다.
				s.HighestMainClear = Math.Max(0, highest);
				return true;
			});
			var state = LocalProgression.State;
			// 퀘스트 상태는 플레이어 상태 파일이 아니라 QuestManager가 보관한다(응답마다 전체 행이 와서 통째로 교체한다).
			if (ok) QuestManager.Receive(response.Quests, response is OnGetUserStateResponseDTO ? "state" : "login");
			// 출석판도 플레이어 상태 파일이 아니라 CheckInService가 보관한다. 우편함은 로그인할 때마다 다시 조회한다.
			if (ok) KingdomIdle.CheckIn.CheckInService.Receive(response.Attendance, response.ServerTimeUtc, response is OnGetUserStateResponseDTO ? "state" : "login");
			if (ok && !(response is OnGetUserStateResponseDTO)) Scripts.Core.GameManager.Instance?.Mailbox?.RequestRefresh("login");
			// 상점 광고 수령 기록도 출석판처럼 메모리에 둔다. 상점의 오늘 0/1·1/1 표시가 이 값을 쓴다.
			if (ok) ShopAdRewardService.Receive(response.AdRewards, response.ServerTimeUtc, response is OnGetUserStateResponseDTO ? "state" : "login");
			Debug.Log($"[PlayerData] server Level={response.Level} Exp={response.Exp} Kills={response.KillScore} Gold={response.Currency?.Gold} AncientCoin={response.Currency?.AncientCoin} ClassFragment={response.Currency?.ClassFragment} ArcaneKnowledge={response.Currency?.ArcaneKnowledge} ATK={response.EnchantATKCount} HP={response.EnchantHPCount} Stage=0x{stage:X} Frontier=0x{frontier:X} Highest=0x{highest:X} Inventory={DescribeInventory(response.Equipment)} Skills={response.MageTowerState?.Skills?.Count ?? -1} Mage={DescribeMage(response.MageTowerState)} Pity={response.GachaState?.EquipmentPity ?? -1} Quests={response.Quests?.Rows?.Count ?? -1} Reinc={response.Reincarnation?.Level ?? -1} Jobs={DescribeJobs(response.CharacterDatas)} Unlocked={CountUnlocked(response.JobTrees)} Board={KingdomIdle.CheckIn.CheckInService.Describe(response.Attendance)} Tickets={response.Currency?.EquipmentTicket}/{response.Currency?.MageTicket} Ads={ShopAdRewardService.Describe(response.AdRewards)}" +
				$" | state Level={state.AccountLevel} Exp={state.Experience} Kills={state.Kills} Gold={LocalProgression.Balance(eCurrency.Gold)} AncientCoin={LocalProgression.Balance(eCurrency.AncientCoin)} ClassFragment={LocalProgression.Balance(eCurrency.ClassFragment)} ArcaneKnowledge={LocalProgression.Balance(eCurrency.ArcaneKnowledge)} ATK={state.AttackLevel} HP={state.HealthLevel} Stage=0x{state.MainStage:X} Frontier=0x{state.CycleFrontierStage:X} Highest=0x{state.HighestMainClear:X} Inventory={DescribeInventory(state)} Skills={state.MageSkills.Count} Mage={DescribeMage(state)} Pity={state.EquipmentPity} Quests={QuestManager.ReceivedRowCount} Reinc={state.ReincarnationLevel} Jobs={DescribeJobs(state)} Unlocked={CountUnlocked(state)} Board={KingdomIdle.CheckIn.CheckInService.Describe()} Tickets={LocalProgression.Balance(eCurrency.EquipmentTicket)}/{LocalProgression.Balance(eCurrency.MageTicket)} Ads={ShopAdRewardService.Describe()}" +
				$" | applied={ok} revision={state.Revision} account={LocalProgression.AccountKey}");
			if (!ok) Debug.LogError("[PlayerData] 서버 플레이어 데이터 적용 실패: " + LocalProgression.LastError);
			return ok;
		}

		/// <summary>OnGetUserState로 플레이어 데이터를 다시 받아 로그인과 같은 규칙으로 쓴다. 가챠 응답이 유실됐을 때 재전송 대신 쓴다.</summary>
		public void RefreshFromServer(Action<bool> onDone)
		{
			var network = NetworkManager.Instance;
			if (network == null || !network.IsGameDataReady) { onDone?.Invoke(false); return; }
			network.Call<OnGetUserStateRequestDTO, OnGetUserStateResponseDTO>(new OnGetUserStateRequestDTO(),
				response =>
				{
					bool ok = ApplyServerPlayerData(response);
					EquipmentManager.Instance?.RestoreEquipment();
					KingdomIdle.MageTower.MageTowerManager.Instance?.NotifyCommitted();
					onDone?.Invoke(ok);
				},
				error =>
				{
					Debug.LogWarning("[PlayerData] OnGetUserState 실패: " + error?.ErrorMessage);
					onDone?.Invoke(false);
				});
		}
		public void SetCharacterData(List<CharacterDataQuery> query)
		{
			_characterDataFromServer = query;
		}
		/// <summary>서버가 저장한 캐릭터 직업 이름. 로그인·전직 응답이 플레이어 상태(Jobs)에 쓴 값을 먼저 보고, 없으면 로그인 응답의 캐릭터 데이터를 본다. 모르면 null.</summary>
		public string GetServerJobName(int characterIndex)
		{
			if (LocalProgression.IsReady && LocalProgression.State.Jobs.TryGetValue(characterIndex, out var saved)) return saved;
			if (_characterDataFromServer == null) return null;
			foreach (var character in _characterDataFromServer)
			{
				if (character == null || character.Index != characterIndex) continue;
				return JobName(character.JobCode);
			}
			return null;
		}
		public void SetInventoryData(List<Protocol.Message.Dto.ItemCode> inventory)
		{
			_inventoryDataFromServer = inventory;
		}
		public void SetJobTreeData(List<JobTreeQuery> jobTrees)
		{
			_jobTreeDataFromServer = jobTrees;
		}
		public void SetSkillTreeData(List<Protocol.Message.Dto.SkillCode> skillCodes)
		{
			_skillTreeDataFromServer = skillCodes;
		}
		/// <summary>해당 캐릭터 슬롯의 서버 JobTree(획득한 jobCode 목록)를 반환. 없으면 null.</summary>
		public List<ulong> GetJobTreeForCharacter(int characterIndex)
		{
			if (_jobTreeDataFromServer == null) return null;
			JobTreeQuery tree = _jobTreeDataFromServer.Find(t => t.Index == characterIndex);
			return tree?.JobList;
		}
		public void CreateCharacter()
		{
			// ── [Login 우회 폴백] 서버 캐릭터 데이터가 없으면 기본값 사용 ──
			// 로그인 없이 타이틀에서 화면 클릭만으로 진입한 경우 _characterDataFromServer가 null이라 NRE가 발생.
			// 정식 로그인 흐름에는 영향 없음.
			if (_characterDataFromServer == null || _characterDataFromServer.Count < 3)
			{
				_characterDataFromServer = new List<CharacterDataQuery>
				{
					new CharacterDataQuery("Knight",  0, 50, 10, 0),
					new CharacterDataQuery("Archer",  0, 50, 10, 1),
					new CharacterDataQuery("Mage",    0, 50, 10, 2),
				};
			}
			// ── [Login 우회 폴백 끝] ──

			_user._players.Clear();
            GameObject obj1 = Instantiate(playerPrefab, new Vector3(0, 1.4f, 0), Quaternion.identity);
			GameObject obj2 = Instantiate(playerPrefab, new Vector3(-1, 0, 0), Quaternion.identity);
			GameObject obj3 = Instantiate(playerPrefab, new Vector3(1, 0, 0), Quaternion.identity);

			Player p1;
			Player p2;
			Player p3;

			int i = 0;
			PlayerData playerData0 = new PlayerData(_characterDataFromServer[0].NickName,
				0,
				_characterDataFromServer[0].JobCode,
				_characterDataFromServer[0].Atk,
				_characterDataFromServer[0].Hp);

			PlayerData playerData1 = new PlayerData(_characterDataFromServer[1].NickName,
				1,
				_characterDataFromServer[1].JobCode,
				_characterDataFromServer[1].Atk,
				_characterDataFromServer[1].Hp);
			PlayerData playerData2 = new PlayerData(_characterDataFromServer[2].NickName,
				2,
				_characterDataFromServer[2].JobCode,
				_characterDataFromServer[2].Atk,
				_characterDataFromServer[2].Hp);
			//Init Player
			p1 = obj1.GetComponent<Player>();
			p1.Init(playerData0, _user);
			EquipmentManager.Instance.RegisterPlayer(p1);
			++i;
			p2 = obj2.GetComponent<Player>();
			p2.Init(playerData1, _user);
			EquipmentManager.Instance.RegisterPlayer(p2);
			++i;
			p3 = obj3.GetComponent<Player>();
			p3.Init(playerData2, _user);
			EquipmentManager.Instance.RegisterPlayer(p3);
			++i;

obj1.GetComponent<ChangeJob>().ChangeJobByCode(_characterDataFromServer[0].JobCode);
			obj2.GetComponent<ChangeJob>().ChangeJobByCode(_characterDataFromServer[1].JobCode);
			obj3.GetComponent<ChangeJob>().ChangeJobByCode(_characterDataFromServer[2].JobCode);

			_user.ConnectCharacters(p1);
			_user.ConnectCharacters(p2);
			_user.ConnectCharacters(p3);

			// 글로벌 강화 보너스 적용
			if (StatEnhanceManager.Instance != null)
				StatEnhanceManager.Instance.ApplyToAllPlayers();

			ChangeJob.RefreshPartyAura();
            foreach (var p in _user._players) p.RefillHP();
            EquipmentManager.Instance.RestoreEquipment();


            bool imported = LocalProgression.Execute("inventory-mage-import-once", state => {
                if (state.Modules.ContainsKey("inventory-imported") && state.Modules.ContainsKey("mage-imported")) return false;
                if (!state.Modules.ContainsKey("inventory-imported"))
                {
                    state.Modules["legacy-inventory"] = Newtonsoft.Json.JsonConvert.SerializeObject(_inventoryDataFromServer);
                    if (_inventoryDataFromServer != null) foreach(var item in _inventoryDataFromServer)
                    {
                        var data = EquipmentManager.Instance.GetData((int)item.GetItemCode());
                        if (data == null) { state.Modules["legacy-unresolved-inventory"]="Unknown item codes remain in legacy-inventory for server migration."; continue; }
                        int amount = (int)item.GetItemAmount();
                        // 저장 복원은 획득 이벤트 없이 보유 장비와 초과 수량만 복구한다.
                        EquipmentManager.ImportLegacy(state, data.itemCode,
                            System.Math.Min((int)item.GetItemEnchantCount(),data.maxEnhancementLevel), amount);
                    }
                    state.Modules["inventory-imported"] = "1";
                }
                if (!state.Modules.ContainsKey("mage-imported"))
                {
                    state.Modules["legacy-mage"] = Newtonsoft.Json.JsonConvert.SerializeObject(_skillTreeDataFromServer);
                    if (_skillTreeDataFromServer != null) foreach(var skill in _skillTreeDataFromServer)
                    {
                        int id = (int)skill.GetSkillId();
                        if (KingdomIdle.MageTower.MageTowerManager.Instance?.GetSkillById(id) == null) continue;
                        state.MageSkills[id] = new MageSave { Enhance = System.Math.Min(100,(int)skill.GetEnchantCount()), Awaken = System.Math.Min(10,(int)skill.GetAwakeningCount()), Fragments = (int)skill.GetSkillAmount(), Spent = 0 };
                    }
                    long[] mageClears={0x200010005,0x200020003,0x200030003};
                    for(int id=0;id<3;id++) if(state.HighestMainClear >= mageClears[id] && !state.MageSkills.ContainsKey(id))
                    { state.MageSkills[id]=new MageSave();int slot=System.Array.IndexOf(state.MageSlots,-1);if(slot>=0)state.MageSlots[slot]=id; }
                    state.Modules["mage-imported"] = "1";
                }
                return true;
            });
            if (!LocalProgression.State.Modules.ContainsKey("inventory-imported") || !LocalProgression.State.Modules.ContainsKey("mage-imported"))
            {
                KingdomIdle.UGUI.UIManager.Instance?.ShowToast("기존 장비·마법 데이터를 이관하지 못했습니다. 원본은 보존되어 있습니다. 재접속 후 다시 시도해 주세요.");
                throw new InvalidOperationException("Progression migration incomplete; battle start withheld.");
            }
            EquipmentManager.Instance.RestoreEquipment();
            KingdomIdle.MageTower.MageTowerManager.Instance?.NotifyCommitted();

		}
	}

}

