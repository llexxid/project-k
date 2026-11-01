using KingdomIdle.Balance;
using Scripts.Core;
using System.Collections.Generic;
using Scripts.Wallets;

namespace Scripts.Users
{
	public class User
	{
		UserData _userData;

		private Wallet _wallet;
		internal Wallet Wallet => _wallet;
		public List<Player> _players;

		const long MAX_EXP = 150;
		
		public User()
		{
			_wallet = new Wallet();
			_players = new List<Player>();
		}

		// 서버에서 받은 플레이어 데이터는 UserManager.ApplyServerPlayerData가 매 로그인마다 플레이어 상태에 쓴다(예전 일회성 import 제거).
		public void SetUserData(UserData data)
		{
			_userData = data;
		}

		public UserData GetData()
		{
        var data = _userData; var s = LocalProgression.State;
        data._level = s.AccountLevel; data._exp = s.Experience; data._killScore = s.Kills;
        data._currentStage = (eStage)s.MainStage; return data;
    }

		public string GetNickName()
		{
			return _userData._nickname;
		}
		public int GetLevel()
		{
        return LocalProgression.State.AccountLevel;
    }
		//Todo Wallet으로 교체
		public long GetCoin()
		{
			long ret;
			_wallet.TryGetAmount(eCurrency.Gold, out ret);
			return ret;
		}
		public long GetAncientCoin()
		{
			long ret;
			_wallet.TryGetAmount(eCurrency.AncientCoin, out ret);
			return ret;
		}
		public eStage GetStage()
		{
        return (eStage)LocalProgression.State.MainStage;
    }

		public void SetStage(eStage stage)
		{
        if ((long)stage == LocalProgression.State.MainStage) return;
        LocalProgression.Execute("main-position", s => { s.MainStage = (long)stage; return true; });
    }

		public void SetCoin(eCurrency type, long amount)
		{
			_wallet.SetCoin(type, amount);
		}

		public void GainCoin(eCurrency type, long amount)
		{
			_wallet.AddCoins(type, amount);
		}
		public void GainArcaneKnowledge(long amount)
		{
			_wallet.AddCoins(eCurrency.ArcaneKnowledge, amount);
		}
		public void GainClassFragment(long amount)
		{
			_wallet.AddCoins(eCurrency.ClassFragment, amount);
		}

		public void SetLevel(int level)
		{
			_userData._level = level;
		}

		public void SetExp(long exp)
		{
			_userData._exp = exp;
		}

		public void GainExp(long exp)
		{
        if (exp <= 0) return;
        LocalProgression.Execute("account-exp", s => { BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, exp); return true; });
    }

		public void SetKillScore(long score)
		{
			_userData._killScore = score;
		}


		// ── [장비 시스템 추가] ────────────────────────────────────────
		/// <summary>
		/// 보유 골드가 amount 이상인지 확인한다.
		/// EquipmentManager.CanEnhance()에서 강화 가능 여부 판단에 사용.
		/// </summary>
		public bool CanAfford(eCurrency type, long amount)
		{
			return _wallet.CanAfford(type, amount);
		}

		/// <summary>
		/// 골드를 차감한다. 잔액 부족 시 false 반환 (차감하지 않음).
		/// EquipmentManager.TryEnhance()에서 강화 비용 차감에 사용.
		/// </summary>
		public bool TrySpendCoin(eCurrency type, long amount)
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

