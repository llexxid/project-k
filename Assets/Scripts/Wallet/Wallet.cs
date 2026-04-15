using Scripts.Users;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace Scripts.Wallets
{
    class Wallet
    {
		private User _user;
		private Dictionary<eCurrency, long> pocket = new Dictionary<eCurrency, long>();

		[SerializeField]
		private int totalCoins;

		// ── 재화 변경 전역 이벤트 ────────────────────────────────────
		// 모든 AddCoins / SetCoin / TrySpendCoins 호출 시 현재 통화 타입과 변경 후
		// 잔액을 전달한다. HUD 상단 재화 라벨 등 UI 가 구독해 즉시 갱신한다.
		// (이전엔 UITKUIManager 가 Update() 폴링으로만 갱신해 차감 반영이 한 박자 늦어
		//  "재화가 소모되지 않는 것처럼" 보이는 버그가 있었다.)
		public static event Action<eCurrency, long> OnAnyChanged;

		private void Notify(eCurrency type)
		{
			if (!pocket.TryGetValue(type, out long v)) v = 0;
			try { OnAnyChanged?.Invoke(type, v); }
			catch (Exception ex) { Debug.LogException(ex); }
		}

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

		public void SetCoin(eCurrency type, long amount)
		{
			pocket[type] = amount;
			Notify(type);
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
			Notify(type);
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
			Notify(type);
			return true;
		}
		// ── [장비 시스템 추가 끝] ────────────────────────────────────

	}
}