using KingdomIdle.Balance;
using Cysharp.Threading.Tasks.Triggers;
using KingdomIdle.KingdomArmy;
using Scripts.Core;
using Scripts.Server.DTO;
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
		List<Scripts.Server.DTO.ItemCode> _inventoryDataFromServer;
		List<JobTreeQuery> _jobTreeDataFromServer;
		List<Scripts.Server.DTO.SkillCode> _skillTreeDataFromServer;
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
		
		public void SetHuntResult(OnHuntResponseDTO res)
		{
            Debug.LogWarning("[Progression] Legacy hunt snapshot ignored; local authority owns this balance version.");
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
		public void SetWallet(CurrencyQueryDTO query)
		{
			_user.SetWallet(_user, query.Gold, query.AncientCoin, query.KingdomSupply, query.ArcaneKnowledge, query.ClassFragment);
		}
		public void SetCharacterData(List<CharacterDataQuery> query)
		{
			_characterDataFromServer = query;
		}
		public void SetInventoryData(List<Scripts.Server.DTO.ItemCode> inventory)
		{
			_inventoryDataFromServer = inventory;
		}
		public void SetJobTreeData(List<JobTreeQuery> jobTrees)
		{
			_jobTreeDataFromServer = jobTrees;
		}
		public void SetSkillTreeData(List<Scripts.Server.DTO.SkillCode> skillCodes)
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
                        for(int n = 0; n < amount; n++)
                        {
                            var saved = new EquipmentSave { Id = System.Guid.NewGuid().ToString("N"), Code = data.itemCode,
                                Level = System.Math.Min((int)item.GetItemEnchantCount(),data.maxEnhancementLevel) };
                            // 저장 복원은 오늘의 신규 획득이 아니다. 과거 승인 카운터는 별도로 보존한다.
                            if (!EquipmentManager.Grant(state,saved,true,false)) throw new System.InvalidOperationException("Inventory migration exceeds capacity; raw server account remains unchanged.");
                        }
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
                    for(int id=0;id<3;id++) if(state.MainClears.Contains(mageClears[id]) && !state.MageSkills.ContainsKey(id))
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

