using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.Balance;
using Object=UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Isolated client regressions. Does not log in, save, or spend a user's inventory.</summary>
    public static class UguiRegressionChecks
    {
        private static readonly List<string> Passed=new();
        private static readonly List<Object> Owned=new();
        private static void Check(bool condition,string name)
        {if(!condition)throw new InvalidOperationException(name);Passed.Add(name);}
        private static void Field(object target,string name,object value)
        {target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
        private static T Model<T>() where T:Component
        {var go=new GameObject("UI regression fixture");go.SetActive(false);Owned.Add(go);return go.AddComponent<T>();}
        [MenuItem("KingdomIdle/UGUI/Run client regression checks")]
        public static void Run()
        {
            Passed.Clear();
            try
            {
                var stat=Model<StatEnhanceManager>();
                Check(stat.GetSingleCost(StatEnhanceManager.EnhanceType.Attack,0)==50,"Enhancement base cost unchanged");
                Check(stat.GetCost(StatEnhanceManager.EnhanceType.Attack,10)==1015,"Ten-level cost sums correctly");
                Check(stat.GetSingleCost(StatEnhanceManager.EnhanceType.Attack,10000)==int.MaxValue,"Large cost saturates without negative overflow");
                Check(stat.TryEnhanceEx(StatEnhanceManager.EnhanceType.Attack,0)==StatEnhanceManager.EnhanceResult.InvalidRequest,"Zero-count request rejected before spending");
                Check(stat.TryEnhanceEx(StatEnhanceManager.EnhanceType.CritRate)==StatEnhanceManager.EnhanceResult.InvalidRequest,"Unimplemented stat rejected");
                Check(MainScreenController.FormatChipAmount(9999)=="9,999"&&MainScreenController.FormatChipAmount(10000)=="1만","Currency unit boundary remains readable");

                var previousUserManager=Scripts.Core.UserManager.Instance;
                try
                {
                    Scripts.Core.UserManager.Instance=null;
                    Check(WalletLocator.FindAnyWallet()==null&&!Scripts.Core.EconomyBridge.TryGetAmount(eCurrency.Gold,out _),"No active account returns no wallet without scene scanning");
                    var users=Model<Scripts.Core.UserManager>();Scripts.Core.UserManager.Instance=users;
                    var firstUser=new Scripts.Users.User();Field(users,"_user",firstUser);
                    Scripts.Core.EconomyBridge.Add(eCurrency.Gold,3000000000L);
                    var firstWallet=WalletLocator.FindAnyWallet();
                    Check(WalletLocator.TryGetAmount(firstWallet,eCurrency.Gold,out long firstGold)&&firstGold==3000000000L,"Direct wallet reads preserve 64-bit balances");
                    Field(users,"_user",new Scripts.Users.User());Scripts.Core.EconomyBridge.Add(eCurrency.Gold,7);
                    Check(Scripts.Core.EconomyBridge.TryGetAmount(eCurrency.Gold,out long nextGold)&&nextGold==7&&WalletLocator.TryGetAmount(firstWallet,eCurrency.Gold,out firstGold)&&firstGold==3000000000L,"Account switch reads and writes only the current wallet");
                }
                finally{Scripts.Core.UserManager.Instance=previousUserManager;}

                var equipment=Model<EquipmentManager>();var inventory=new EquipmentInventory();Field(equipment,"_inventory",inventory);
                var data=ScriptableObject.CreateInstance<EquipmentData>();Owned.Add(data);data.enhanceMaterialCount=1;data.maxEnhancementLevel=10;
                var target=new EquipmentInstance(data);var equipped=new EquipmentInstance(data){equipmentPlayerIndex=0};inventory.Add(target);inventory.Add(equipped);
                Check(equipment.GetEnhanceMaterialCount(target)==0&&!equipment.CanEnhance(target),"Equipped copies excluded from material count");
                Check(equipment.TryEnhanceDetailed(target)==EquipmentManager.EnhancementResult.NotEnoughMaterials&&inventory.Items.Count==2&&target.enhancementLevel==0,"Rejected enhancement preserves items and level");
                Check(equipment.TryEnhanceDetailed(new EquipmentInstance(data))==EquipmentManager.EnhancementResult.InvalidItem,"Foreign target rejected");
                var material=new EquipmentInstance(data);inventory.Add(material);
                Check(equipment.GetEnhanceMaterialCount(target)==1&&equipment.CanEnhance(target),"Available copies agree with enhancement eligibility");
                var result=equipment.TryEnhanceDetailed(target);
                Check((result==EquipmentManager.EnhancementResult.Success||result==EquipmentManager.EnhancementResult.ChanceFailed)&&inventory.Items.Contains(target)&&inventory.Items.Contains(equipped)&&!inventory.Items.Contains(material),"Attempt consumes only eligible material");

                // UI contracts can be checked without opening or mutating a player's save.
                var token = new QuestClaimToken(1, 20001, "2026-09-17");
                Check(token != new QuestClaimToken(2, 20001, "2026-09-17") && token != new QuestClaimToken(1, 20001, "2026-09-18"),
                    "Quest buttons distinguish account and period");
                var rewards = new List<QuestRewardSnapshot> { new(eCurrency.Gold, 50) };
                var questRow = new QuestRowSnapshot(token, eQuestCategory.Daily, "Daily", "Kill", eQuestObjectiveType.MonsterKill,
                    0, 10, 10, QuestRowState.Claimable, false, 0, rewards);
                rewards.Clear();
                var rows = new List<QuestRowSnapshot> { questRow };
                var board = new QuestBoardSnapshot(1, 1, eQuestCategory.Daily, token.Period, 0, rows);
                rows.Clear();
                Check(board.Rows.Count == 1 && questRow.Rewards.Count == 1 && questRow.CanClaim,
                    "Quest UI snapshot collections cannot be changed through source lists");
                Check(!QuestBoardSnapshot.NotReady(eQuestCategory.Daily).IsReady &&
                    !new QuestClaimResult(QuestClaimStatus.SaveFailed, token).Succeeded,
                    "Quest UI distinguishes loading and failed reward saves");

                var root=new GameObject("Modal order fixture",typeof(RectTransform));Owned.Add(root);
                var lower=new GameObject("Lower");lower.transform.SetParent(root.transform);
                var upper=new GameObject("Upper");upper.transform.SetParent(root.transform);
                int closed=0;ModalBackHandler.Bind(upper,()=>{closed=2;upper.SetActive(false);});ModalBackHandler.Bind(lower,()=>{closed=1;lower.SetActive(false);});
                Check(ModalBackHandler.TryCloseTop()&&closed==2&&lower.activeSelf,"Back closes rendered top modal, not registration order");
                Check(ModalBackHandler.TryCloseTop()&&closed==1,"Second Back closes remaining modal");
                Debug.Log("[UI regressions] "+Passed.Count+" passed\n"+string.Join("\n",Passed));
            }
            finally{for(int i=Owned.Count-1;i>=0;i--)if(Owned[i]!=null)Object.DestroyImmediate(Owned[i]);Owned.Clear();}
        }
    }
}
