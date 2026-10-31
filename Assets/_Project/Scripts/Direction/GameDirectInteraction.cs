using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using KingdomIdle.Balance;
using KingdomIdle.UGUI;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace Direction
{
    /// <summary>현재 단계의 짧은 실행 범위와 거래 안에 쓰는 성공 영수증을 연결한다. 순서·진행 저장의 소유자는 Manager다.</summary>
    public static class GameDirectInteraction
    {
        public static GameDirectStep Step { get; private set; }
        public static long Epoch { get; private set; }
        public static bool Armed { get; private set; }
        public static bool Clicked { get; private set; }
        public static bool SaveFailed { get; private set; }
        private static string _sequence;
        private static long _account;
        private static Func<bool> _enter;
        private static bool _entered;
        public static bool CurrentAccount => Step != null && _account == LocalProgression.AccountGeneration;
        public static bool AtAttackCap => CurrentAccount && Step.Action == GuideAction.AttackOnce &&
            LocalProgression.TryGetCommittedState(out var s) && s.AttackLevel >= BalanceMath.GoldCap;
        public static bool Succeeded => CurrentAccount && HasReceipt(_sequence, Step.Id);

        /// <summary>Manager가 미확인 단계 시작 때 호출한다. 입력은 아직 닫고 최초 표시 직전에 실행할 저장 콜백을 보관한다.</summary>
        public static void Begin(string sequence, GameDirectStep step, long account, Func<bool> enter)
        { End(); _sequence = sequence; Step = step; _account = account; _enter = enter; }

        /// <summary>화면 준비 이후 호출하며 지급 저장이 성공한 뒤에만 입력을 허용한다. 실패하면 Player의 finally가 표시를 정리한다.</summary>
        public static void Arm()
        {
            if (!CurrentAccount) return;
            if (!_entered)
            {
                if (_enter != null && !_enter()) throw new InvalidOperationException("실습 지급을 저장하지 못했습니다. 다음 요청에서 이어갑니다.");
                _entered = true;
            }
            Armed = true;
        }

        /// <summary>모달·로딩으로 숨길 때 호출한다. 진행은 지우지 않고 뒤늦은 버튼·연출 콜백의 경제 동작을 막는다.</summary>
        public static void Suspend() { if (Armed) Epoch++; Armed = false; }

        /// <summary>단계 종료·취소·계정 전환 시 모든 참조를 해제하고 지연 콜백 세대를 무효화한다.</summary>
        public static void End()
        { Epoch++; Armed = false; Clicked = false; SaveFailed = false; Step = null; _sequence = null; _enter = null; _entered = false; }

        /// <summary>실제 경제 거래의 저장 실패를 Player에 알린다. 다음 프레임 정리 전에 추가 소비부터 금지한다.</summary>
        public static void ReportSaveFailure() { if (CurrentAccount) { SaveFailed = true; Suspend(); } }

        /// <summary>원본 버튼의 기존 처리가 끝난 뒤 호출한다. 현재 허용된 탐색 버튼만 확인하며 경제 성공으로 사용하지 않는다.</summary>
        public static void NotifyClick(GameDirectTarget target)
        { if (CurrentAccount && Armed && Step.Completion == GuideCompletion.Click && Step.Target == target) Clicked = true; }

        /// <summary>기존 경제 진입점에서 사용한다. 실습 중에는 지정된 1회 행동만 허용하고 이미 저장된 행동의 연타를 거절한다.</summary>
        public static bool CanPerform(GuideAction action, int count)
        { return Step == null || (CurrentAccount && Armed && Step.Completion == GuideCompletion.Action && Step.Action == action && count == 1 && !Succeeded); }

        /// <summary>강화·뽑기의 거래 초안에 성공 영수증을 넣는다. 별도 Execute를 호출하지 않아 소비·보상·증거가 함께 롤백된다.</summary>
        public static void RecordSuccess(ProgressionState draft, GuideAction action)
        {
            if (CurrentAccount && Armed && Step.Completion == GuideCompletion.Action && Step.Action == action)
                draft.Modules[ReceiptKey(_sequence, Step.Id)] = "1";
        }

        /// <summary>같은 계정의 확정 스냅샷만 읽는다. 성공 직후 앱 종료 시 이 값으로 재소비를 방지한다.</summary>
        public static bool HasReceipt(string sequence, string step) => LocalProgression.TryGetCommittedState(out var s) && s.Modules.ContainsKey(ReceiptKey(sequence, step));

        /// <summary>진행 JSON과 분리한 단계별 키를 만든다. 이전 진행 복사본을 저장해도 경제 성공 증거를 덮지 않는다.</summary>
        private static string ReceiptKey(string sequence, string step) => "game-direct-action:" + sequence + ":" + step;

        /// <summary>Manager가 실습 단계를 표시하기 전에 호출한다. 지급은 서버(OnClaimGuideReward)가 단계별로 한 번만 하고, 이미 지급된 단계는
        /// AlreadyClaimed로 알려 주므로 중단된 안내를 그대로 재개한다. 실패하면 false를 돌려주고 단계는 미확인으로 남는다.</summary>
        public static UniTask<bool> ClaimPracticeCoinsAsync(string sequence, GameDirectStep step, long account, CancellationToken token)
        {
            if (!step.GrantPracticeCoins) return UniTask.FromResult(true);
            return ClaimGuideRewardAsync(sequence + ":" + step.Id, account, token);
        }

        /// <summary>서버 연결로 무효화된 로컬 지급. 에디터 QA 스크립트(InteractiveGuideAcceptance) 컴파일 호환용으로 시그니처만 남긴다.
        /// 지급하지 않으며 운영 경로는 ClaimPracticeCoinsAsync다.</summary>
        public static bool GrantCoins(string sequence, GameDirectStep step, long account) => account == LocalProgression.AccountGeneration;

        /// <summary>서버에 실습 보상을 요청하고 응답의 재화 절대값을 플레이어 상태에 쓴다. 로컬에서 지급하거나 청구 키를 남기지 않는다.</summary>
        public static async UniTask<bool> ClaimGuideRewardAsync(string guideId, long account, CancellationToken token)
        {
            if (account != LocalProgression.AccountGeneration) return false;
            var network = NetworkManager.Instance;
            if (network == null || !network.IsGameDataReady)
            {
                Debug.LogWarning("[Guide] claim skipped: server login is not ready");
                return false;
            }
            var completion = new UniTaskCompletionSource<bool>();
            network.Call<OnClaimGuideRewardRequestDTO, OnClaimGuideRewardResponseDTO>(new OnClaimGuideRewardRequestDTO { GuideId = guideId },
                response =>
                {
                    if (account != LocalProgression.AccountGeneration) { completion.TrySetResult(false); return; }
                    bool saved = LocalProgression.Execute("server-guide-reward", draft => { UserManager.ApplyCurrency(draft, response.Currency); return true; });
                    Debug.Log($"[Guide] claim id={guideId} already={response.AlreadyClaimed} granted={response.GrantedAncientCoin} | server AncientCoin={response.Currency?.AncientCoin} | state AncientCoin={LocalProgression.Balance(eCurrency.AncientCoin)} | saved={saved}");
                    if (!saved) ReportSaveFailure();
                    completion.TrySetResult(saved);
                },
                error =>
                {
                    string kind = NetworkManager.GetFailureKind(error);
                    int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
                    Debug.LogWarning($"[Guide] claim id={guideId} rejected code={code} kind={kind} message={error?.ErrorMessage}");
                    if (kind == "Timeout" || kind == "Transport")
                    {
                        // 응답 유실: 서버가 지급했을 수 있다. 재전송 대신 상태를 다시 받고 이번 요청은 중단한다. 다음 요청은 AlreadyClaimed로 이어진다.
                        UserManager.Instance?.RefreshFromServer(_ => completion.TrySetResult(false));
                        return;
                    }
                    UIManager.Instance?.ShowToast(NetworkManager.DescribeFailure(error, "실습 보상"));
                    completion.TrySetResult(false);
                });
            return await completion.Task.AttachExternalCancellation(token);
        }
    }
}
