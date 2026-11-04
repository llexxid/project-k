using System;
using Newtonsoft.Json.Linq;

namespace Scripts.Core
{
    public enum ClientAccessState { Allowed, Maintenance, UpdateRequired }

    public sealed class ClientAccessPolicy
    {
        public const string TitleDataKey = "ClientAccessPolicy";
        public string MinimumVersion { get; private set; }
        public bool Maintenance { get; private set; }
        public string Message { get; private set; }
        public string UpdateUrl { get; private set; }

        public static ClientAccessPolicy Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4096)
                throw new FormatException("Invalid client access policy size.");
            var data = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            string minimum = ReadString(data, "minimumVersion", true);
            ParseVersion(minimum);
            if (data["maintenance"]?.Type != JTokenType.Boolean)
                throw new FormatException("maintenance must be a boolean.");
            string message = ReadString(data, "message", false);
            if (message.Length > 200) throw new FormatException("message must be at most 200 characters.");
            string url = ReadString(data, "updateUrl", false);
            if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsWellFormedOriginalString()))
                throw new FormatException("updateUrl must be an absolute HTTPS URL without credentials.");
            return new ClientAccessPolicy
            {
                MinimumVersion = minimum, Maintenance = (bool)data["maintenance"], Message = message, UpdateUrl = url
            };
        }

        public ClientAccessState Evaluate(string installedVersion)
        {
            var installed = ParseVersion(installedVersion);
            if (Maintenance) return ClientAccessState.Maintenance;
            if (installed >= ParseVersion(MinimumVersion)) return ClientAccessState.Allowed;
            if (UpdateUrl.Length == 0) throw new FormatException("An update URL is required for an outdated client.");
            return ClientAccessState.UpdateRequired;
        }

        private static Version ParseVersion(string text)
        {
            if (!Version.TryParse(text, out var version) || version.Build < 0 || version.Revision >= 0 || version.ToString() != text)
                throw new FormatException("Version must use MAJOR.MINOR.PATCH.");
            return version;
        }

        private static string ReadString(JObject data, string key, bool required)
        {
            var value = data[key];
            if (value == null && !required) return string.Empty;
            if (value?.Type != JTokenType.String) throw new FormatException(key + " must be a string.");
            return (string)value;
        }
    }
}
