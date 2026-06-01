using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using PlayFab.Internal;
using Scripts.Server.Auth;
using Scripts.Server.DTO;
using Scripts.Users;
using System;
using System.Collections;
using System.Collections.Generic;
using Scripts.Core.Offline;
using Scripts.Core.Utils;
using Unity.Jobs;
using UnityEngine;

namespace Scripts.Core.Manager
{
	//.
	using ItemCode = Scripts.Server.DTO.ItemCode;
	public class NetworkManager : MonoBehaviour
	{
		public static NetworkManager Instance;
		private string _playFabId;
		private string _sessionTicket;
		private string _sessionGUID;

		private Authentication _AuthComponent;

		[OfflineMode] private bool isOfflineMode;
		
		private void Awake()
		{
			if (Instance == null)
			{
				Instance = this;
				Init();
				DontDestroyOnLoad(gameObject);
				return;
			}

			Destroy(gameObject);
		}

		private void Init()
		{
			_AuthComponent = new Authentication();
		}

		[OfflineMode]
		public void SetOfflineMode(bool value)
		{
			isOfflineMode = value;
		}

		public void SetSessionGUID(string guid)
		{
			_sessionGUID = guid;
		}

		public void SetSessionID(string id)
		{
			_playFabId = id;
		}
		public void SetSessionTicket(string ticket)
		{
			_sessionTicket = ticket;
		}
		public string GetSessionID()
		{
			return _playFabId;
		}
		public string GetSessionTicket()
		{
			return _sessionTicket;
		}
		//NetWork Message�� ���⼭ �Լ� Call�� �ҷ��ٰ���.

		//�г��� �ߺ�üũ
		public void CheckDuplicatedNickName(string nickname, Action<UpdateUserTitleDisplayNameResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			UpdateUserTitleDisplayNameRequest req = new UpdateUserTitleDisplayNameRequest
			{
				DisplayName = nickname,
			};

			PlayFabClientAPI.UpdateUserTitleDisplayName(req, successCallback, errorCallback);
			return;
		}
		public void AuthenticateTest()
		{
			_AuthComponent.Authenticate(eAuthType.CustomLogin);
		}

		//For Testing
		public void DummyLogin()
		{
			_AuthComponent.Authenticate(eAuthType.DummyLogin);
		}

		public void Authenticate(eAuthType authType)
		{
			_AuthComponent.Authenticate(authType);
		}

		private void OnError(PlayFab.PlayFabError error)
		{
			Debug.Log(error.ErrorMessage);
			Debug.Log(error.Error);
		}


		//ĳ���� ó�� �����ϴ� �Լ�
		public void OnSignUpInitUser(Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnSignUpInitUser",
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}

		public void OnExistUserInit(Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnExistUserInit",
				FunctionParameter = _sessionGUID,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}

		public void OnHuntReward(List<HuntResult> sendMsg, Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			if (isOfflineMode)
			{
				OfflineTestManager.Instance.ApplyHuntReward(sendMsg);
				return;
			}

			OnRewardRequestDTO request = new OnRewardRequestDTO
			{
				SessionID = _sessionGUID,
				Loots = sendMsg,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnHuntReward",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}

		public void OnStageClear(Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			OnStageClearRequestDTO request = new OnStageClearRequestDTO
			{
				SessionID = _sessionGUID,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnStageClear",
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}

		public void OnGachaEquipmentClick(int count, Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			OnGachaRequestDTO request = new OnGachaRequestDTO
			{
				SessionID = _sessionGUID,
				Count = count,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnGachaEquipmentClassFragment",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}
		
		//Test�ؾ���.
		public void OnGachaSkillClick(int count, Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			OnGachaRequestDTO request = new OnGachaRequestDTO
			{
				SessionID = _sessionGUID,
				Count = count,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnGachaSkillArcaneKnowledge",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}
		public void OnEnchantHp(int count, Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			if (isOfflineMode)
			{
				OfflineTestManager.Instance.ApplyEnchantHp(count);
				return;
			}
			
			OnEnchantRequestDTO request = new OnEnchantRequestDTO
			{
				SessionID = _sessionGUID,
				Count = count,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnEnChantHP",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}
		public void OnEnchantATK(int count, Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
			if (isOfflineMode)
			{
				OfflineTestManager.Instance.ApplyEnchantAtk(count);
				return;
			}
			
			OnEnchantRequestDTO request = new OnEnchantRequestDTO
			{
				SessionID = _sessionGUID,
				Count = count,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnEnChantATK",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}
		
		public void OnEnchantEquipment(ItemCode code, Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
		{
			OnEnchantEquipmentRequestDTO request = new OnEnchantEquipmentRequestDTO
			{
				SessionID = _sessionGUID,
				ItemCode = code,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnEnchantEquipment",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}
		public void OnEnchantSkill(SkillCode code, Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
		{
			OnEnchantSkillRequestDTO request = new OnEnchantSkillRequestDTO
			{
				SessionID = _sessionGUID,
				SkillCode = code,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnEnChantSkill",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};

			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}
		public void OnAwakenSkill(SkillCode code, Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
		{
			OnAwakeningSkillRequestDTO request = new OnAwakeningSkillRequestDTO
			{
				SessionID = _sessionGUID,
				SkillCode = code,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnAwakeningSkill",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};
			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}

		public void OnGetJob(ulong job, int characterIdx, Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
		{
			OnGetJobRequestDTO request = new OnGetJobRequestDTO
			{
				SessionID = _sessionGUID,
				JobCode = job,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnGetJob",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};
			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}
		public void OnChangeJob(ulong job, int characterIdx, Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
		{
			OnGetJobRequestDTO request = new OnGetJobRequestDTO
			{
				SessionID = _sessionGUID,
				JobCode = job,
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnChangeJob",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};
			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}

		//Test
		public void OnSetJobTree(Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
		{
			InitUserRequestDTO request = new InitUserRequestDTO
			{
			};

			ExecuteFunctionRequest cloudFunction = new ExecuteFunctionRequest()
			{
				FunctionName = "OnSetJobTree",
				FunctionParameter = request,
				GeneratePlayStreamEvent = true,
			};
			PlayFabCloudScriptAPI.ExecuteFunction(cloudFunction, successCallback, errorCallback);
		}

		private void OnDuplicatedNickNameCallback(PlayFab.PlayFabError error)
		{
			Debug.Log("�г����� �ߺ��˴ϴ�.");
		}
		private void OnEnableNicknameCallback(UpdateUserTitleDisplayNameResult result)
		{
			Debug.Log("�ߺ��� �г����� �����ϴ�.");
		}


	}
}

