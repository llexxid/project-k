using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google;
using Protocol.Message.Dto;
using PlayFab;
using PlayFab.ClientModels;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace Scripts.Server.Auth
{
	public enum eAuthType { GooglePlayGame, GoogleWebLogin, CustomLogin, DummyLogin }

	public sealed class Authentication
	{
		private readonly NetworkManager _network;
		// PlayFab display name of the account being signed in. The server snapshot has no nickname.
		private string _displayName;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
		// Development-only shared master account. Release builds never compile this ID.
		private const string MasterCustomId = "516A4AAD45F1CC09";
#endif
		private const double AuthenticationTimeoutSeconds = 30;
		private const string WebClientId = "222024339558-o157jmef5jhip2s9vtfo3kto1ojfpejf.apps.googleusercontent.com";
		// OnLogIn이 PlayFab의 10초 한도에 걸린 뒤(함수 앱 콜드 스타트) 한 번만 다시 부른다.
		private bool _retriedLogin;
		public Authentication(NetworkManager network) => _network = network;

		public async void Authenticate(eAuthType type, Func<Task<bool>> checkEligibility, Func<Task<bool>> requestTerms)
		{
			long generation = _network.BeginAuthentication();
			_retriedLogin = false;
			try
			{
				bool checkedAge = false;
				if (!GameTermsConsent.HasEligibleProvider(type) || KingdomIdle.UGUI.AlphaAgeGate.IsBlocked)
				{
					if (!await CheckAgeAsync(generation, checkEligibility)) return;
					checkedAge = true;
				}
				string acceptedAt = null;
				string googleAccountHint = null;
				if (!GameTermsConsent.VerifiedOnDevice)
				{
					acceptedAt = await GameTermsConsent.RequestAsync(requestTerms);
					if (!IsCurrent(generation)) return;
				}
				// Isolate SDK tokens too: a timed-out login must not replace another account's context.
				var client = new PlayFabClientInstanceAPI();
				var completion = new TaskCompletionSource<LoginResult>();
				var info = new GetPlayerCombinedInfoRequestParams
				{
					GetPlayerProfile = true, GetUserData = true,
					UserDataKeys = new List<string> { GameTermsConsent.ReceiptKey }
				};
				Action<PlayFabError> fail = error => completion.TrySetException(new AuthenticationException(error));
				if (type == eAuthType.CustomLogin)
				{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
					// The title disables player creation for custom IDs, so only sign in to the existing master account.
					client.LoginWithCustomID(new LoginWithCustomIDRequest
					{ CustomId = MasterCustomId, CreateAccount = false, InfoRequestParameters = info }, result => completion.TrySetResult(result), fail);
#else
                    throw new InvalidOperationException("게스트 로그인은 개발 빌드에서만 지원합니다.");
#endif
				}
				else if (type == eAuthType.GoogleWebLogin)
				{
					GoogleSignIn.Configuration = new GoogleSignInConfiguration
					{
						WebClientId = WebClientId,
						UseGameSignIn = false,
						RequestIdToken = true,
						RequestAuthCode = true,
						RequestEmail = true,
						RequestProfile = true
					};
					var google = await WithTimeout(GoogleSignIn.DefaultInstance.SignIn());
					if (!IsCurrent(generation)) return;
					googleAccountHint = GameTermsConsent.GoogleHint(google.UserId);
					if (!GameTermsConsent.RecognizesGoogleAccount(googleAccountHint))
					{
						// Check the newly selected person before allowing PlayFab to create their account.
						if (!checkedAge && !await CheckAgeAsync(generation, checkEligibility)) return;
						acceptedAt ??= await GameTermsConsent.RequestAsync(requestTerms);
						if (!IsCurrent(generation)) return;
					}
					client.LoginWithGoogleAccount(new LoginWithGoogleAccountRequest
					{ ServerAuthCode = google.AuthCode, CreateAccount = true, InfoRequestParameters = info },
						result => completion.TrySetResult(result), fail);
				}
				else
				{
					throw new InvalidOperationException("지원하지 않는 로그인 방식입니다.");
				}


				var login = await WithTimeout(completion.Task);
				if (!IsCurrent(generation)) return;
				if (!GameTermsConsent.HasReceipt(login.InfoResultPayload?.UserData))
				{
					// A device hint for a previous player never counts as this player's agreement.
					acceptedAt ??= await GameTermsConsent.RequestAsync(requestTerms);
					if (!IsCurrent(generation)) return;
					try
					{
						await WithTimeout(GameTermsConsent.StoreAsync(client, acceptedAt));
					}
					catch (Exception)
					{
						if (IsCurrent(generation))
							_network.FailLogin(NetworkManager.CreateError("ConsentSaveFailed",
								"약관 동의 기록을 저장하지 못했습니다. 연결을 확인하고 다시 로그인해 주세요."));
						return;
					}
					if (!IsCurrent(generation)) return;
				}
				GameTermsConsent.MarkVerifiedOnDevice(type, googleAccountHint);
#if UNITY_ANDROID && !UNITY_EDITOR
				InitializePlayGamesAfterAgreement();
#endif
				_network.SetAuthenticatedAccount(login.PlayFabId, login.SessionTicket, client.authenticationContext);
				_displayName = login.InfoResultPayload?.PlayerProfile?.DisplayName;
				// A missing display name does not prove that the player's data needs initialization.
				if (login.NewlyCreated)
				{
					_network.Call<InitUserRequestDTO, UserInitResponseDTO>(new InitUserRequestDTO(), OnInitialized, _network.FailLogin);
				}
				else
				{
					_network.Call<AuthRequestDTO, UserInitResponseDTO>(new AuthRequestDTO(), OnInitialized, OnLoginFailed);
				}
			}
			catch (Exception ex)
			{
				if (!IsCurrent(generation)) return;
				if (ex is OperationCanceledException)
					_network.FailLogin(NetworkManager.CreateError("ConsentDeclined", "게임을 시작하려면 서비스 이용약관에 동의해 주세요."));
				else if (ex is AuthenticationException auth)
					_network.FailLogin(auth.Error);
				else
					_network.FailLogin(NetworkManager.CreateError(ex is TimeoutException ? "Timeout" : "Transport",
						ex is TimeoutException ? "로그인 대기 시간이 초과되었습니다. 다시 시도해 주세요." : "로그인이 취소되었거나 완료되지 않았습니다."));
			}
		}

		private bool IsCurrent(long generation) => _network != null && _network.AccountGeneration == generation;

#if UNITY_ANDROID && !UNITY_EDITOR
		private static bool _playGamesStarted;

		private static void InitializePlayGamesAfterAgreement()
		{
			if (_playGamesStarted) return;
			_playGamesStarted = true;
			UnityEngine.Android.AndroidApplication.InvokeOnUIThread(() =>
			{
				try
				{
					using var sdk = new AndroidJavaClass("com.google.android.gms.games.PlayGamesSdk");
					sdk.CallStatic("initialize", UnityEngine.Android.AndroidApplication.currentContext);
				}
				catch (AndroidJavaException)
				{
					Debug.LogWarning("[Login] Play Games 플랫폼 연결을 시작하지 못했습니다.");
				}
			});
		}
#endif

		private async Task<bool> CheckAgeAsync(long generation, Func<Task<bool>> checkEligibility)
		{
			GameTermsConsent.ClearAgeHint();
			bool eligible = checkEligibility != null && await checkEligibility();
			if (!IsCurrent(generation)) return false;
			if (!eligible) _network.FailLogin(NetworkManager.CreateError("EligibilityCanceled", "로그인을 진행하지 않았습니다."));
			return eligible;
		}

		// Order: apply the server snapshot to UserManager, then publish Ready. Ready synchronously starts the main
		// scene load (TitleScreenController), which reads the current stage and characters from UserManager.
		private void OnInitialized(UserInitResponseDTO response)
		{
			var users = UserManager.Instance;
			if (users == null)
			{
				_network.FailLogin(NetworkManager.CreateError("ApplyFailed", "사용자 데이터를 적용하지 못했습니다. 다시 시도해 주세요."));
				return;
			}
			// The DTO and game stage enums share long stage IDs. ValidateResponse already rejected negative
			// enhancement counts, so the ulong casts are safe.
			users.CreateUser(string.IsNullOrWhiteSpace(_displayName) ? "Guest" : _displayName, (eStage)(long)response.StageState.CurrentStage,
				response.Exp, response.KillScore, response.Level, (ulong)response.EnchantHPCount, (ulong)response.EnchantATKCount);
			users.SetCharacterData(response.CharacterDatas);
			// GetSessionID() already returns the PlayFabId here, so the server player data lands in this account's progression file.
			if (!users.ApplyServerPlayerData(response))
			{
				_network.FailLogin(NetworkManager.CreateError("ApplyFailed", "서버 계정 상태를 저장하지 못했습니다. 다시 시도해 주세요."));
				return;
			}
			users.SetInventoryData(response.Inventory);
			users.SetSkillTreeData(response.SkillTreeDatas);
			users.SetJobTreeData(response.JobTrees);

			_network.SetGameSession(response.SessionID);
			_network.CompleteLogin();
		}

		private void OnLoginFailed(PlayFabError error)
		{
			if (error?.CustomData is ErrorResponseDTO business &&
				business.ErrorCode == Protocol.Message.Data.eErrorCode.PlayerNotInitialized)
			{
				_network.Call<InitUserRequestDTO, UserInitResponseDTO>(new InitUserRequestDTO(), OnInitialized, _network.FailLogin);
				return;
			}
			// Consumption 플랜 콜드 스타트는 PlayFab의 10초 실행 한도를 넘길 수 있다. 그때쯤 함수 앱은 떠 있으므로 한 번 더 부르면 보통 1~2초에 끝난다.
			if (error != null && error.Error == PlayFabErrorCode.CloudScriptAzureFunctionsExecutionTimeLimitExceeded && !_retriedLogin)
			{
				_retriedLogin = true;
				Debug.LogWarning("[Login] OnLogIn이 실행 시간 한도를 넘겨 한 번 더 시도합니다: " + error.ErrorMessage);
				_network.Call<AuthRequestDTO, UserInitResponseDTO>(new AuthRequestDTO(), OnInitialized, OnLoginFailed);
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

