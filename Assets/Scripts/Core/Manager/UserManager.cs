using Cysharp.Threading.Tasks.Triggers;
using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserManager : MonoBehaviour
{
	public static UserManager Instance;
	private DummyUser _user;

	public struct DummyUserData
	{
		public DummyUserData(long Id, string nickname, eStage stage, int level, int coin, int ancientcoin)
		{
			_Id = Id;
			_nickname = nickname;
			_currentStage = stage;
			_level = level;
			_coin = coin;
			_ancientCoin = ancientcoin;
		}

		long _Id;
		public string _nickname;
		public eStage _currentStage;

		public int _level;
		public int _coin;//Wallet정보들은 직렬화 되서 올테니
		public int _ancientCoin;

		//Todo : Token을 저장하고 있어야함.
	}
	//DummyUser
	public class DummyUser
	{
		public DummyUser(DummyUserData data)
		{
			_userData = data;
		}
		public DummyUserData GetData()
		{
			return _userData;
		}

		public string GetNickName()
		{
			return _userData._nickname;
		}

		public int GetLevel()
		{
			return _userData._level;
		}

		//Todo Wallet으로 교체
		public int GetCoin()
		{
			return _userData._coin;
		}
		public int GetAncientCoin()
		{
			return _userData._ancientCoin;
		}
		public eStage GetStage()
		{
			return _userData._currentStage;
		}

		DummyUserData _userData;
	}
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
		DummyUserData dummyUser = new DummyUserData(0,"zx지존zx", eStage.Stage1, 1, 0, 0);
		CreateUser(dummyUser);
	}

	public DummyUserData GetUserData()
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

	public eStage GetUserCurrentStage()
	{
		return _user.GetStage();
	}

	public void CreateUser(DummyUserData data)
	{
		_user = new DummyUser(data);
	}
}
