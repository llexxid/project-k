using Google;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.DTO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

using ItemID = Scripts.Server.DTO.ItemCode;
namespace Scripts.Server.Auth
{
	public enum eAuthType
	{
		GooglePlayGame,
		GoogleWebLogin,
		CustomLogin, //For Test
	}

	public class Authentication
	{
		private string _authToken;
		private eAuthType _type;
		//하드코딩 해도 되나?
		private readonly string _webClientId = "222024339558-o157jmef5jhip2s9vtfo3kto1ojfpejf.apps.googleusercontent.com";

		private readonly string key_userStage = "UserCurrentStage";
		private readonly string key_userData = "UserData";
		private readonly string key_userEnhancement = "UserEnhanceMent";
		private readonly string key_characterData = "CharacterData";
		private readonly string key_skillTreeData = "SkillTreeData";
		private readonly string key_inventoryData = "Inventory";
		private readonly string key_currency = "Currency";


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
				case eAuthType.CustomLogin:
					LoginTest();
					break;
				default:
					break;
			}
		}

		private void LoginTest()
		{
			GetPlayerCombinedInfoRequestParams infoRequestParams = new GetPlayerCombinedInfoRequestParams
			{
				GetUserReadOnlyData = true,
				UserReadOnlyDataKeys = new List<string>
				{
					key_userStage,
					key_userData,
					key_userEnhancement,
					key_characterData,
					key_skillTreeData,
					key_inventoryData,
					//MailSlot
					//
				},
				GetPlayerProfile = true,
			};

			LoginWithCustomIDRequest req = new LoginWithCustomIDRequest
			{
				CustomId = "516A4AAD45F1CC09",
				InfoRequestParameters = infoRequestParams,
			};
			PlayFabClientAPI.LoginWithCustomID(req, pfAuthSuccessTest, pfAuthErrorCallback);

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
					key_userStage,
					key_userData,
					key_userEnhancement,
					key_characterData,
					key_skillTreeData,
					key_inventoryData,
					key_currency,
					//MailSlot
					//
				},
				GetPlayerProfile = true,
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
		private void pfAuthSuccessTest(LoginResult result)
		{
			NetworkManager.Instance.SetSessionID(result.PlayFabId);
			NetworkManager.Instance.SetSessionTicket(result.SessionTicket);

			//NetworkManager.Instance.OnExistUserInit(OnSignUpUserSucccess, pfAuthErrorCallback);

			Dictionary<string, UserDataRecord> datas = result.InfoResultPayload.UserReadOnlyData;

			//필수정보들
			string nickName = result.InfoResultPayload.PlayerProfile.DisplayName;
			long currentStage = Convert.ToInt64(datas[key_userStage].Value);
			CurrencyQueryDTO currency = JsonConvert.DeserializeObject<CurrencyQueryDTO>(datas[key_currency].Value);
			UserDataQuery userdata = JsonConvert.DeserializeObject<UserDataQuery>(datas[key_userData].Value);
			UserEnhanceMentQuery enhancement = JsonConvert.DeserializeObject<UserEnhanceMentQuery>(datas[key_userEnhancement].Value);
			List<CharacterDataQuery> characterData = JsonConvert.DeserializeObject<List<CharacterDataQuery>>(datas[key_characterData].Value);

			//가진 장비나 스킬이 없을수도 있음.
			if (datas.ContainsKey(key_skillTreeData))
			{
				List<SkillCode> skillCodes = JsonConvert.DeserializeObject<List<SkillCode>>(datas[key_skillTreeData].Value);
			}
			if (datas.ContainsKey(key_inventoryData))
			{
				List<ItemID> inventory = JsonConvert.DeserializeObject<List<ItemID>>(datas[key_inventoryData].Value);
			}

			//Todo : 유저에서 Inventory랑 SkillTree가 연결되어야함.

			//유저셋팅
			UserManager.Instance.CreateUser(nickName,(eStage)currentStage, userdata, enhancement);
			UserManager.Instance.SetCharacterData(characterData);
			UserManager.Instance.SetWallet(currency);
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
				//TODO : 닉네임 설정 UI 띄우기
				// 닉네임 설정 UI에서 NetworkManager의 유저 생성 등의 함수를 부르면 됨.
				return;
			}

			//닉네임 중복체크하고, 확인버튼을 안눌렀을 수도 있음.
			string nickname = authresult.InfoResultPayload.PlayerProfile.DisplayName;
			if (nickname.Length == 0)
			{
				NetworkManager.Instance.OnSignUpInitUser(OnSignUpUserSucccess, pfAuthErrorCallback);
				return;
			}

			//성공적으로 로그인한 경우
			NetworkManager.Instance.OnExistUserInit(OnExistUserSuccess, pfAuthErrorCallback);
			//ReadOnlyData에 PF정보를 그대로 받아옴.
			//For Debugging
			Dictionary<string, UserDataRecord> datas = authresult.InfoResultPayload.UserReadOnlyData;

			//필수정보들
			string nickName = authresult.InfoResultPayload.PlayerProfile.DisplayName;
			long currentStage = Convert.ToInt64(datas[key_userStage].Value);
			CurrencyQueryDTO currency = JsonConvert.DeserializeObject<CurrencyQueryDTO>(datas[key_currency].Value);
			UserDataQuery userdata = JsonConvert.DeserializeObject<UserDataQuery>(datas[key_userData].Value);
			UserEnhanceMentQuery enhancement = JsonConvert.DeserializeObject<UserEnhanceMentQuery>(datas[key_userEnhancement].Value);
			List<CharacterDataQuery> characterData = JsonConvert.DeserializeObject<List<CharacterDataQuery>>(datas[key_characterData].Value);

			//가진 장비나 스킬이 없을수도 있음.
			if (datas.ContainsKey(key_skillTreeData))
			{
				List<SkillCode> skillCodes = JsonConvert.DeserializeObject<List<SkillCode>>(datas[key_skillTreeData].Value);
			}
			if (datas.ContainsKey(key_inventoryData))
			{
				List<ItemID> inventory = JsonConvert.DeserializeObject<List<ItemID>>(datas[key_inventoryData].Value);
			}


			//Todo : 유저에서 Inventory랑 SkillTree가 연결되어야함.
			//유저셋팅
			UserManager.Instance.CreateUser(nickName, (eStage)currentStage, userdata, enhancement);
			UserManager.Instance.SetCharacterData(characterData);
			UserManager.Instance.SetWallet(currency);
		}

		void pfAuthErrorCallback(PlayFab.PlayFabError error)
		{
			Debug.Log(error.Error);
		}


		private void OnSignUpUserSucccess(ExecuteFunctionResult result)
		{
			//For Debugging
			string JsonString = JsonConvert.SerializeObject(result.FunctionResult);

			//Todo : 유저 정보 셋팅하기
			UserOnSignUpInitResponseDTO response = JsonConvert.DeserializeObject<UserOnSignUpInitResponseDTO>(JsonString);

		}

		private void OnExistUserSuccess(ExecuteFunctionResult result)
		{
			//For Debugging
			string SessionId = JsonConvert.SerializeObject(result.FunctionResult);
			//Session ID 셋팅 

		}
	}
}

