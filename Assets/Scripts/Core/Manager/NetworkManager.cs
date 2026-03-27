using Scripts.Users;
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
	}
}

