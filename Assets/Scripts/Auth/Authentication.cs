using Google;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.PfEditor.EditorModels;
using Scripts.Core;
using Scripts.Core.Manager;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Auth
{
	public enum eAuthType
	{
		GooglePlayGame,
		GoogleWebLogin,
	}

	public class Authentication
	{
		private string _authToken;
		private eAuthType _type;
		//하드코딩 해도 되나?
		private readonly string _webClientId = "222024339558-o157jmef5jhip2s9vtfo3kto1ojfpejf.apps.googleusercontent.com";
		public Authentication()
		{
			GoogleSignInConfiguration conf = new GoogleSignInConfiguration
			{
				WebClientId = _webClientId,
				RequestIdToken = true,
				RequestAuthCode = true,
				RequestEmail = true,
				RequestProfile = true
			};

			GoogleSignIn.Configuration = conf;
			GoogleSignIn.Configuration.UseGameSignIn = false;
			GoogleSignIn.Configuration.RequestIdToken = true;
		}

		public void Authenticate(eAuthType type)
		{
			//내가 로그인 한 타입 저장해놓기 (추후 디버깅용)
			_type = type;
			switch (type)
			{
				case eAuthType.GooglePlayGame:
					break;
				case eAuthType.GoogleWebLogin:
					GoogleWebAuth();
					break;
				default:
					break;
			}

		}


		private void GoogleWebAuth()
		{
			GoogleSignIn.DefaultInstance.SignIn().ContinueWith(PlayFabAuth);
		}


		private void PlayFabAuth(Task<GoogleSignInUser> task)
		{
			if (task.IsFaulted)
			{
				Debug.LogError("구글 로그인 실패: " + task.Exception);
				return;
			}
			else if (task.IsCanceled)
			{
				Debug.Log("구글 로그인이 취소되었습니다.");
				return;
			}

			//PlayFab에 로그인을 요청할 때, 어떤 정보를 같이 요청할 것인가?
			//인벤토리 데이터,유저 데이터, 캐릭터 데이터, 강화수치, 재화
			GetPlayerCombinedInfoRequestParams infoRequestParams = new GetPlayerCombinedInfoRequestParams
			{ 
				GetUserReadOnlyData = true,
				UserReadOnlyDataKeys = new List<string>
				{ 
					"UserData",
					"CharacterData",
					"Inventory",
					"Currency",
					"MagicSkill"
					//MailSlot
					//
				}
			};
			_authToken = task.Result.AuthCode;
			LoginWithGoogleAccountRequest pfLoginRequest = new LoginWithGoogleAccountRequest
			{
				TitleId = PlayFabSettings.TitleId,
				ServerAuthCode = _authToken,
				InfoRequestParameters = infoRequestParams,
				CreateAccount = true
			};

			PlayFabClientAPI.LoginWithGoogleAccount(
				request: pfLoginRequest,
				pfAuthSuccessCallback,
				pfAuthErrorCallback
				);

		}

		private void pfAuthSuccessCallback(LoginResult authresult)
		{
			//User에 playFabId 셋팅 / SessionTicket셋팅 
			//User와 SEssion이 합쳐있는 구조로 하니까, User를 생성하는 시점에
			// 처음 생성하는거면, 데이터를 먼저 받고 -> 닉네임 설정 -> 유저 정보 셋팅 
			// 즉, user와 Session이 나눠져야함.
			NetworkManager.Instance.SetSessionID(authresult.PlayFabId);
			NetworkManager.Instance.SetSessionTicket(authresult.SessionTicket);
			if (authresult.NewlyCreated == true)
			{
				// 처음 로그인 한다면 유저 생성 및 닉네임 설정 쪽으로 분기해야함. 
				// 유저 생성 함수 부르고, 닉네임 설정 UI로 분기.
				return;
			}




			// 처음 로그인 이 아니라면, 



		}

		private void pfAuthErrorCallback(PlayFab.PlayFabError error)
		{

		}

	}
}

