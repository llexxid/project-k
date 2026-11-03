#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor.Build;

namespace KingdomIdle.EditorTools
{
    internal sealed class AlphaAndroidManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string unityLibrary)
        {
            var manifest = Path.Combine(unityLibrary, "..", "launcher", "src", "main", "AndroidManifest.xml");
            var xml = XDocument.Load(manifest);
            var application = xml.Root?.Element("application") ??
                              throw new BuildFailedException("Cannot configure alpha permissions: Android launcher has no application.");
            XNamespace android = "http://schemas.android.com/apk/res/android";
            XNamespace tools = "http://schemas.android.com/tools";
            // Declare this on the app itself; an unversioned library manifest can imply legacy storage/phone permissions.
            const string notifications = "android.permission.POST_NOTIFICATIONS";
            bool addPermission = !xml.Root.Elements("uses-permission").Any(p => (string)p.Attribute(android + "name") == notifications);
            if (addPermission)
                application.AddBeforeSelf(new XElement("uses-permission", new XAttribute(android + "name", notifications)));

            // The Play Games SDK normally starts before Unity. Authentication explicitly starts it after agreement.
            const string providerName = "com.google.android.gms.games.provider.PlayGamesInitProvider";
            var node = application.Elements("provider").FirstOrDefault(p => (string)p.Attribute(android + "name") == providerName);
            if (!addPermission && (string)node?.Attribute(tools + "node") == "remove") return;
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
