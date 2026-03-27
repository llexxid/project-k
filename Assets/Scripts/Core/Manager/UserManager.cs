using Cysharp.Threading.Tasks.Triggers;
using Scripts.Core;
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
			// Todo : ���Ŀ��� ������ ����, ���� ������ �޾ƿ;���. 
			// ������ �׽�Ʈ 
			UserData dummyUser = new UserData(0, "zx����zx", eStage.Stage1_1, 1, 0, 0);
			CreateUser(dummyUser);
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

		public int GetUserCoin()
		{
			return _user.GetCoin();
		}
		public int GetUserAncientCoin()
		{
			return _user.GetAncientCoin();
		}
		public eStage GetUserCurrentStage()
		{
			return _user.GetStage();
		}

		public void CreateUser(UserData data)
		{
			_user = new User(data);
		}
		
		public void CreateCharacter()
		{
			GameObject obj1 = Instantiate(playerPrefab, new Vector3(0, 1.4f, 0), Quaternion.identity);
			GameObject obj2 = Instantiate(playerPrefab, new Vector3(-1, 0, 0), Quaternion.identity);
			GameObject obj3 = Instantiate(playerPrefab, new Vector3(1, 0, 0), Quaternion.identity);

			Player p1;
			Player p2;
			Player p3;

			int i = 0;
			PlayerData dummyData = new PlayerData(i, $"Test{i}", 1, 10, 500);
			//Init Player
			p1 = obj1.GetComponent<Player>();
			p1.Init(dummyData, _user);
			++i;
			p2 = obj2.GetComponent<Player>();
			p2.Init(dummyData, _user);
			++i;
			p3 = obj3.GetComponent<Player>();
			p3.Init(dummyData, _user);
			++i;

			_user.ConnectCharacters(p1);
			_user.ConnectCharacters(p2);
			_user.ConnectCharacters(p3);
		}
	}

}

