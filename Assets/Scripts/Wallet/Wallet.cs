using Scripts.Users;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Wallets
{
    class Wallet
    {
		private User _user;
		private Dictionary<eCurrency, int> pocket = new Dictionary<eCurrency, int>();
		
		[SerializeField]
		private int totalCoins;

		public Wallet(User user, int coins, int ancientCoins)
		{
			_user = user;
			AddCoins(eCurrency.Gold, coins);
			AddCoins(eCurrency.AncientCoin, ancientCoins);
		}

		public int TotalCoins
		{
			get { return totalCoins; }
			set { totalCoins = value; }
		}

		public void AddCoins(eCurrency type, int amount)
		{
			if (pocket.ContainsKey(type))
			{
				pocket[type] += amount;
			}
			else
			{
				pocket.Add(type, amount);
			}
		}

		public bool TryGetAmount(eCurrency type, out int amount)
		{
			return pocket.TryGetValue(type, out amount);
		}

		// ── [장비 시스템 추가] User.CanAfford / TrySpendCoin에서 호용 ──────────
		/// <summary>
		/// 지정한 통화가 amount 이상 보유 중인지 확인한다.
		/// </summary>
		public bool CanAfford(eCurrency type, int amount)
		{
			return pocket.TryGetValue(type, out int current) && current >= amount;
		}

		/// <summary>
		/// 지정한 통화를 차감한다. 잔액 부족 시 false 반환 (차감하지 않음).
		/// </summary>
		public bool TrySpendCoins(eCurrency type, int amount)
		{
			if (!CanAfford(type, amount)) return false;
			pocket[type] -= amount;
			return true;
		}
		// ── [장비 시스템 추가 끝] ────────────────────────────────────

	}
}