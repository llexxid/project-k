using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    public static class CombatUiPreparation
    {
        public static void Build()
        {
            F.Init();F.Catalog=PrefabGenUtil.GetOrCreateCatalog();
            SettingsRevisionBuilder.Generate();
            var tree=KingdomArmyPanelPrefabGens.GenerateJobChange();
            var card=ItemGens.GenerateJobCard();
            foreach(var prefab in new[]{tree,card})
            {
                string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
                try { UguiPolishPass.ApplyTo(root);PrefabUtility.SaveAsPrefabAsset(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            const string screen="Assets/UGUI/Prefabs/Screens/Screen_Main.prefab";
            var main=PrefabUtility.LoadPrefabContents(screen);
            try
            {
                var top=main.GetComponentsInChildren<Transform>(true).First(t=>t.name=="HudTop");
                AddTopBleed(top.gameObject);PrefabUtility.SaveAsPrefabAsset(main,screen);
            }
            finally { PrefabUtility.UnloadPrefabContents(main); }
            AssetDatabase.SaveAssets();
        }
        internal static void AddTopBleed(GameObject hud)
        {
            var bleed=hud.GetComponent<SafeAreaTopBleed>()??hud.AddComponent<SafeAreaTopBleed>();
            var existing=hud.transform.Find("TopInset");
            var image=existing!=null?existing.GetComponent<Image>():F.Box(hud.transform,"TopInset",UguiTheme.RusticBar,rounded:false);
            image.sprite=null;
            image.color=UguiTheme.RusticBar;image.raycastTarget=false;
            var rt=image.rectTransform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,0);rt.anchoredPosition=Vector2.zero;
            (image.GetComponent<LayoutElement>()??image.gameObject.AddComponent<LayoutElement>()).ignoreLayout=true;
            bleed.background=rt;
        }
    }
}
