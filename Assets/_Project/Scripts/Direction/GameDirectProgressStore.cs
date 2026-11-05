using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using KingdomIdle.Balance;
using Protocol.Message.Dto;
using Scripts.Core.Manager;
using UnityEngine;

namespace Direction
{
    /// <summary>서버 안내 기록의 실행 복사본. 완료와 명시적 건너뛰기는 서버 IsFinished로 함께 저장한다.</summary>
    public sealed class GameDirectProgress
    {
        public int Version = 1;
        public List<string> ConfirmedSteps = new();
        public bool Completed;
        public bool Skipped;

        /// <summary>저장 전 원본과 분리된 후보 상태를 만든다. 실패한 저장을 메모리 완료 상태로 공개하지 않는다.</summary>
        public GameDirectProgress Copy() => new()
        {
            Version = Version, ConfirmedSteps = new List<string>(ConfirmedSteps),
            Completed = Completed, Skipped = Skipped
        };
    }

    /// <summary>로그인에서 받은 계정별 서버 진행만 읽는다. 옛 로컬 진행과 실습·지급 기록은 변경하지 않는다.</summary>
    public sealed class GameDirectProgressStore
    {
        public const string NotReadyMessage = "튜토리얼 서버 연동을 준비 중입니다. 진행 정보를 받을 때 안내를 이어갑니다.";
        private static Dictionary<string, TutorialProgressDTO> _records;
        private static NetworkManager _network;
        private static long _accountGeneration = -1, _networkGeneration = -1;
        public static bool Available => _records != null && IsCurrent(_accountGeneration, _networkGeneration);
        public static bool IsCurrent(long account, long network) => account == LocalProgression.AccountGeneration &&
            _network != null && _network == NetworkManager.Instance && network == _network.AccountGeneration && _network.IsGameDataReady;

        /// <summary>null은 미배포/미조회이며 빈 기록과 다르다. 실패한 로그인은 이 메서드를 호출하지 않는다.</summary>
        public static void ReceiveServerState(List<TutorialProgressDTO> records)
        {
            _network = NetworkManager.Instance;
            _accountGeneration = LocalProgression.AccountGeneration;
            _networkGeneration = _network != null ? _network.AccountGeneration : -1;
            _records = records != null && records.Count <= 32 && records.All(Valid) &&
                records.Select(x => x.SequenceId).Distinct(StringComparer.Ordinal).Count() == records.Count
                ? records.ToDictionary(x => x.SequenceId, Copy, StringComparer.Ordinal) : null;
        }

        /// <summary>서버 스냅샷에 해당 ID가 없는 경우에만 처음부터 시작한다. 로컬 저장을 열거나 읽지 않는다.</summary>
        public bool TryLoad(string id, long generation, out GameDirectProgress progress)
        {
            progress = null;
            if (!Available || generation != _accountGeneration || !ValidId(id)) return false;
            progress = _records.TryGetValue(id, out var record)
                ? new GameDirectProgress { ConfirmedSteps = new List<string>(record.ConfirmedSteps), Completed = record.IsFinished }
                : new GameDirectProgress();
            return true;
        }

        /// <summary>합집합/종료 OR 저장은 응답 유실 후 같은 입력을 재전송해도 지급·소비하지 않는다.</summary>
        public async UniTask<bool> TrySaveAsync(string id, long generation, GameDirectProgress progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Available || generation != _accountGeneration || progress?.ConfirmedSteps == null) return false;
            var request = new TutorialProgressDTO
            {
                SequenceId = id, ConfirmedSteps = new List<string>(progress.ConfirmedSteps),
                IsFinished = progress.Completed || progress.Skipped
            };
            if (!Valid(request)) return false;
            long networkGeneration = _networkGeneration;
            var completion = new UniTaskCompletionSource<bool>();
            _network.Call<OnSaveTutorialProgressRequestDTO, OnSaveTutorialProgressResponseDTO>(
                new OnSaveTutorialProgressRequestDTO { Progress = request },
                response => completion.TrySetResult(AcceptSavedProgress(request, response.Progress, generation, networkGeneration)),
                error =>
                {
                    Debug.LogWarning("[GameDirect] 서버 진행 저장 실패: " + NetworkManager.DescribeFailure(error, "튜토리얼 저장"));
                    completion.TrySetResult(false);
                });
            return await completion.Task.AttachExternalCancellation(token);
        }

        internal static bool AcceptSavedProgress(TutorialProgressDTO sent, TutorialProgressDTO saved, long account, long network)
        {
            if (!Available || !IsCurrent(account, network) || !Valid(sent) || !Valid(saved) || !Contains(saved, sent) ||
                (_records.TryGetValue(sent.SequenceId, out var latest) && !Contains(saved, latest))) return false;
            _records[sent.SequenceId] = Copy(saved);
            return true;
        }

        private static bool Contains(TutorialProgressDTO saved, TutorialProgressDTO expected) =>
            saved.SequenceId == expected.SequenceId && (!expected.IsFinished || saved.IsFinished) &&
            expected.ConfirmedSteps.All(saved.ConfirmedSteps.Contains);
        private static TutorialProgressDTO Copy(TutorialProgressDTO source) => new()
        { SequenceId = source.SequenceId, ConfirmedSteps = new List<string>(source.ConfirmedSteps), IsFinished = source.IsFinished };
        internal static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 64 && !id.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
        private static bool Valid(TutorialProgressDTO record) => record != null && ValidId(record.SequenceId) && record.ConfirmedSteps != null &&
            record.ConfirmedSteps.Count <= 128 && record.ConfirmedSteps.All(ValidId) &&
            record.ConfirmedSteps.Distinct(StringComparer.Ordinal).Count() == record.ConfirmedSteps.Count;
    }
}
