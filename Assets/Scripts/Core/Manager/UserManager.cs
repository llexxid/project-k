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
		}

		private void Init()
		{
			// Todo : 추후에는 서버로 부터, 유저 정보를 받아와야함. 
			// 지금은 테스트 
			UserData dummyUser = new UserData(0, "zx지존zx", eStage.Stage1, 1, 100, 100);
			CreateUser(dummyUser);

			// Todo : 추후에는 서버로 부터 플레이어 정보를 받고, 유저에서 Instantiate로 
			// 정보에 맞는 직업 Prefab을 생성해야함.
			GameObject obj1 = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
			GameObject obj2 = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
			GameObject obj3 = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

			Player p1;
			Player p2;
			Player p3;

			int i = 0;
			PlayerData dummyData = new PlayerData(i, $"Test{i}", 1, 10, 500);
			//Init Player
			p1 = obj1.GetComponent<Player>();
			p1.Init(dummyData);
			++i;
			p2 = obj2.GetComponent<Player>();
			p2.Init(dummyData);
			++i;
			p3 = obj3.GetComponent<Player>();
			p3.Init(dummyData);
			++i;

			_user.ConnectCharacters(p1);
			_user.ConnectCharacters(p2);
			_user.ConnectCharacters(p3);
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
	}

}

