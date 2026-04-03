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
using UnityEngine;

namespace Scripts.Core.Manager
{
	public class NetworkManager : MonoBehaviour
	{
		public static NetworkManager Instance;
		private string _playFabId;
		private string _sessionTicket;
		private string _sessionGUID;

		private Authentication _AuthComponent;
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
		//NetWork Message는 여기서 함수 Call로 불러줄거임.

		//닉네임 중복체크
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

		public void Authenticate(eAuthType authType)
		{
			_AuthComponent.Authenticate(authType);
		}

		private void OnError(PlayFab.PlayFabError error)
		{
			Debug.Log(error.ErrorMessage);
			Debug.Log(error.Error);
		}


		//캐릭터 처음 생성하는 함수
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

		public void OnHuntReward(List<RewardCode> sendMsg, Action<ExecuteFunctionResult> successCallback, Action<PlayFab.PlayFabError> errorCallback)
		{
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

		private void OnDuplicatedNickNameCallback(PlayFab.PlayFabError error)
		{
			Debug.Log("닉네임이 중복됩니다.");
		}
		private void OnEnableNicknameCallback(UpdateUserTitleDisplayNameResult result)
		{
			Debug.Log("중복된 닉네임이 없습니다.");
		}


	}
}

