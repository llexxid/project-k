using Scripts.Users;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace Scripts.Wallets
{
    class Wallet
    {
		private User _user;
		private Dictionary<eCurrency, long> pocket;
		
		[SerializeField]
		private int totalCoins;

		public void SetOwner(User user, long golds, long ancientCoins, long kingdomSupplys, long arcaneKnowledges, long classfragments)
		{
			_user = user;
			pocket = new Dictionary<eCurrency, long>();

			AddCoins(eCurrency.Gold, golds);
			AddCoins(eCurrency.AncientCoin, ancientCoins);
			AddCoins(eCurrency.KingdomSupply, kingdomSupplys);
			AddCoins(eCurrency.ArcaneKnowledge, arcaneKnowledges);
			AddCoins(eCurrency.ClassFragment, classfragments);
		}

		public int TotalCoins
		{
			get { return totalCoins; }
			set { totalCoins = value; }
		}

		public void AddCoins(eCurrency type, long amount)
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

		public bool TryGetAmount(eCurrency type, out long amount)
		{
			return pocket.TryGetValue(type, out amount);
		}

		// ── [장비 시스템 추가] User.CanAfford / TrySpendCoin에서 호용 ──────────
		/// <summary>
		/// 지정한 통화가 amount 이상 보유 중인지 확인한다.
		/// </summary>
		public bool CanAfford(eCurrency type, long amount)
		{
			return pocket.TryGetValue(type, out long current) && current >= amount;
		}

		/// <summary>
		/// 지정한 통화를 차감한다. 잔액 부족 시 false 반환 (차감하지 않음).
		/// </summary>
		public bool TrySpendCoins(eCurrency type, long amount)
		{
			if (!CanAfford(type, amount)) return false;
			pocket[type] -= amount;
			return true;
		}
		// ── [장비 시스템 추가 끝] ────────────────────────────────────

	}
}