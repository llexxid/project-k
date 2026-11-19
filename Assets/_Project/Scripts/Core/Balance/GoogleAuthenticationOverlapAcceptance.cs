#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.Auth;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.Balance
{
    /// <summary>Drives the real SDK Future poller over one shared mutable result. No native, account or network calls.</summary>
    public static class GoogleAuthenticationOverlapAcceptance
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        public static Dictionary<string, object> Run()
        {
            if (LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null)
                throw new InvalidOperationException("Run GoogleAuthenticationOverlapAcceptance without an account or production managers.");
            var cases = new List<Result>();
            Collect(cases, "ActualSdkSharedFutureLateResultBaseline", Baseline);
            Collect(cases, "ProcessWideProviderPendingGuard", Guard);
            Collect(cases, "GuardFaultCancelAndFactoryFailure", TerminalFailures);
            Collect(cases, "ProductionGuardCallsite", Callsite);
            var failures = cases.SelectMany(value => value.Failures.Select(label => value.Name + ": " + label)).ToArray();
            return new Dictionary<string, object>
            {
                ["passed"] = failures.Length == 0 ? cases.Sum(value => value.Checks.Count) : 0,
                ["success"] = failures.Length == 0, ["failures"] = failures,
                ["cases"] = cases.Select(value => new Dictionary<string, object>
                { ["name"] = value.Name, ["checks"] = value.Checks.ToArray(), ["failures"] = value.Failures.ToArray() }).ToArray()
            };
        }

        private static void Baseline(Result result)
        {
            using (var provider = new SharedProvider())
            {
                var first = provider.Start();
                var expired = Timeout(first);
                result.Check(IsTimeout(expired) && !first.IsCompleted, "Wrapper timeout leaves the first actual SDK Future poller pending");
                var second = provider.Start();
                result.Check(provider.Starts == 2 && provider.Waiters == 2, "Two SDK Future wrappers read the same native-style mutable result slot");
                var oldResult = new GoogleSignInUser();
                provider.Complete(oldResult, GoogleSignInStatusCode.Success);
                result.Check(ReferenceEquals(first.GetAwaiter().GetResult(), oldResult) && ReferenceEquals(second.GetAwaiter().GetResult(), oldResult),
                    "Baseline: one late old result completes both actual SDK pollers, including the newer attempt");
                result.Check(IsTimeout(expired) && provider.Waiters == 0, "The old timeout wrapper remains failed after both SDK pollers drain");
            }
        }

        private static void Guard(Result result)
        {
            var begin = BeginMethod();
            result.Check(begin != null, "Authentication has one private process-wide Google SDK start guard");
            if (begin == null) return;
            using (var state = new GuardState())
            using (var provider = new SharedProvider())
            {
                var first = Start(begin, provider.Start);
                var expired = Timeout(first);
                result.Check(IsTimeout(expired) && !first.IsCompleted, "A timed-out wrapper preserves the still-running provider task");
                // The static guard must outlive either Authentication instance and an account-generation change.
                var otherAuthenticator = new Authentication(null);
                result.Check(otherAuthenticator != null && state.Pending.IsStatic, "Tracker is independent of Authentication object identity");
                bool rejected = false;
                try { Start(begin, provider.Start); }
                catch (GooglePendingMarker) { rejected = true; }
                result.Check(rejected && provider.Starts == 1 && provider.Waiters == 1,
                    "A retry while the old provider is pending is rejected before a second SDK start");
                var discarded = new GoogleSignInUser();
                provider.Complete(discarded, GoogleSignInStatusCode.Success);
                result.Check(IsTimeout(expired) && provider.Waiters == 0, "Late old success drains the SDK poller without reviving the expired login wrapper");
                var retry = Start(begin, provider.Start);
                result.Check(provider.Starts == 2 && !retry.IsCompleted, "Only an explicit retry after provider completion starts a fresh SDK attempt");
                var fresh = new GoogleSignInUser();
                provider.Complete(fresh, GoogleSignInStatusCode.Success);
                result.Check(ReferenceEquals(retry.GetAwaiter().GetResult(), fresh) && !ReferenceEquals(retry.Result, discarded),
                    "The explicit retry receives its fresh result instead of the drained old result");
                result.Check(state.Pending.GetValue(null) == null, "Completed provider ownership is released without account work");
            }
        }

        private static void TerminalFailures(Result result)
        {
            var begin = BeginMethod();
            result.Check(begin != null, "Terminal-state checks use the production Google start guard");
            if (begin == null) return;
            using (var state = new GuardState())
            using (var provider = new SharedProvider())
            {
                var canceled = Start(begin, provider.Start);
                provider.Complete(null, GoogleSignInStatusCode.Canceled);
                result.Check(canceled.IsCanceled && state.Pending.GetValue(null) == null, "Provider cancellation releases the guard");
                var failed = Start(begin, provider.Start);
                provider.Complete(null, GoogleSignInStatusCode.NetworkError);
                result.Check(failed.IsFaulted && state.Pending.GetValue(null) == null, "Provider failure releases the guard and is observed without logging payloads");
                bool factoryFailed = false;
                try { Start(begin, () => throw new InvalidOperationException()); }
                catch (InvalidOperationException) { factoryFailed = true; }
                result.Check(factoryFailed && state.Pending.GetValue(null) == null, "A synchronous SDK start failure cannot strand provider ownership");
                var completed = Task.FromResult(new GoogleSignInUser());
                result.Check(ReferenceEquals(Start(begin, () => completed), completed) && state.Pending.GetValue(null) == null,
                    "An already completed provider result returns unchanged and leaves no pending guard");
            }
        }

        private static void Callsite(Result result)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/Server/Auth/Authentication.cs");
            result.Check(script != null && script.text.Contains("BeginGoogleSignIn(GoogleSignIn.DefaultInstance.SignIn)"),
                "The production interactive call passes the SDK start delegate into the guard before invocation");
        }

        private static MethodInfo BeginMethod() => typeof(Authentication).GetMethod("BeginGoogleSignIn", PrivateStatic, null,
            new[] { typeof(Func<Task<GoogleSignInUser>>) }, null);

        private static Task<GoogleSignInUser> Start(MethodInfo begin, Func<Task<GoogleSignInUser>> factory)
        {
            try { return (Task<GoogleSignInUser>)begin.Invoke(null, new object[] { factory }); }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException?.GetType().Name == "GoogleSignInPendingException") throw new GooglePendingMarker();
                throw exception.InnerException ?? exception;
            }
        }

        private static Task<GoogleSignInUser> Timeout(Task<GoogleSignInUser> provider)
        {
            var method = typeof(Authentication).GetMethods(PrivateStatic).Single(value => value.Name == "WithTimeout" && value.IsGenericMethodDefinition);
            return (Task<GoogleSignInUser>)method.MakeGenericMethod(typeof(GoogleSignInUser)).Invoke(null, new object[] { provider, 0d });
        }

        private static bool IsTimeout(Task<GoogleSignInUser> task)
        {
            if (!task.IsCompleted) return false;
            try { task.GetAwaiter().GetResult(); return false; }
            catch (TimeoutException) { return true; }
        }

        private static void Collect(List<Result> cases, string name, Action<Result> action)
        {
            var result = new Result(name); cases.Add(result);
            try { action(result); }
            catch (Exception exception) { result.Check(false, "Fixture exception type only: " + exception.GetType().Name); }
        }

        private sealed class GuardState : IDisposable
        {
            public readonly FieldInfo Pending = typeof(Authentication).GetField("_pendingGoogleSignIn", PrivateStatic);
            private readonly object _gate = typeof(Authentication).GetField("GoogleSignInGate", PrivateStatic)?.GetValue(null);
            private readonly object _saved;
            public GuardState()
            {
                if (Pending == null || _gate == null) throw new InvalidOperationException("Google ownership tracker is unavailable");
                lock (_gate)
                {
                    _saved = Pending.GetValue(null);
                    if (_saved is Task task && !task.IsCompleted) throw new InvalidOperationException("A production provider task is still pending");
                    Pending.SetValue(null, null);
                }
            }
            public void Dispose() { lock (_gate) Pending.SetValue(null, _saved); }
        }

        // This interface and Future ctor/poller are internal to the unchanged SDK. This Editor-only file
        // shares Assembly-CSharp with it, so the real poller can run without constructing a native impl.
        private sealed class SharedApi : FutureAPIImpl<GoogleSignInUser>
        {
            public bool Pending { get; set; }
            public GoogleSignInStatusCode Status { get; set; }
            public GoogleSignInUser Result { get; set; }
        }

        private sealed class SharedProvider : IDisposable
        {
            private readonly SharedApi _slot = new SharedApi();
            private readonly List<IEnumerator> _waiters = new List<IEnumerator>();
            public int Starts { get; private set; }
            public int Waiters => _waiters.Count;
            public Task<GoogleSignInUser> Start()
            {
                Starts++; _slot.Pending = true; _slot.Result = null;
                var completion = new TaskCompletionSource<GoogleSignInUser>();
                var routine = new Future<GoogleSignInUser>(_slot).WaitForResult(completion);
                if (!routine.MoveNext() || !(routine.Current is WaitUntil)) throw new InvalidOperationException("SDK poller contract changed");
                _waiters.Add(routine);
                return completion.Task;
            }
            public void Complete(GoogleSignInUser user, GoogleSignInStatusCode status)
            {
                _slot.Result = user; _slot.Status = status; _slot.Pending = false;
                foreach (var routine in _waiters.ToArray())
                {
                    if (((WaitUntil)routine.Current).keepWaiting || routine.MoveNext()) throw new InvalidOperationException("SDK poller did not drain");
                    (routine as IDisposable)?.Dispose(); _waiters.Remove(routine);
                }
            }
            public void Dispose()
            {
                if (_waiters.Count != 0) Complete(null, GoogleSignInStatusCode.Canceled);
            }
        }

        private sealed class GooglePendingMarker : Exception { }
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
#endif
