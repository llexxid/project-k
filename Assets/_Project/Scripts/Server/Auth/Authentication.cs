using System;
using System.Collections.Generic;
using System.Threading;
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
		private const double GoogleInteractiveTimeoutSeconds = 180;
		private static readonly object GoogleSignInGate = new object();
		private static Task<GoogleSignInUser> _pendingGoogleSignIn;
		private const string WebClientId = "222024339558-o157jmef5jhip2s9vtfo3kto1ojfpejf.apps.googleusercontent.com";
		// 콜드 스타트 또는 저장소 요청 제한 뒤 같은 인증 시도에서 로그인 조회를 한 번만 재시도한다.
		private bool _retriedLogin;
		public Authentication(NetworkManager network) => _network = network;

		public async void Authenticate(eAuthType type, Func<Task<bool>> requestTerms)
		{
			long generation = _network.BeginAuthentication();
			_retriedLogin = false;
			try
			{
				if (GameTermsConsent.HasActiveAgeRestriction)
				{
					_network.FailLogin(NetworkManager.CreateError("AgeRestricted", "이전 연령 확인에 따라 알파 테스트 참여가 제한되어 있습니다."));
					return;
				}
				string acceptedAt = null;
				string googleAccountHint = null;
				if (!GameTermsConsent.VerifiedOnDevice || !GameTermsConsent.HasEligibleProvider(type))
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
					ConfigureGoogleSignIn();
					var google = await WithTimeout(BeginGoogleSignIn(GoogleSignIn.DefaultInstance.SignIn), GoogleInteractiveTimeoutSeconds);
					if (!IsCurrent(generation)) return;
					googleAccountHint = GameTermsConsent.GoogleHint(google.UserId);
					if (acceptedAt == null && !GameTermsConsent.RecognizesGoogleAccount(googleAccountHint))
					{
						// The last player's agreement cannot authorize creation of this player's account.
						acceptedAt = await GameTermsConsent.RequestAsync(requestTerms);
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
				if (!await KingdomIdle.UGUI.ClientAccessGate.WaitAsync(client, () => IsCurrent(generation)) || !IsCurrent(generation)) return;
				_displayName = login.InfoResultPayload?.PlayerProfile?.DisplayName;
				// A missing display name does not prove that the player's data needs initialization.
				if (login.NewlyCreated)
				{
					_network.Call<InitUserRequestDTO, UserInitResponseDTO>(new InitUserRequestDTO { EquipmentSchema = EquipmentManager.InstanceSchemaVersion, EquipmentCapacity = EquipmentManager.InstanceCapacity }, OnInitialized, _network.FailLogin);
				}
				else
				{
					_network.Call<AuthRequestDTO, UserInitResponseDTO>(new AuthRequestDTO { EquipmentSchema = EquipmentManager.InstanceSchemaVersion, EquipmentCapacity = EquipmentManager.InstanceCapacity }, OnInitialized, OnLoginFailed);
				}
			}
			catch (Exception ex)
			{
				if (!IsCurrent(generation)) return;
				if (ex is GoogleSignInPendingException)
					_network.FailLogin(NetworkManager.CreateError("GooglePending",
						"이전 Google 로그인 요청이 아직 진행 중입니다. Google 창에서 처리를 마친 뒤 다시 시도해 주세요."));
				else if (ex is GameTermsConsent.TermsDeclinedException)
					_network.FailLogin(NetworkManager.CreateError("ConsentDeclined", "게임을 시작하려면 필수 항목에 동의해 주세요."));
				else if (ex is AuthenticationException auth)
					_network.FailLogin(auth.Error);
				else
					_network.FailLogin(NetworkManager.CreateError(ex is TimeoutException ? "Timeout" : "Transport",
						ex is TimeoutException ? "로그인 대기 시간이 초과되었습니다. 다시 시도해 주세요." : "로그인이 취소되었거나 완료되지 않았습니다."));
			}
		}

		private bool IsCurrent(long generation) => _network != null && _network.AccountGeneration == generation;

		private static void ConfigureGoogleSignIn()
		{
			// The SDK rejects a replacement reference once DefaultInstance exists, including after a failed attempt.
			// SignOut clears Configuration, so restore the same settings only when it is absent.
			if (GoogleSignIn.Configuration != null) return;
			GoogleSignIn.Configuration = new GoogleSignInConfiguration
			{
				WebClientId = WebClientId,
				UseGameSignIn = false,
				RequestIdToken = true,
				RequestAuthCode = true,
				RequestEmail = true,
				RequestProfile = true
			};
		}

		private static Task<GoogleSignInUser> BeginGoogleSignIn(Func<Task<GoogleSignInUser>> start)
		{
			// The SDK reuses one native Future. A wrapper timeout must not start another request on it.
			lock (GoogleSignInGate)
			{
				if (_pendingGoogleSignIn != null && !_pendingGoogleSignIn.IsCompleted)
					throw new GoogleSignInPendingException();
				_pendingGoogleSignIn = null;
				var task = start();
				_pendingGoogleSignIn = task;
				_ = task.ContinueWith(completed =>
				{
					// Drain a late failure without logging credentials or performing Unity/account work here.
					if (completed.IsFaulted) { var ignored = completed.Exception; }
					lock (GoogleSignInGate)
					{
						if (ReferenceEquals(_pendingGoogleSignIn, completed)) _pendingGoogleSignIn = null;
					}
				}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
				return task;
			}
		}

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
				_network.Call<InitUserRequestDTO, UserInitResponseDTO>(new InitUserRequestDTO { EquipmentSchema = EquipmentManager.InstanceSchemaVersion, EquipmentCapacity = EquipmentManager.InstanceCapacity }, OnInitialized, _network.FailLogin);
				return;
			}
			if (!_retriedLogin && TryGetLoginRetrySeconds(error, out var retryAfter))
			{
				_retriedLogin = true;
				_network.StartCoroutine(RetryThrottledLogin(_network.AccountGeneration, retryAfter));
				return;
			}
			// Consumption 플랜 콜드 스타트는 PlayFab의 10초 실행 한도를 넘길 수 있다.
			if (error != null && error.Error == PlayFabErrorCode.CloudScriptAzureFunctionsExecutionTimeLimitExceeded && !_retriedLogin)
			{
				_retriedLogin = true;
				Debug.LogWarning("[Login] OnLogIn이 실행 시간 한도를 넘겨 한 번 더 시도합니다: " + error.ErrorMessage);
				_network.Call<AuthRequestDTO, UserInitResponseDTO>(new AuthRequestDTO { EquipmentSchema = EquipmentManager.InstanceSchemaVersion, EquipmentCapacity = EquipmentManager.InstanceCapacity }, OnInitialized, OnLoginFailed);
				return;
			}
			_network.FailLogin(error);
		}

		internal static bool TryGetLoginRetrySeconds(PlayFabError error, out int seconds)
		{
			bool storage = NetworkManager.TryGetStorageThrottle(error, out var suggested);
			bool limited = error != null && (error.HttpCode == 429 || error.Error == PlayFabErrorCode.APIClientRequestRateLimitExceeded);
			long delay = suggested ?? (long?)error?.RetryAfterSeconds ?? 5L;
			seconds = (int)Math.Min(int.MaxValue, Math.Max(1L, delay));
			return storage || limited;
		}

		private System.Collections.IEnumerator RetryThrottledLogin(long generation, int seconds)
		{
			yield return new WaitForSecondsRealtime(seconds);
			if (IsCurrent(generation) && _network.IsAuthenticating)
				_network.Call<AuthRequestDTO, UserInitResponseDTO>(new AuthRequestDTO {
					EquipmentSchema = EquipmentManager.InstanceSchemaVersion, EquipmentCapacity = EquipmentManager.InstanceCapacity
				}, OnInitialized, OnLoginFailed);
		}

		private static async Task<T> WithTimeout<T>(Task<T> task, double seconds = AuthenticationTimeoutSeconds)
		{
			if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds))) != task)
			{
				// Observe a late fault without running any Unity or account work on its worker.
				_ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
				throw new TimeoutException();
			}
			return await task;
		}

		private sealed class GoogleSignInPendingException : Exception { }

		private sealed class AuthenticationException : Exception
		{
			public PlayFabError Error { get; }
			public AuthenticationException(PlayFabError error) : base("PlayFab authentication failed.") => Error = error;
		}
	}
}

