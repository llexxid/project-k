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

		public void SetWallet(User owner, long golds, long ancientCoins, long kingdomSupplys, long arcaneKnowledges, long classfragments)
		{
        LocalProgression.Execute("import-account-once", s => {
            if (s.Modules.ContainsKey("imported")) return false;
            s.Modules["imported"] = "legacy-server-snapshot";
            s.Modules["legacy-user"] = Newtonsoft.Json.JsonConvert.SerializeObject(_userData);
            s.Kills = System.Math.Max(0,_userData._killScore);
            long legacyStage = (long)_userData._currentStage;
            int legacyS = (int)((legacyStage >> 16) & 0xFFF), legacyW = (int)(legacyStage & 0xFFFF);
            if (legacyStage == (0x200000000L | ((long)legacyS << 16) | (uint)legacyW) && legacyS >= 1 && legacyS <= 3 && legacyW >= 1 && legacyW <= 11)
            {
                s.MainStage = legacyStage;
                // Reaching a main wave proves strictly earlier linear clears, not the current boss.
                for(int stage=1;stage<=legacyS;stage++) for(int wave=1;wave<=11;wave++)
                {
                    long id=0x200000000L | ((long)stage<<16) | (uint)wave;
                    if(id>=legacyStage) continue;
                    s.MainClears.Add(id);s.HighestMainClear=System.Math.Max(s.HighestMainClear,id);
                    if(wave==11) s.CycleBossStage=System.Math.Max(s.CycleBossStage,stage);
                }
                s.Modules["legacy-clear-basis"]="Strictly earlier waves inferred from reached position; no retroactive currency rewards.";
            }
            if(UnityEngine.PlayerPrefs.HasKey("reincarnation.progress.v1"))
                s.Modules["legacy-unassigned-reincarnation"]=UnityEngine.PlayerPrefs.GetString("reincarnation.progress.v1");
            s.Wallet[eCurrency.Gold] = System.Math.Max(0, golds);
            s.Wallet[eCurrency.AncientCoin] = System.Math.Max(0, ancientCoins);
            s.Wallet[eCurrency.KingdomSupply] = System.Math.Max(0, kingdomSupplys);
            s.Wallet[eCurrency.ArcaneKnowledge] = System.Math.Max(0, arcaneKnowledges);
            s.Wallet[eCurrency.ClassFragment] = System.Math.Max(0, classfragments);
            s.AccountLevel = BalanceMath.Clamp(_userData._level, 1, 200);
            s.Experience = System.Math.Max(0, _userData._exp);
            BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, 0);
            s.AttackLevel = (int)System.Math.Min(300UL, _userData._enchantATKCount);
            s.HealthLevel = (int)System.Math.Min(300UL, _userData._enchantHPCount);
            return true;
        });
    }
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

