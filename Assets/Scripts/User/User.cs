using Scripts.Core;
using System.Collections.Generic;
using Scripts.Wallets;

namespace Scripts.Users
{
	public class User
	{
		private Wallet _wallet;
		UserData _userData;

		public List<Player> _players;

		public User(UserData data)
		{
			_wallet = new Wallet(this, data._coin, data._coin);
			_userData = data;
			_players = new List<Player>();
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

		public void SetStage(eStage stage)
		{
			_userData._currentStage = stage;
		}

		public void GainCoin(eCurrency type, int amount)
		{
			_wallet.AddCoins(type, amount);
		}

		// ── [장비 시스템 추가] ────────────────────────────────────────
		/// <summary>
		/// 보유 골드가 amount 이상인지 확인한다.
		/// EquipmentManager.CanEnhance()에서 강화 가능 여부 판단에 사용.
		/// </summary>
		public bool CanAfford(eCurrency type, int amount)
		{
			return _wallet.CanAfford(type, amount);
		}

		/// <summary>
		/// 골드를 차감한다. 잔액 부족 시 false 반환 (차감하지 않음).
		/// EquipmentManager.TryEnhance()에서 강화 비용 차감에 사용.
		/// </summary>
		public bool TrySpendCoin(eCurrency type, int amount)
		{
			return _wallet.TrySpendCoins(type, amount);
		}
		// ── [장비 시스템 추가 끝] ───────────────────────────────────────
		public void ConnectCharacters(Player player)
		{
			_players.Add(player);
		}
	}
}
// User 스크립트에 지갑 정보, Player 3마리 연결, Player에서 User로 연결 로직 추가

