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
		private eAuthType _type;
		//�ϵ��ڵ� �ص� �ǳ�?
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
				case eAuthType.DummyLogin:
					DummyLogin();
					break;
				default:
					break;
			}
		}

		private void DummyLogin()
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
					key_currency,
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
			PlayFabClientAPI.LoginWithCustomID(req, OnDummyLoginSuccess, pfAuthErrorCallback);
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
					key_currency,
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
				Debug.LogError("���� �α��� ����: " + task.Exception);
				return;
			}
			else if (task.IsCanceled)
			{
				Debug.Log("���� �α����� ��ҵǾ����ϴ�.");
				return;
			}

			//PlayFab�� �α����� ��û�� ��, � ������ ���� ��û�� ���ΰ�?
			//�κ��丮 ������,���� ������, ĳ���� ������, ��ȭ��ġ, ��ȭ
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

		private void callbacks(ExecuteFunctionResult result)
		{
			Debug.Log("����");
		}

		//Test��
		private void pfAuthSuccessTest(LoginResult result)
		{
			//��������
			NetworkManager.Instance.SetSessionID(result.PlayFabId);
			NetworkManager.Instance.SetSessionTicket(result.SessionTicket);

			NetworkManager.Instance.OnExistUserInit(OnExistUserSuccess, pfAuthErrorCallback);

			Dictionary<string, UserDataRecord> datas = result.InfoResultPayload.UserReadOnlyData;
			//NetworkManager.Instance.OnSignUpInitUser(callbacks, pfAuthErrorCallback);
			//�ʼ�������
			string nickName = result.InfoResultPayload.PlayerProfile.DisplayName;
			long currentStage = Convert.ToInt64(datas[key_userStage].Value);
			CurrencyQueryDTO currency = JsonConvert.DeserializeObject<CurrencyQueryDTO>(datas[key_currency].Value);
			UserDataQuery userdata = JsonConvert.DeserializeObject<UserDataQuery>(datas[key_userData].Value);
			UserEnhanceMentQuery enhancement = JsonConvert.DeserializeObject<UserEnhanceMentQuery>(datas[key_userEnhancement].Value);
			List<CharacterDataQuery> characterData = JsonConvert.DeserializeObject<List<CharacterDataQuery>>(datas[key_characterData].Value);
		
			//���� ��� ��ų�� �������� ����.
			if (datas.ContainsKey(key_skillTreeData))
			{
				List<SkillCode> skillCodes = JsonConvert.DeserializeObject<List<SkillCode>>(datas[key_skillTreeData].Value);
				//Todo : �������� Inventory�� SkillTree�� ����Ǿ����.
			}
			if (datas.ContainsKey(key_inventoryData))
			{
				List<ItemID> inventory = JsonConvert.DeserializeObject<List<ItemID>>(datas[key_inventoryData].Value);
				//Todo : 여기에서 Inventory와 SkillTree가 적용되어야함.
				UserManager.Instance.SetInventoryData(inventory);
			}

			//��������
			UserManager.Instance.CreateUser(nickName,(eStage)currentStage, userdata, enhancement);
			UserManager.Instance.SetCharacterData(characterData);
			UserManager.Instance.SetWallet(currency);

			GameManager.Instance.LoadAsyncScene(eSceneType.main);
		}
		//�갡 Release�� ���۵� Google Login�Լ�
		private void pfAuthSuccessCallback(LoginResult authresult)
		{
			//User�� playFabId ���� / SessionTicket���� 
			//User�� SEssion�� �����ִ� ������ �ϴϱ�, User�� �����ϴ� ������
			// ó�� �����ϴ°Ÿ�, �����͸� ���� �ް� -> �г��� ���� -> ���� ���� ���� 
			// ��, user�� Session�� ����������.
			NetworkManager.Instance.SetSessionID(authresult.PlayFabId);
			NetworkManager.Instance.SetSessionTicket(authresult.SessionTicket);
			if (authresult.NewlyCreated == true)
			{
				//TODO : �г��� ���� UI ����
				// �г��� ���� UI���� NetworkManager�� ���� ���� ���� �Լ��� �θ��� ��.
				return;
			}

			//�г��� �ߺ�üũ�ϰ�, Ȯ�ι�ư�� �ȴ����� ���� ����.
			string nickname = authresult.InfoResultPayload.PlayerProfile.DisplayName;
			if (nickname.Length == 0)
			{
				NetworkManager.Instance.OnSignUpInitUser(OnSignUpUserSucccess, pfAuthErrorCallback);
				return;
			}

			//���������� �α����� ���
			NetworkManager.Instance.OnExistUserInit(OnExistUserSuccess, pfAuthErrorCallback);
			//ReadOnlyData�� PF������ �״�� �޾ƿ�.
			//For Debugging
			Dictionary<string, UserDataRecord> datas = authresult.InfoResultPayload.UserReadOnlyData;

			//�ʼ�������
			string nickName = authresult.InfoResultPayload.PlayerProfile.DisplayName;
			long currentStage = Convert.ToInt64(datas[key_userStage].Value);
			CurrencyQueryDTO currency = JsonConvert.DeserializeObject<CurrencyQueryDTO>(datas[key_currency].Value);
			UserDataQuery userdata = JsonConvert.DeserializeObject<UserDataQuery>(datas[key_userData].Value);
			UserEnhanceMentQuery enhancement = JsonConvert.DeserializeObject<UserEnhanceMentQuery>(datas[key_userEnhancement].Value);
			List<CharacterDataQuery> characterData = JsonConvert.DeserializeObject<List<CharacterDataQuery>>(datas[key_characterData].Value);

			//���� ��� ��ų�� �������� ����.
			if (datas.ContainsKey(key_skillTreeData))
			{
				List<SkillCode> skillCodes = JsonConvert.DeserializeObject<List<SkillCode>>(datas[key_skillTreeData].Value);
				//Todo : �������� Inventory�� SkillTree�� ����Ǿ����.
			}
			if (datas.ContainsKey(key_inventoryData))
			{
				List<ItemID> inventory = JsonConvert.DeserializeObject<List<ItemID>>(datas[key_inventoryData].Value);
				//Todo : 여기에서 Inventory와 SkillTree가 적용되어야함.
				UserManager.Instance.SetInventoryData(inventory);
			}

			//��������
			UserManager.Instance.CreateUser(nickName, (eStage)currentStage, userdata, enhancement);
			UserManager.Instance.SetCharacterData(characterData);
			UserManager.Instance.SetWallet(currency);

			GameManager.Instance.LoadAsyncScene(eSceneType.main);
		}

		void pfAuthErrorCallback(PlayFab.PlayFabError error)
		{
			Debug.Log(error.Error);
		}

		void OnDummyLoginSuccess(LoginResult result)
		{
			NetworkManager.Instance.SetSessionID(result.PlayFabId);
			NetworkManager.Instance.SetSessionTicket(result.SessionTicket);

			NetworkManager.Instance.OnExistUserInit(OnExistUserSuccess, pfAuthErrorCallback);

			Debug.Log("DummyLogin����!");
		}

		private void OnSignUpUserSucccess(ExecuteFunctionResult result)
		{
			
		}

		private void OnExistUserSuccess(ExecuteFunctionResult result)
		{
			//For Debugging
			Debug.Log("Redis�� ���� ������û ����!");
			string SessionId = JsonConvert.SerializeObject(result.FunctionResult);
			OnAuthInitResponseDTO responsedto = JsonConvert.DeserializeObject<OnAuthInitResponseDTO>(SessionId);
			Debug.Log($"SessionID : {responsedto.SessionID}");
			NetworkManager.Instance.SetSessionGUID(responsedto.SessionID);
			//Session ID ���� 

		}
	}
}

