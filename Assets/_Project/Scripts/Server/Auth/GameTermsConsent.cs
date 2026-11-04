using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Scripts.Server.Auth
{
    internal static class GameTermsConsent
    {
        // Bump only when the published agreement changes and fresh acceptance is required.
        internal const string TermsVersion = "2026-10-08";
        internal const string ReceiptKey = "TermsAccepted_" + TermsVersion;
        const string VerifiedVersionKey = "legal.lastVerifiedTermsVersion";
        const string LastGoogleKey = "legal.lastVerifiedGoogle";
        const string AgeProviderKey = "legal.lastEligibleProvider";
        const string AgePolicyVersion = "alpha14-v1/";
        // Earlier alpha builds stored this 24-hour restriction when a player confirmed an ineligible age.
        const string PreviousAgeRetryKey = "age.alpha14.v1.retryAfterUtc";

        internal static bool HasActiveAgeRestriction =>
            long.TryParse(PlayerPrefs.GetString(PreviousAgeRetryKey), NumberStyles.None, CultureInfo.InvariantCulture,
                out var retryAt) && retryAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        internal static bool VerifiedOnDevice => PlayerPrefs.GetString(VerifiedVersionKey) == TermsVersion;

        internal static async Task<string> RequestAsync(Func<Task<bool>> prompt)
        {
            if (prompt == null || !await prompt()) throw new TermsDeclinedException();
            return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }

        internal static bool HasReceipt(Dictionary<string, UserDataRecord> data) =>
            data != null && data.TryGetValue(ReceiptKey, out var receipt) && receipt != null &&
            DateTime.TryParseExact(receipt.Value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var acceptedAt) && acceptedAt.Kind == DateTimeKind.Utc;

        internal static Task<UpdateUserDataResult> StoreAsync(PlayFabClientInstanceAPI client, string acceptedAt)
        {
            var result = new TaskCompletionSource<UpdateUserDataResult>();
            client.UpdateUserData(new UpdateUserDataRequest
            {
                Permission = UserDataPermission.Private,
                Data = new Dictionary<string, string> { [ReceiptKey] = acceptedAt }
            }, response => result.TrySetResult(response),
               error => result.TrySetException(new InvalidOperationException(error.Error.ToString())));
            return result.Task;
        }

        internal static string GoogleHint(string googleId)
        {
            if (string.IsNullOrEmpty(googleId)) return null;
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes("KingdomIdle/Google/" + googleId)));
        }

        internal static bool RecognizesGoogleAccount(string hint) =>
            !string.IsNullOrEmpty(hint) && PlayerPrefs.GetString(LastGoogleKey) == hint;

        internal static bool HasEligibleProvider(eAuthType provider) =>
            PlayerPrefs.GetString(AgeProviderKey) == AgePolicyVersion + provider &&
            (provider != eAuthType.GoogleWebLogin || !string.IsNullOrEmpty(PlayerPrefs.GetString(LastGoogleKey)));

        internal static void MarkVerifiedOnDevice(eAuthType provider, string googleHint)
        {
            if (VerifiedOnDevice && HasEligibleProvider(provider) && PlayerPrefs.GetString(LastGoogleKey) == (googleHint ?? "")) return;
            PlayerPrefs.SetString(VerifiedVersionKey, TermsVersion);
            PlayerPrefs.SetString(AgeProviderKey, AgePolicyVersion + provider);
            if (string.IsNullOrEmpty(googleHint)) PlayerPrefs.DeleteKey(LastGoogleKey);
            else PlayerPrefs.SetString(LastGoogleKey, googleHint);
            PlayerPrefs.Save();
        }

        internal sealed class TermsDeclinedException : OperationCanceledException { }
    }
}
