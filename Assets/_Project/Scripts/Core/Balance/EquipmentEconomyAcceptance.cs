#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomIdle.Balance
{
    public static class EquipmentEconomyAcceptance
    {
        public static Dictionary<string, object> Run()
        {
            var manager = EquipmentManager.Instance ?? throw new InvalidOperationException("Equipment catalog must be loaded.");
            var checks = new List<string>();
            void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
            int code = manager.GetByRarity(eEquipmentRarity.Normal).First().itemCode;
            int rare = manager.GetByRarity(eEquipmentRarity.Rare).First().itemCode;
            int epic = manager.GetByRarity(eEquipmentRarity.Epic).First().itemCode;
            string account = "equipment-acceptance-" + Guid.NewGuid().ToString("N");
            LocalProgression.OpenTestAccount(account);
            bool Write(Action<ProgressionState> mutate) => LocalProgression.Execute("equipment-check", s => { mutate(s); return true; });
            Check(Write(s => { for (int i=0;i<405;i++) Check(EquipmentManager.Grant(s,new EquipmentSave{Id="item"+i,Code=code},true), "Overflow grant " + i); }),"Full bag rewards commit");
            Check(LocalProgression.State.Equipment.Count==300 && LocalProgression.State.PendingEquipment.Count==100 && LocalProgression.State.LegacyEquipment.Single().Count==5,"405 rewards preserve all three storage tiers");
            var bulk = EquipmentEconomy.Preview(_=>true);
            Check(bulk.Count==405 && bulk.Stones==405,"Bulk preview includes overflow without claiming");
            Check(Write(s=>EquipmentManager.Grant(s,new EquipmentSave{Id="late",Code=code},true)),"A new drop arrives while preview is open");
            Check(manager.Dismantle(bulk) && LocalProgression.Balance(eCurrency.EquipmentStone)==405 && LocalProgression.State.LegacyEquipment.Single().Count==1,"Preview removes only the original count, preserving later drops");
            Check(!manager.Dismantle(bulk) && LocalProgression.Balance(eCurrency.EquipmentStone)==405,"Reusing a confirmation cannot pay twice");
            Check(Write(s=> {
                s.LegacyEquipment.Clear(); s.Equipment.Add(new EquipmentSave{Id="locked",Code=code,Locked=true});
                s.Equipment.Add(new EquipmentSave{Id="equipped",Code=code,Player=0});
                s.Equipment.Add(new EquipmentSave{Id="enhanced",Code=code,Level=1});
                s.Equipment.Add(new EquipmentSave{Id="unknown",Code=int.MaxValue});
                s.Equipment.Add(new EquipmentSave{Id="free",Code=rare});
                s.PendingEquipment.Add(new EquipmentSave{Id="pending",Code=epic});
            }),"Protected fixture committed");
            bulk=EquipmentEconomy.Preview(_=>true);
            Check(bulk.Count==2 && bulk.Stones==16,"Bulk excludes locked, equipped, upgraded and unknown items");
            Check(Write(s=>s.PendingEquipment[0].Locked=true),"Protection changed during confirmation");
            Check(!manager.Dismantle(bulk) && LocalProgression.State.Equipment.Any(x=>x.Id=="free") && LocalProgression.Balance(eCurrency.EquipmentStone)==405,"Stale preview rolls back every removal and credit");
            Check(EquipmentEconomy.Preview(_=>true,"locked").Count==0 && EquipmentEconomy.Preview(_=>true,"equipped").Count==0,"Single dismantle also protects locked and equipped items");
            Check(EquipmentEconomy.Preview(_=>true,"enhanced").Stones==1,"Legacy enhancement does not invent paid stone refunds");
            Check(Write(s=> { s.Equipment.Clear();s.PendingEquipment.Clear();s.AutoDismantleMask=3;s.Wallet[eCurrency.EquipmentStone]=0; }),"Auto policy configured");
            Check(Write(s=> {
                EquipmentManager.Grant(s,new EquipmentSave{Id="auto-normal",Code=code},true);
                EquipmentManager.Grant(s,new EquipmentSave{Id="auto-rare",Code=rare},true);
                EquipmentManager.Grant(s,new EquipmentSave{Id="kept-epic",Code=epic},true);
                EquipmentManager.Grant(s,new EquipmentSave{Id="kept-locked",Code=code,Locked=true},true);
                EquipmentManager.Grant(s,new EquipmentSave{Id="kept-upgraded",Code=code,Level=1},true);
            }),"Future acquisition policy runs");
            Check(LocalProgression.Balance(eCurrency.EquipmentStone)==5 && LocalProgression.State.Equipment.Count==3,"Auto dismantles exactly selected future rarities and protects special items");
            Check(Write(s=> { s.AutoDismantleMask=0;s.Equipment.Clear();s.Equipment.Add(new EquipmentSave{Id="upgrade",Code=code});s.Wallet[eCurrency.EquipmentStone]=20; }),"Upgrade fixture committed");
            manager.RestoreEquipment(); var weapon=manager.Inventory.Items.Single();
            Check(EquipmentEconomy.EnhanceCost(weapon)==2 && manager.TryEnhance(weapon) && weapon.enhancementLevel==1,"Stone enhancement applies and refreshes live item");
            Check(LocalProgression.State.Equipment[0].EnhancementStonesSpent==2 && LocalProgression.Balance(eCurrency.EquipmentStone)==18,"Exact paid stone amount persists");
            Check(manager.Dismantle(weapon) && LocalProgression.Balance(eCurrency.EquipmentStone)==20,"Single upgraded dismantle returns base plus floored 80 percent");
            Check(EquipmentEconomy.Yield(new EquipmentSave{Code=code,EnhancementStonesSpent=long.MaxValue})>0,"Refund multiplication avoids intermediate long overflow");
            LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.State.AutoDismantleMask==0 && LocalProgression.Balance(eCurrency.EquipmentStone)==20,"Equipment currency and preferences survive reopen");
            var gacha=KingdomIdle.Gacha.GachaManager.Instance;
            if(gacha!=null)
            {
                Write(s=>{s.AutoDismantleMask=7;s.Wallet[eCurrency.AncientCoin]=500;s.Wallet[eCurrency.EquipmentStone]=0;s.EquipmentPity=39;});
                List<KingdomIdle.Gacha.GachaRewardEntry> result=null;string failure=null;
                var table=gacha.GetAllTables().First(x=>x.gachaType==KingdomIdle.Gacha.eGachaType.Equipment);
                gacha.TryPull(table,10,r=>result=r,e=>failure=e);
                Check(failure==null && result?.Count==10 && LocalProgression.Balance(eCurrency.AncientCoin)==0,"Auto dismantle gacha remains an atomic paid ten-pull");
                Check(result.All(x=>x.rewardType==KingdomIdle.Gacha.eGachaRewardType.Currency) && result[0].currency==eCurrency.EquipmentStone && result[0].amount==12,"Auto-dismantled pity epic is shown as twelve stones");
                Check(result.Where(x=>x.currency==eCurrency.EquipmentStone).Sum(x=>(long)x.amount)==LocalProgression.Balance(eCurrency.EquipmentStone) && LocalProgression.State.Equipment.Count==0,"Gacha presentation equals the exact converted payout");
            }
            return new Dictionary<string,object>{{"passed",checks.Count},{"checks",checks}};
        }
    }
}
#endif
