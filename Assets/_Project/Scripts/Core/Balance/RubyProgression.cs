using System;
namespace KingdomIdle.Balance
{
    public static class RubyProgression
    {
        public static bool Unlocked => LocalProgression.State.MainClears.Contains(0x200020005);
        public static bool Enhance(bool experience)
        {
            if (!Unlocked) return false;
            return LocalProgression.Execute("ruby-enhance", s => {
                if (experience && s.AccountLevel >= 200) return false;
                int level = experience ? s.RubyExpLevel : s.RubyGoldLevel;
                long? cost = BalanceMath.RubyCost(level);
                if (!cost.HasValue || !LocalProgression.Spend(s, eCurrency.Ruby, cost.Value)) return false;
                if (experience) { s.RubyExpLevel++; s.RubyExpSpent = checked(s.RubyExpSpent + cost.Value); s.BestRubyExpLevel = Math.Max(s.BestRubyExpLevel, s.RubyExpLevel); }
                else { s.RubyGoldLevel++; s.RubyGoldSpent = checked(s.RubyGoldSpent + cost.Value); s.BestRubyGoldLevel = Math.Max(s.BestRubyGoldLevel, s.RubyGoldLevel); }
                return true;
            });
        }
        public static bool Reset(bool experience)
        {
if (!ChangeJob.CanQueueChange) return false;
            return LocalProgression.Execute("ruby-reset-request", s => {
                if (s.RubyResetDay == LocalProgression.KstDay || s.PendingRubyReset != 0 || (experience ? s.RubyExpLevel : s.RubyGoldLevel) == 0) return false;
                s.PendingRubyReset = experience ? 2 : 1;
                if (Scripts.Core.Manager.StageManager.Instance?.CurrentRunState != Scripts.Core.Manager.eStageRunState.Running) CommitReset(s);
                return true;
            });
    }
        public static void CommitReset(ProgressionState s)
        {
            if (s.PendingRubyReset == 0) return;
            bool experience = s.PendingRubyReset == 2; s.PendingRubyReset = 0;
            if (s.RubyResetDay == LocalProgression.KstDay) return;
            long spent = experience ? s.RubyExpSpent : s.RubyGoldSpent;
            LocalProgression.Credit(s,eCurrency.Ruby,BalanceMath.Floor(spent * .8m));
            if (experience) { s.RubyExpLevel=0;s.RubyExpSpent=0; } else { s.RubyGoldLevel=0;s.RubyGoldSpent=0; }
            s.RubyResetDay=LocalProgression.KstDay;
        }
    }
}
