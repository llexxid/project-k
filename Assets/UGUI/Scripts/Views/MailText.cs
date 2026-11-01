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
            ["mail.test.body"] = "첨부된 테스트 보상을 확인해 주세요."
        };
        private static readonly HashSet<string> Warned = new();
        internal static string Title(MailDTO mail) => Resolve(mail.TitleKey, "시스템 우편");
        internal static string Body(MailDTO mail) => Resolve(mail.BodyKey, "첨부 보상을 확인해 주세요.");
        internal static string Sender(MailDTO mail) => mail.Category == MailCategory.Attendance ? "출석 보상" : "운영팀";
        private static string Resolve(string key, string fallback)
        {
            if (key != null && Text.TryGetValue(key, out var text)) return text;
            if (Warned.Count < 100 && Warned.Add(key ?? "")) Debug.LogWarning("[Mailbox] Missing text key: " + key);
            return fallback;
        }
    }
}
