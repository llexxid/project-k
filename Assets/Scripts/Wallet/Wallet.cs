using Scripts.Users;
using System.Collections;
﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Wallets
{
	public class Wallet
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
			// �̹� �ִ� ��ȭ�� ������ ���ϰ�, ó���̸� ���� �߰�
			if (pocket.ContainsKey(type))
			{
				pocket[type] += amount;
			}
			else
			{
				pocket.Add(type, amount);
			}
		}

	}

    public void AddCoins(eCurrency type, int amount)
    {
        // 이미 있는 재화면 개수만 더하고, 처음이면 새로 추가
        if (wallet.ContainsKey(type))
        {
            wallet[type] += amount;
        }
        else
        {
            wallet.Add(type, amount);
        }

        /*
        foreach (var pair in wallet)
        {
            Debug.Log($"재화: {pair.Key}, 수량: {pair.Value}");
        }
        */
    }

    public bool TryGetAmount(eCurrency type, out int amount)
    {
        return wallet.TryGetValue(type, out amount);
    }

    void Start()
    {
        // Enum을 List로 변환
        var values = (eCurrency[])System.Enum.GetValues(typeof(eCurrency));
        currencies = new List<eCurrency>(values);
    }

    void Update()
    {

    }
}
