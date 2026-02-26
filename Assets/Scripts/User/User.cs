using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Wallets;

namespace Scripts.Users
{
	public class User
	{
		public Wallet wallet;
		UserData _userData;

		public List<Player> players;

		public User(UserData data)
		{
			wallet = new Wallet(this, data._coin, data._coin);


		}

		public UserData GetData()
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

		public void ConnectCharacters(Player player)
		{
			players.Add(player);
		}
	}

}
// User 스크립트에 지갑 정보, Player 3마리 연결, Player에서 User로 연결 로직 추가

