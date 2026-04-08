using Cysharp.Threading.Tasks.Triggers;
using Scripts.Core;
using Scripts.Server.DTO;
using Scripts.Users;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Core
{
	public class UserManager : MonoBehaviour
	{
		public static UserManager Instance;
		private User _user;

		//ForTest
		[SerializeField]
		GameObject playerPrefab;
		List<CharacterDataQuery> _characterDataFromServer;
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
		public void SetWallet(CurrencyQueryDTO query)
		{
			_user.SetWallet(_user, query.Gold, query.AncientCoin, query.KingdomSupply, query.ArcaneKnowledge, query.ClassFragment);
		}
		public void SetCharacterData(List<CharacterDataQuery> query)
		{
			_characterDataFromServer = query;
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
			++i;
			p2 = obj2.GetComponent<Player>();
			p2.Init(playerData1, _user);
			++i;
			p3 = obj3.GetComponent<Player>();
			p3.Init(playerData2, _user);
			++i;

			obj1.GetComponent<ChangeJob>().ApplyJobByIndex(0);
			obj2.GetComponent<ChangeJob>().ApplyJobByIndex(0);
			obj3.GetComponent<ChangeJob>().ApplyJobByIndex(0);

			_user.ConnectCharacters(p1);
			_user.ConnectCharacters(p2);
			_user.ConnectCharacters(p3);

			// 글로벌 강화 보너스 적용
			if (StatEnhanceManager.Instance != null)
				StatEnhanceManager.Instance.ApplyToAllPlayers();
		}
	}

}

