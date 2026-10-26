using System;
using System.Threading.Tasks;
using Google;
using Protocol.Message.Dto;
using PlayFab;
using PlayFab.ClientModels;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Server.Auth
{
    public enum eAuthType { GooglePlayGame, GoogleWebLogin, CustomLogin, DummyLogin }

    public sealed class Authentication
    {
        private readonly NetworkManager _network;
        private const string GuestKey = "authentication.guest-id";
        private const double AuthenticationTimeoutSeconds = 30;
        private const string WebClientId = "222024339558-o157jmef5jhip2s9vtfo3kto1ojfpejf.apps.googleusercontent.com";
        public Authentication(NetworkManager network) => _network = network;

        public async void Authenticate(eAuthType type)
        {
            long generation = _network.BeginAuthentication();
            try
            {
                // Isolate SDK tokens too: a timed-out login must not replace another account's context.
                var client = new PlayFabClientInstanceAPI();
                var completion = new TaskCompletionSource<LoginResult>();
                var info = new GetPlayerCombinedInfoRequestParams { GetPlayerProfile = true };
                Action<PlayFabError> fail = error => completion.TrySetException(new AuthenticationException(error));
                if (type == eAuthType.CustomLogin)
                {
                    string id = PlayerPrefs.GetString(GuestKey, "");
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        id = Guid.NewGuid().ToString("N");
                        PlayerPrefs.SetString(GuestKey, id);
                        PlayerPrefs.Save();
                    }
                    client.LoginWithCustomID(new LoginWithCustomIDRequest
                    { CustomId = id, CreateAccount = true, InfoRequestParameters = info }, result => completion.TrySetResult(result), fail);
                }
                else if (type == eAuthType.GoogleWebLogin)
                {
                    GoogleSignIn.Configuration = new GoogleSignInConfiguration
                    {
                        WebClientId = WebClientId, UseGameSignIn = false, RequestIdToken = true,
                        RequestAuthCode = true, RequestEmail = true, RequestProfile = true
                    };
                    var google = await WithTimeout(GoogleSignIn.DefaultInstance.SignIn());
                    if (!IsCurrent(generation)) return;
                    client.LoginWithGoogleAccount(new LoginWithGoogleAccountRequest
                    { ServerAuthCode = google.AuthCode, CreateAccount = true, InfoRequestParameters = info },
                        result => completion.TrySetResult(result), fail);
                }
                else throw new InvalidOperationException("지원하지 않는 로그인 방식입니다.");

                var login = await WithTimeout(completion.Task);
                if (!IsCurrent(generation)) return;
                _network.SetAuthenticatedAccount(login.PlayFabId, login.SessionTicket, client.authenticationContext);
                // A missing display name does not prove that the player's data needs initialization.
                if (login.NewlyCreated)
                {
                    _network.Call<InitUserRequestDTO, UserInitResponseDTO>(new InitUserRequestDTO(), OnInitialized, _network.FailLogin);
                }
                else _network.Call<AuthRequestDTO, UserInitResponseDTO>(new AuthRequestDTO(), OnInitialized, OnLoginFailed);
            }
            catch (Exception ex)
            {
                if (!IsCurrent(generation)) return;
                if (ex is AuthenticationException auth)
                    _network.FailLogin(auth.Error);
                else
                    _network.FailLogin(NetworkManager.CreateError(ex is TimeoutException ? "Timeout" : "Transport",
                        ex is TimeoutException ? "로그인 대기 시간이 초과되었습니다. 다시 시도해 주세요." : "로그인이 취소되었거나 완료되지 않았습니다."));
            }
        }

        private bool IsCurrent(long generation) => _network != null && _network.AccountGeneration == generation;

        private void OnInitialized(UserInitResponseDTO response)
        {
            _network.SetGameSession(response.SessionID);
            // The full snapshot reaches game code through NetworkManager.LatestUserState / UserStateReceived and can be
            // re-read with RefreshUserState (OnGetUserState). Here only the session is stored and Ready is published.
            _network.CompleteLogin();
        }

        // Login rejection. When the server has no player data (1012 PlayerNotInitialized) sign up once instead.
        // While non-200 rejections arrive wrapped in FunctionExecutionError the ErrorCode is not delivered and this
        // branch cannot trigger; the delivery contract is decided after observing a live title (docs/client-contract-review.md).
        private void OnLoginFailed(PlayFabError error)
        {
            if (error?.CustomData is ErrorResponseDTO business &&
                business.ErrorCode == Protocol.Message.Data.eErrorCode.PlayerNotInitialized)
            {
                _network.Call<InitUserRequestDTO, UserInitResponseDTO>(new InitUserRequestDTO(), OnInitialized, _network.FailLogin);
                return;
            }
            _network.FailLogin(error);
        }

        private static async Task<T> WithTimeout<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(AuthenticationTimeoutSeconds))) != task)
            {
                // Observe a late fault without running any Unity or account work on its worker.
                _ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException();
            }
            return await task;
        }

        private sealed class AuthenticationException : Exception
        {
            public PlayFabError Error { get; }
            public AuthenticationException(PlayFabError error) : base("PlayFab authentication failed.") => Error = error;
        }
    }
}

