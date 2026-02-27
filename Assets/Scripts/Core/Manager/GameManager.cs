using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using ExcelDataReader;
using Scripts.Core.SO;
using Scripts.Core.Utils;
using Scripts.Monster;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Timers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Scripts.Core
{
	using static Scripts.Monster.Monster;
	using Monster = Scripts.Monster.Monster;
	public class GameManager : MonoBehaviour
	{
		public static GameManager Instance;

		[Header("Scene Name Mapping")]
		[SerializeField] private string bootstrapSceneName = "bootstrap";
		[SerializeField] private string titleSceneName = "title";
		[SerializeField] private string mainSceneName = "main";
		[SerializeField] private string dungeonSceneName = "dungeon";

		[Header("Async Loading")]
		[SerializeField] private float minLoadingSeconds = 0f;

		//씬 , 스테이지와 관련된 몬스터, SFX,VFX정보들
		[SerializeField]
		MonsterMetaSO _monsterMetaDataSO;
		[SerializeField]
		SceneSFXMetaSO _SceneSFXMetaSO;
		[SerializeField]
		SceneVFXMetaSO _SceneVFXMetaSO;

		public event Action<eSceneType> SceneLoadStarted;
		public event Action<eSceneType> SceneLoadFinished;
		public event Action<eSceneType, float> SceneLoadProgress;

		private CancellationTokenSource _token;
		private CancellationTokenSource _LoadStageToken;

		private AsyncOperation _UnitySceneLoaderOp;

		private AsyncOperationHandle<IList<GameObject>> _VFXSceneHandle;
		private AsyncOperationHandle<IList<AudioClip>> _SFXSceneHandle;
		private AsyncOperationHandle<IList<GameObject>> _StageLoaderHandle;

		private AsyncOperationHandle<IList<GameObject>> _VFXMonsterHandle;
		private AsyncOperationHandle<IList<AudioClip>> _SFXMonsterHandle;


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
		private void OnEnable()
		{
			SceneManager.sceneLoaded += OnSceneLoaded;
		}
		private void OnDestroy()
		{
			if (_token != null)
			{
				_token.Cancel();
				_token.Dispose();
				_token = null;
			}
		}

		private void Init()
		{
			_token = new CancellationTokenSource();
			_LoadStageToken = new CancellationTokenSource();

			_monsterMetaDataSO.Init();

			_SceneSFXMetaSO.Init();
			_SceneVFXMetaSO.Init();
		}

		private string GetSceneName(eSceneType type)
		{
			switch (type)
			{
				case eSceneType.bootstrap: return bootstrapSceneName;
				case eSceneType.title: return titleSceneName;
				case eSceneType.main: return mainSceneName;
				case eSceneType.dungeon: return dungeonSceneName;
				default:
					return type.ToString();
			}
		}

		public void ReloadCurrentScene()
		{
			Time.timeScale = 1f;

			var current = SceneManager.GetActiveScene().name;
			SceneManager.LoadScene(current);
		}
		// 동기 로드
		public void LoadScene(eSceneType type)
		{
			Time.timeScale = 1f;

			SceneLoadStarted?.Invoke(type);

			string sceneName = GetSceneName(type);
			SceneManager.LoadScene(sceneName);

			// 동기 로드는 여기 시점에 이미 로드 완료로 간주
			SceneLoadProgress?.Invoke(type, 1f);
			SceneLoadFinished?.Invoke(type);
		}
		//씬 전환하는 기능
		public void LoadAsyncScene(eSceneType type)
		{
			Time.timeScale = 1f;

			//기존의 핸들이 있다면, 핸들들을 Release시켜줘야함.
			CheckHandle();
			LoadingScene(type).Forget();
		}
		//Stage를 전환하는 기능
		public void LoadAsyncStage(eStage stage)
		{
			Time.timeScale = 1f;

			//몬스터들 정보 정도만 Clear. VFX는 어차피 그렇게 많지 않음.
			//VFX Manager자체를 초기화 시키거나, stage에 요청한 VFX들을 
			//Handle , Pool, effectCache에서 delete해야하는데, delete하는 작업이 더 느릴거 같음.
			if (_StageLoaderHandle.IsValid())
			{
				StageManager.Instance.Clear();
				_StageLoaderHandle = default;
			}
			LoadStage(stage).Forget();
		}

		private void CheckHandle()
		{
			//뭔가 값이 있다면 해제
			if (_StageLoaderHandle.IsValid())
			{
				StageManager.Instance.Clear();
				_StageLoaderHandle = default;
			}

			if (_VFXSceneHandle.IsValid() || _VFXMonsterHandle.IsValid())
			{
				VFXManager.Instance.Clear();
				_VFXSceneHandle = default;
				_VFXMonsterHandle = default;
			}

			if (_SFXSceneHandle.IsValid() || _SFXMonsterHandle.IsValid())
			{
				SFXManager.Instance.Clear();
				_SFXSceneHandle = default;
				_SFXMonsterHandle = default;
			}
		}

		private async UniTaskVoid LoadingScene(eSceneType type)
		{
			if (_token == null) _token = new CancellationTokenSource();

			SceneLoadStarted?.Invoke(type);

			string sceneName = GetSceneName(type);
			float startRealtime = Time.realtimeSinceStartup;

			_UnitySceneLoaderOp = SceneManager.LoadSceneAsync(sceneName);
			_UnitySceneLoaderOp.allowSceneActivation = false;


			// User의 현재 스테이지 정보를 가져와서 Load준비해야함.
			if (type == eSceneType.main)
			{
				eStage currentStage = UserManager.Instance.GetUserCurrentStage();
				_StageLoaderHandle = StageManager.Instance.PreLoadAssets(currentStage);
				LoadResourceInMonster(currentStage);

				//Player에 필요한 VFX,SFX 로딩
			}

			//각 씬에 필요한 VFX,SFX 로딩
			List<eVFXType> vfxList;
			List<eSFXType> sfxList;
			bool IsVFXLoadNeed = _SceneVFXMetaSO.TryGetVFXTypeList(type, out vfxList);
			bool IsSFXLoadNeed = _SceneSFXMetaSO.TryGetSFXTypeList(type, out sfxList);

			if (IsVFXLoadNeed)
			{
				_VFXSceneHandle = VFXManager.Instance.PreLoadVFX((ulong)type, vfxList.ToArray());
			}
			if (IsSFXLoadNeed)
			{
				_SFXSceneHandle = SFXManager.Instance.PreLoadSFX((ulong)type, sfxList.ToArray());
			}


			while (true)
			{
				bool stageDone = _StageLoaderHandle.IsDone;
				bool vfxDone = !_VFXSceneHandle.IsValid() | _VFXSceneHandle.IsDone;
				bool sfxDone = !_SFXSceneHandle.IsValid() | _SFXSceneHandle.IsDone;
				bool vfxMonsterDone = !_VFXMonsterHandle.IsValid() | _VFXMonsterHandle.IsDone;
				bool sfxMonsterDone = !_SFXMonsterHandle.IsValid() | _SFXMonsterHandle.IsDone;

				//로딩창 Scroll조절
				//timer += Time.unscaledDeltaTime;
				//scrollbar.fillAmount = Mathf.Lerp(0.9f, 1f, timer);

				float normalized = Mathf.Clamp01(_UnitySceneLoaderOp.progress / 0.9f);

				if (minLoadingSeconds > 0f)
				{
					float t = Mathf.Clamp01((Time.realtimeSinceStartup - startRealtime) / minLoadingSeconds);
					normalized = Mathf.Min(normalized, t);
				}

				SceneLoadProgress?.Invoke(type, normalized);

				if (stageDone &&
					vfxDone &&
					sfxDone &&
					vfxMonsterDone &&
					sfxMonsterDone &&
					_UnitySceneLoaderOp.progress >= 0.9f)
				{
					if (minLoadingSeconds <= 0f || (Time.realtimeSinceStartup - startRealtime) >= minLoadingSeconds)
					{
						break;
					}
				}

				//스크롤바가 다 채워졌다면, SceneActive하기.
				await UniTask.Yield(_token.Token);
			}

			_UnitySceneLoaderOp.allowSceneActivation = true;

			// 실제 씬 활성화 완료까지 대기
			while (!_UnitySceneLoaderOp.isDone)
			{
				await UniTask.Yield(_token.Token);
			}

			SceneLoadProgress?.Invoke(type, 1f);
			SceneLoadFinished?.Invoke(type);
			//임시패치
		}
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			//MainScene전환시
			if (scene.name == "main")
			{
				Debug.Log("Scene전환!");
				Vector3 pos = new Vector3(5, 0, 0);
				Monster mon;

				MonsterSpawner.Instance.OnEnterScene();
				VFXManager.Instance.OnEnterScene();

				//MainScene진입시 SpawnLogic
				//Stage정보를 가져와서, 해당 스테이지 정보에 맞게 스폰을 요청하게 됨.
				//아래는 테스트용
				MonsterSpawner.Instance.SpawnMonster(eMonsterType.MON_ORC, pos, Quaternion.identity, out mon);
				_monsterMetaDataSO.TryGetMonsterInfo(eMonsterType.MON_ORC, out MonsterInfo monInfo);
				MonsterStat stat = new MonsterStat(monInfo._baseHp, 0, (ulong)monInfo._baseAtk, monInfo._baseMoveSpeed, monInfo._baseAtkSpeed);
				mon.Init(eMonsterType.MON_ORC, stat, monInfo._dropTableNumber);
			}
		}

		/// <summary>
		/// 리소스가 바뀌는 스테이지 Load함수.
		/// </summary>
		/// <param name="stage"></param>
		/// <returns></returns>
		private async UniTaskVoid LoadStage(eStage stage)
		{
			float startRealtime = Time.realtimeSinceStartup;
			/*
            _StageLoaderHandle = StageManager.Instance.PreLoadAssets(stage);
            LoadResourceInMonster(stage);
            */

			while (true)
			{
				bool stageDone = !_StageLoaderHandle.IsValid() || _StageLoaderHandle.IsDone;
				bool vfxDone = !_VFXMonsterHandle.IsValid() || _VFXMonsterHandle.IsDone;
				bool sfxDone = !_SFXMonsterHandle.IsValid() || _SFXMonsterHandle.IsDone;

				//로딩창 or 화면 검게 Fade Out - FadeIn 연출
				//timer += Time.unscaledDeltaTime;
				//scrollbar.fillAmount = Mathf.Lerp(0.9f, 1f, timer);

				float normalized = Mathf.Clamp01(_UnitySceneLoaderOp.progress / 0.9f);

				// 최소 로딩 시간 옵션
				if (minLoadingSeconds > 0f)
				{
					float t = Mathf.Clamp01((Time.realtimeSinceStartup - startRealtime) / minLoadingSeconds);
					normalized = Mathf.Min(normalized, t);
				}

				// (에셋 로딩 완료) + (씬 로딩 0.9 도달) + (최소 로딩 시간 충족) => 씬 활성화로 넘어감
				if (stageDone && vfxDone && sfxDone && _UnitySceneLoaderOp.progress >= 0.9f)
				{
					if (minLoadingSeconds <= 0f || (Time.realtimeSinceStartup - startRealtime) >= minLoadingSeconds)
					{
						break;
					}
				}

				//스크롤바가 다 채워졌다면, SceneActive하기.
				await UniTask.Yield(_LoadStageToken.Token);
			}
		}
		private void LoadResourceInMonster(eStage stage)
		{
			//Stage에 필요한 몬스터 프리펩들 로딩
			_StageLoaderHandle = StageManager.Instance.PreLoadAssets(stage);

			//스테이지에 필요한 VFX로딩
			List<eMonsterType> monList = StageManager.Instance.GetStageMonsterTypes(stage);
			GetVFXListIds(monList, out eVFXType[] vfxList);
			GetSFXListIds(monList, out eSFXType[] sfxList);

			_VFXMonsterHandle = VFXManager.Instance.PreLoadVFX((ulong)stage, vfxList);
			_SFXMonsterHandle = SFXManager.Instance.PreLoadSFX((ulong)stage, sfxList);

		}
		private void GetSFXListIds(List<eMonsterType> monList, out eSFXType[] Ids)
		{
			int totalArrayLength = 0;
			//각 스테이지의 몬스터 목록의 SFXList담기
			List<eSFXType[]> neededSFXs = new List<eSFXType[]>();

			for (int i = 0; i < monList.Count; i++)
			{
				List<eSFXType> ret;
				bool IsSFXListNeed = _monsterMetaDataSO.TryGetSFXList(monList[i], out ret);
				if (!IsSFXListNeed)
				{
					continue;
				}
				eSFXType[] tmp = ret.ToArray();
				neededSFXs.Add(tmp);
				totalArrayLength += ret.Count;
			}

			//1차원 배열로 만들기
			Ids = new eSFXType[totalArrayLength];
			for (int i = 0; i < neededSFXs.Count; i++)
			{
				for (int j = 0; j < neededSFXs[i].Length; j++)
				{
					Ids[i] = neededSFXs[i][j];
				}
			}
		}
		private void GetVFXListIds(List<eMonsterType> monList, out eVFXType[] Ids)
		{
			int totalArrayLength = 0;
			//각 스테이지의 몬스터 목록의 VFXList담기
			List<eVFXType[]> neededVFXs = new List<eVFXType[]>();

			for (int i = 0; i < monList.Count; i++)
			{
				List<eVFXType> ret;
				bool IsVFXListNeed = _monsterMetaDataSO.TryGetVFXList(monList[i], out ret);
				if (!IsVFXListNeed)
				{
					continue;
				}
				eVFXType[] tmp = ret.ToArray();
				neededVFXs.Add(tmp);
				totalArrayLength += ret.Count;
			}


			Ids = new eVFXType[totalArrayLength];
			for (int i = 0; i < neededVFXs.Count; i++)
			{
				for (int j = 0; j < neededVFXs[i].Length; j++)
				{
					Ids[i] = neededVFXs[i][j];
				}
			}
		}
	}
}