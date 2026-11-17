using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Core.Utils
{
    /// <summary>진행에 필요한 실제 대기만 추적한다. 서버 요청이나 게임 상태는 변경하지 않는다.</summary>
    public static class GameplayLoading
    {
        public const float DelaySeconds = .3f;
        private static readonly List<Scope> Pending = new();

        static GameplayLoading() => LocalProgression.AccountChanged += Reset;

        public static Scope Begin(string operation, float notBefore = 0f, bool blockInput = false)
        {
            var scope = new Scope(operation, notBefore, blockInput);
            Pending.Add(scope);
            return scope;
        }

        public static bool IsInputBlocked
        {
            get
            {
                bool blocked = false;
                for (int i = Pending.Count - 1; i >= 0; i--)
                {
                    var scope = Pending[i];
                    if (!scope.IsCurrent) scope.Dispose();
                    else if (scope.BlocksInput) blocked = true;
                }
                return blocked;
            }
        }

        public static bool IsVisible
        {
            get
            {
                bool visible = false;
                for (int i = Pending.Count - 1; i >= 0; i--)
                {
                    var scope = Pending[i];
                    if (!scope.IsCurrent) scope.Dispose();
                    else if (!scope._paused && scope.ElapsedSeconds >= DelaySeconds) visible = true;
                }
                return visible;
            }
        }

        public static void Reset()
        {
            foreach (var scope in Pending) scope.IsDisposed = true;
            Pending.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            Reset();
            LocalProgression.AccountChanged -= Reset;
            LocalProgression.AccountChanged += Reset;
        }

        public sealed class Scope : IDisposable
        {
            public string Operation { get; }
            internal bool BlocksInput { get; }
            public bool IsDisposed { get; internal set; }
            private readonly NetworkManager _network;
            private readonly long _accountGeneration, _networkGeneration;
            private readonly float _notBefore;
            private float _activeSince, _elapsed;
            internal bool _paused;

            internal Scope(string operation, float notBefore, bool blockInput)
            {
                Operation = operation;
                BlocksInput = blockInput;
                _notBefore = _activeSince = Mathf.Max(Time.realtimeSinceStartup, notBefore);
                _network = NetworkManager.Instance;
                _accountGeneration = LocalProgression.AccountGeneration;
                _networkGeneration = _network != null ? _network.AccountGeneration : -1;
            }

            internal bool IsCurrent => !IsDisposed && _accountGeneration == LocalProgression.AccountGeneration &&
                ReferenceEquals(_network, NetworkManager.Instance) &&
                (_network == null || _networkGeneration == _network.AccountGeneration);

            public float ElapsedSeconds => _elapsed + (_paused || IsDisposed ? 0f : Mathf.Max(0f, Time.realtimeSinceStartup - _activeSince));

            public void Pause()
            {
                if (IsDisposed || _paused) return;
                _elapsed = ElapsedSeconds;
                _paused = true;
            }

            public void Resume()
            {
                if (IsDisposed || !_paused) return;
                _activeSince = Mathf.Max(Time.realtimeSinceStartup, _notBefore);
                _paused = false;
            }

            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;
                Pending.Remove(this);
            }
        }
    }
}
