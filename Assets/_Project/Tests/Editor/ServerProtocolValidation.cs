using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.Tests
{
    [InitializeOnLoad]
    public static class ServerProtocolValidation
    {
        private const string RequestPath = "Library/ServerProtocolValidation.request";
        private const string ResultPath = "Docs/ServerDataAudit/Validation/UnityProtocolResults.json";
        static ServerProtocolValidation() => EditorApplication.update += CheckRequest;
        private static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            Run();
        }
        [MenuItem("KingdomIdle/Validation/Server Protocol")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            try
            {
                var passed = GameRpcAcceptance.Run();
                File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new
                { passed = true, unity = Application.unityVersion, utc = DateTimeOffset.UtcNow, mode = "isolated protocol samples; no live server", tests = passed }, Formatting.Indented));
                Debug.Log("SERVER PROTOCOL VALIDATION PASSED: " + passed.Length);
            }
            catch (Exception error)
            {
                File.WriteAllText(ResultPath, JsonConvert.SerializeObject(new
                { passed = false, unity = Application.unityVersion, utc = DateTimeOffset.UtcNow, error = error.ToString() }, Formatting.Indented));
                Debug.LogException(error);
                throw;
            }
        }
    }
}
