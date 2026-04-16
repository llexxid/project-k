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

		//For Test
		CustomLogin,
		DummyLogin,
	}

	public class Authentication
	{
		private string _authToken;
		private string _displayName;
		private eAuthType _type;
		private GetPlayerCombinedInfoRequestParams _infoRequestParams;
		//�ϵ��ڵ� �ص� �ǳ�?
		private readonly string _webClientId = "222024339558-o157jmef5jhip2s9vtfo3kto1ojfpejf.apps.googleusercontent.com";

		private readonly string key_userStage = "UserCurrentStage";
		private readonly string key_userData = "UserData";
		private readonly string key_userEnhancement = "UserEnhanceMent";
		private readonly string key_characterData = "CharacterData";
		private readonly string key_skillTreeData = "SkillTreeData";
		private readonly string key_inventoryData = "Inventory";
		private readonly string key_currency = "Currency";
		private readonly string key_jobTree = "JobTree";


		private readonly string DummyUser1 = "516A4AAD45F1CC09";

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

			_infoRequestParams = new GetPlayerCombinedInfoRequestParams
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
					key_jobTree,
					//MailSlot
					//
				},
				GetPlayerProfile = true,
			};
		}

		public void Authenticate(eAuthType type)
		{
			//���� �α��� �� Ÿ�� �����س��� (���� ������)
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
			LoginWithCustomIDRequest req = new LoginWithCustomIDRequest
			{
				CustomId = "516A4AAD45F1CC09",
				InfoRequestParameters = _infoRequestParams,
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
				return;
			}
			else if (task.IsCanceled)
			{
				return;
			}
			Debug.Log("[GoogleAuth] Google Auth Success");
			_authToken = task.Result.AuthCode;
			LoginWithGoogleAccountRequest pfLoginRequest = new LoginWithGoogleAccountRequest
			{
				TitleId = PlayFabSettings.TitleId,
				ServerAuthCode = _authToken,
				InfoRequestParameters = _infoRequestParams,
				CreateAccount = true
			};

			PlayFabClientAPI.LoginWithGoogleAccount(
				request: pfLoginRequest,
				pfAuthSuccessCallback,
				pfAuthErrorCallback
				);
		}

		//Test��
		private void pfAuthSuccessTest(LoginResult result)
		{
			NetworkManager.Instance.SetSessionID(result.PlayFabId);
			NetworkManager.Instance.SetSessionTicket(result.SessionTicket);

			LoginLogic(result);
		}

		private void pfAuthSuccessCallback(LoginResult authresult)
		{
			NetworkManager.Instance.SetSessionID(authresult.PlayFabId);
			NetworkManager.Instance.SetSessionTicket(authresult.SessionTicket);
			Debug.Log("[Google Login Success Callback]");
			if (authresult.NewlyCreated == true)
			{
				Debug.Log("[First Time Sign Up Account]");
				string uuid = Guid.NewGuid().ToString().Substring(0, 18);
				NetworkManager.Instance.CheckDuplicatedNickName(uuid, duplicatedNameSuccessCallback, pfAuthErrorCallback);
				return;
			}

			string nickname = authresult.InfoResultPayload.PlayerProfile.DisplayName;
			if (nickname.Length == 0)
			{
				NetworkManager.Instance.OnSignUpInitUser(OnSignUpUserSucccess, pfAuthErrorCallback);
				return;
			}

			LoginLogic(authresult);
		}

		void pfAuthErrorCallback(PlayFab.PlayFabError error)
		{
			Debug.Log(error.Error);
		}

		private void OnSignUpUserSucccess(ExecuteFunctionResult result)
		{
			string json = JsonConvert.SerializeObject(result.FunctionResult);
			UserOnSignUpInitResponseDTO userSignUpDTO = JsonConvert.DeserializeObject<UserOnSignUpInitResponseDTO>(json);

			NetworkManager.Instance.SetSessionGUID(userSignUpDTO.SessionGUID);
			UserManager.Instance.CreateUser(
				_displayName,
				(eStage)userSignUpDTO.CurrentStage,
				userSignUpDTO.Exp,
				userSignUpDTO.KillScore,
				userSignUpDTO.Level,
				(ulong)userSignUpDTO.EnchantHPCount,
				(ulong)userSignUpDTO.EnchantATKCount);
			UserManager.Instance.SetCharacterData(userSignUpDTO.CharacterDatas);
			UserManager.Instance.SetWallet(userSignUpDTO.Currency);

			GameManager.Instance.LoadAsyncScene(eSceneType.main);
		}

		private void OnExistUserSuccess(ExecuteFunctionResult result)
		{
			//For Debugging
			string SessionId = JsonConvert.SerializeObject(result.FunctionResult);
			OnAuthInitResponseDTO responsedto = JsonConvert.DeserializeObject<OnAuthInitResponseDTO>(SessionId);
			NetworkManager.Instance.SetSessionGUID(responsedto.SessionID);
			//Session ID ���� 
		}
		private void duplicatedNameSuccessCallback(UpdateUserTitleDisplayNameResult result)
		{
			_displayName = result.DisplayName;
			NetworkManager.Instance.OnSignUpInitUser(OnSignUpUserSucccess, pfAuthErrorCallback);
		}
		private void LoginLogic(LoginResult result)
		{
			NetworkManager.Instance.OnExistUserInit(OnExistUserSuccess, pfAuthErrorCallback);

			Dictionary<string, UserDataRecord> datas = result.InfoResultPayload.UserReadOnlyData;
			//NetworkManager.Instance.OnSignUpInitUser(callbacks, pfAuthErrorCallback);

			string nickName = result.InfoResultPayload.PlayerProfile.DisplayName;
			long currentStage = Convert.ToInt64(datas[key_userStage].Value);
			CurrencyQueryDTO currency = JsonConvert.DeserializeObject<CurrencyQueryDTO>(datas[key_currency].Value);
			UserDataQuery userdata = JsonConvert.DeserializeObject<UserDataQuery>(datas[key_userData].Value);
			UserEnhanceMentQuery enhancement = JsonConvert.DeserializeObject<UserEnhanceMentQuery>(datas[key_userEnhancement].Value);
			List<CharacterDataQuery> characterData = JsonConvert.DeserializeObject<List<CharacterDataQuery>>(datas[key_characterData].Value);

			if (datas.ContainsKey(key_skillTreeData))
			{
				SkillTreeDTO skillCodes = JsonConvert.DeserializeObject<SkillTreeDTO>(datas[key_skillTreeData].Value);
				if (skillCodes?.SkillTrees != null)
					UserManager.Instance.SetSkillTreeData(skillCodes.SkillTrees);
			}
			if (datas.ContainsKey(key_inventoryData))
			{
				InventoryQueryDTO inventory = JsonConvert.DeserializeObject<InventoryQueryDTO>(datas[key_inventoryData].Value);
				//Todo : 여기에서 Inventory와 SkillTree가 적용되어야함.
				UserManager.Instance.SetInventoryData(inventory.Items);
			}
			if (datas.ContainsKey(key_jobTree))
			{
				List<JobTreeQuery> jobTrees = JsonConvert.DeserializeObject<List<JobTreeQuery>>(datas[key_jobTree].Value);
				UserManager.Instance.SetJobTreeData(jobTrees);
			}

			UserManager.Instance.CreateUser(nickName, (eStage)currentStage, userdata, enhancement);
			UserManager.Instance.SetCharacterData(characterData);
			UserManager.Instance.SetWallet(currency);

			GameManager.Instance.LoadAsyncScene(eSceneType.main);
		}
	}
}

