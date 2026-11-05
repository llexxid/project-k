using System.Collections.Generic;
using Protocol.Message.Dto;
using UnityEngine;

namespace KingdomIdle.UGUI
{
    internal static class MailText
    {
        private static readonly Dictionary<string, string> Text = new()
        {
            ["mail.attendance.carryover.title"] = "지난 달 출석 미수령 보상",
            ["mail.attendance.carryover.body"] = "지난 달 출석부에서 받지 않은 보상입니다.",
            ["mail.test.title"] = "테스트 보상",
            ["mail.test.body"] = "첨부된 테스트 보상을 확인해 주세요.",
            ["mail.cbt.title"] = "클로즈 베타 테스트에 참여해주셔서 감사합니다",
            ["mail.cbt.body"] = "즐겁게 플레이 해 주시고 불편한점은 자유롭게 건의해주세요!",
            ["mail.maintenance.reward.title"] = "점검 협조 감사 보상",
            ["mail.maintenance.reward.body"] = "원활한 서비스 운영에 협조해 주셔서 감사합니다. 첨부된 보상을 확인해 주세요.",
            ["mail.compensation.title"] = "서비스 이용 불편 보상",
            ["mail.compensation.body"] = "서비스 이용 중 겪으신 불편에 대해 사과드립니다. 첨부된 보상을 확인해 주세요.",
            ["mail.update.gift.title"] = "업데이트 기념 선물",
            ["mail.update.gift.body"] = "새로운 콘텐츠와 함께 즐거운 시간을 보내시길 바랍니다. 첨부된 선물을 확인해 주세요.",
            ["mail.event.reward.title"] = "이벤트 보상 안내",
            ["mail.event.reward.body"] = "이벤트와 함께해 주셔서 감사합니다. 첨부된 보상을 확인해 주세요.",
            ["mail.feedback.thanks.title"] = "의견 전달 감사 선물",
            ["mail.feedback.thanks.body"] = "보내주신 의견은 게임을 개선하는 데 참고하겠습니다. 관심과 참여에 감사드립니다.",
            ["mail.support.reward.title"] = "플레이 지원 선물",
            ["mail.support.reward.body"] = "모험에 도움이 되길 바라며 준비했습니다. 첨부된 보상을 확인해 주세요.",
            ["mail.operations.gift.title"] = "운영팀의 선물",
            ["mail.operations.gift.body"] = "함께해 주시는 플레이어 여러분께 감사드립니다. 즐거운 모험이 되시길 바랍니다."
        };
        private static readonly HashSet<string> Warned = new();
        internal static string Title(MailDTO mail) => mail.TextMode == MailTextMode.Text ? mail.Title : Resolve(mail.TitleKey, "시스템 우편");
        internal static string Body(MailDTO mail) => mail.TextMode == MailTextMode.Text ? mail.Body : Resolve(mail.BodyKey, "첨부 보상을 확인해 주세요.");
        internal static string Sender(MailDTO mail) => mail.Category == MailCategory.Attendance ? "출석 보상" : "운영팀";
        private static string Resolve(string key, string fallback)
        {
            if (key != null && Text.TryGetValue(key, out var text)) return text;
            if (Warned.Count < 100 && Warned.Add(key ?? "")) Debug.LogWarning("[Mailbox] Missing text key: " + key);
            return fallback;
        }
    }
}
