#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor.Build;

namespace KingdomIdle.EditorTools
{
    // The installed Play Games v2 SDK normally starts before Unity, ahead of the title's age and agreement screens.
    internal sealed class DeferPlayGamesStartup : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string unityLibrary)
        {
            var manifest = Path.Combine(unityLibrary, "..", "launcher", "src", "main", "AndroidManifest.xml");
            var xml = XDocument.Load(manifest);
            var application = xml.Root?.Element("application") ??
                              throw new BuildFailedException("Cannot apply the Play Games startup policy: Android launcher has no application.");
            XNamespace android = "http://schemas.android.com/apk/res/android";
            XNamespace tools = "http://schemas.android.com/tools";
            const string providerName = "com.google.android.gms.games.provider.PlayGamesInitProvider";
            var node = application.Elements("provider").FirstOrDefault(p => (string)p.Attribute(android + "name") == providerName);
            if ((string)node?.Attribute(tools + "node") == "remove") return;
            if (node == null)
            {
                node = new XElement("provider", new XAttribute(android + "name", providerName));
                application.Add(node);
            }
            xml.Root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
            node.SetAttributeValue(tools + "node", "remove");
            xml.Save(manifest);
        }
    }
}
#endif
