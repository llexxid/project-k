using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google;
using KingdomIdle.Balance;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.Auth;
using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>SDK setter/timeout policy only. No native sign-in, service, account, preferences or network work.</summary>
    public static class GoogleAuthenticationAcceptance
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        public static Dictionary<string, object> Run()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || LocalProgression.IsReady ||
                NetworkManager.Instance != null || UserManager.Instance != null)
                throw new InvalidOperationException("Run GoogleAuthenticationAcceptance with Android selected and no account or production managers.");

            var cases = new List<Result>();
            Collect(cases, "ActualSdkRejectsReplacementAfterFailedAttempt", VerifySdkRule);
            Collect(cases, "ProductionConfigurationRetryAndSignOut", VerifyProductionConfiguration);
            Collect(cases, "InteractiveAndNetworkTimeoutBudgets", VerifyTimeouts);
            Collect(cases, "ProductionCallsiteBoundaries", VerifyCallsites);
            var failures = cases.SelectMany(value => value.Failures.Select(failure => value.Name + ": " + failure)).ToArray();
            return new Dictionary<string, object>
            {
                ["passed"] = failures.Length == 0 ? cases.Sum(value => value.Checks.Count) : 0,
                ["success"] = failures.Length == 0,
                ["failures"] = failures,
                ["cases"] = cases.Select(value => new Dictionary<string, object>
                {
                    ["name"] = value.Name, ["checks"] = value.Checks.ToArray(), ["failures"] = value.Failures.ToArray()
                }).ToArray()
            };
        }

        private static void VerifySdkRule(Result result)
        {
            using (var fixture = new SdkFixture())
            {
                var first = new GoogleSignInConfiguration();
                GoogleSignIn.Configuration = first;
                fixture.InstallFakeInstance();
                bool failed = false;
                try { Task.FromException<int>(new TimeoutException()).GetAwaiter().GetResult(); }
                catch (TimeoutException) { failed = true; }
                result.Check(failed, "A completed provider-failure task is observed without sign-in/native work");
                bool rejected = false;
                try { GoogleSignIn.Configuration = new GoogleSignInConfiguration(); }
                catch (GoogleSignIn.SignInException exception) { rejected = exception.Status == GoogleSignInStatusCode.DeveloperError; }
                result.Check(rejected, "Actual SDK setter rejects a different configuration after the failed first attempt created its instance");
                result.Check(ReferenceEquals(GoogleSignIn.Configuration, first), "Rejected replacement preserves the original SDK configuration");
                GoogleSignIn.Configuration = first;
                result.Check(ReferenceEquals(GoogleSignIn.Configuration, first), "Actual SDK setter permits reusing the exact existing reference");
            }
        }

        private static void VerifyProductionConfiguration(Result result)
        {
            MethodInfo configure = typeof(Authentication).GetMethod("ConfigureGoogleSignIn", StaticPrivate, null, Type.EmptyTypes, null);
            result.Check(configure != null, "Authentication exposes its private production configuration step to reflection");
            if (configure == null) return;

            using (var fixture = new SdkFixture())
            {
                configure.Invoke(null, null);
                var first = GoogleSignIn.Configuration;
                result.Check(first != null, "A fresh attempt configures the actual SDK setter");
                if (first == null) return;
                CheckFields(result, first);
                fixture.InstallFakeInstance();
                for (int retry = 0; retry < 3; retry++)
                {
                    configure.Invoke(null, null);
                    result.Check(ReferenceEquals(GoogleSignIn.Configuration, first), "Retry " + (retry + 1) + " retains the same SDK configuration reference");
                }
                CheckFields(result, first);

                // The SDK clears Configuration before invoking its implementation. The deliberately null
                // implementation stops the fake here, so no native SignOut is called.
                bool nullImplementationStopped = false;
                try { fixture.Fake.SignOut(); }
                catch (NullReferenceException) { nullImplementationStopped = true; }
                result.Check(nullImplementationStopped && GoogleSignIn.Configuration == null,
                    "Actual SDK SignOut clears Configuration before the fake stops at its null implementation");
                configure.Invoke(null, null);
                var restored = GoogleSignIn.Configuration;
                result.Check(restored != null, "A later attempt restores configuration after SDK SignOut cleared it");
                if (restored != null)
                {
                    CheckFields(result, restored);
                    configure.Invoke(null, null);
                    result.Check(ReferenceEquals(GoogleSignIn.Configuration, restored), "The post-SignOut retry also preserves its configuration reference");
                }
            }
        }

        private static void CheckFields(Result result, GoogleSignInConfiguration configuration)
        {
            var webClient = typeof(Authentication).GetField("WebClientId", StaticPrivate)?.GetRawConstantValue() as string;
            result.Check(!string.IsNullOrEmpty(webClient) && configuration.WebClientId == webClient,
                "Web client configuration matches the existing production constant without disclosing it");
            result.Check(!configuration.UseGameSignIn && configuration.RequestIdToken && configuration.RequestAuthCode &&
                configuration.RequestEmail && configuration.RequestProfile, "Existing Google request fields remain enabled unchanged");
            result.Check(!configuration.ForceTokenRefresh && !configuration.HidePopups && configuration.AccountName == null &&
                configuration.AdditionalScopes == null, "No refresh, account, scope or popup policy is introduced");
        }

        private static void VerifyTimeouts(Result result)
        {
            var auth = typeof(Authentication);
            var network = auth.GetField("AuthenticationTimeoutSeconds", StaticPrivate);
            var interactive = auth.GetField("GoogleInteractiveTimeoutSeconds", StaticPrivate);
            result.Check(network != null && Convert.ToDouble(network.GetRawConstantValue()) == 30d, "Network authentication budget remains 30 seconds");
            result.Check(interactive != null && Convert.ToDouble(interactive.GetRawConstantValue()) == 180d, "Interactive Google account/consent budget is finite at 180 seconds");
            var method = auth.GetMethods(StaticPrivate).SingleOrDefault(value => value.Name == "WithTimeout" && value.IsGenericMethodDefinition);
            var parameters = method?.GetParameters();
            bool explicitBudget = parameters != null && parameters.Length == 2 && parameters[1].ParameterType == typeof(double) &&
                parameters[1].HasDefaultValue && Convert.ToDouble(parameters[1].DefaultValue) == 30d;
            result.Check(explicitBudget, "WithTimeout supports a separate budget while its omitted argument still defaults to 30 seconds");
            if (!explicitBudget) return;

            var closed = method.MakeGenericMethod(typeof(int));
            var completed = (Task<int>)closed.Invoke(null, new object[] { Task.FromResult(7), Type.Missing });
            result.Check(completed.GetAwaiter().GetResult() == 7, "Default network timeout preserves an already completed result");
            var source = new TaskCompletionSource<int>();
            var timedOut = (Task<int>)closed.Invoke(null, new object[] { source.Task, 0d });
            bool timeout = false;
            try { timedOut.GetAwaiter().GetResult(); }
            catch (TimeoutException) { timeout = true; }
            result.Check(timeout && !source.Task.IsCompleted, "An explicit elapsed budget times out without completing or canceling provider work");
            source.SetResult(9);
            result.Check(timedOut.IsFaulted && timedOut.Exception?.InnerException is TimeoutException,
                "A late provider result cannot turn a timed-out attempt into a successful wrapper result");
        }

        private static void VerifyCallsites(Result result)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/Server/Auth/Authentication.cs");
            result.Check(script != null, "Current production Authentication source is available for callsite policy checks");
            if (script == null) return;
            var code = script.text;
            result.Check(Regex.IsMatch(code, @"ConfigureGoogleSignIn\(\);\s*var google = await WithTimeout\(BeginGoogleSignIn\(GoogleSignIn.DefaultInstance.SignIn\),\s*GoogleInteractiveTimeoutSeconds\);"),
                "Google interactive sign-in alone selects its 180-second budget through the production configuration step");
            result.Check(code.Contains("var login = await WithTimeout(completion.Task);"), "PlayFab login keeps the default network timeout");
            result.Check(code.Contains("await WithTimeout(GameTermsConsent.StoreAsync(client, acceptedAt));"), "Terms storage keeps the default network timeout");
            result.Check(Regex.IsMatch(code, @"var google = await WithTimeout\([^;]+;\s*if \(!IsCurrent\(generation\)\) return;"),
                "The account generation guard remains immediately after interactive completion");
        }

        private static void Collect(List<Result> cases, string name, Action<Result> action)
        {
            var result = new Result(name); cases.Add(result);
            try { action(result); }
            catch (Exception exception)
            {
                if (exception is TargetInvocationException invocation && invocation.InnerException != null) exception = invocation.InnerException;
                result.Check(false, "Fixture exception type only: " + exception.GetType().Name);
            }
        }

        private sealed class SdkFixture : IDisposable
        {
            private readonly FieldInfo _instance = typeof(GoogleSignIn).GetField("theInstance", StaticPrivate);
            private readonly FieldInfo _configuration = typeof(GoogleSignIn).GetField("theConfiguration", StaticPrivate);
            private readonly object _savedInstance;
            private readonly object _savedConfiguration;
            public GoogleSignIn Fake { get; private set; }

            public SdkFixture()
            {
                if (_instance == null || _configuration == null) throw new InvalidOperationException("SDK fixture static fields are unavailable");
                _savedInstance = _instance.GetValue(null);
                _savedConfiguration = _configuration.GetValue(null);
                _instance.SetValue(null, null);
                _configuration.SetValue(null, null);
            }

            public void InstallFakeInstance()
            {
                var constructor = typeof(GoogleSignIn).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(value => value.GetParameters().Length == 1);
                Fake = (GoogleSignIn)constructor.Invoke(new object[] { null });
                _instance.SetValue(null, Fake);
            }

            public void Dispose()
            {
                _configuration.SetValue(null, _savedConfiguration);
                _instance.SetValue(null, _savedInstance);
                if (!ReferenceEquals(_configuration.GetValue(null), _savedConfiguration) || !ReferenceEquals(_instance.GetValue(null), _savedInstance))
                    throw new InvalidOperationException("SDK fixture failed to restore original static references");
            }
        }

        private sealed class Result
        {
            public readonly string Name;
            public readonly List<string> Checks = new List<string>();
            public readonly List<string> Failures = new List<string>();
            public Result(string name) { Name = name; }
            public void Check(bool passed, string label) { (passed ? Checks : Failures).Add(label); }
        }
    }
}
