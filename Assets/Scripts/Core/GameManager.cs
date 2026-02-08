using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Scripts.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        public GameObject _loadPannelPrefab;

        private CancellationTokenSource _token;
        private AsyncOperation asyncOp;
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Instance.Init();
                DontDestroyOnLoad(this);
                return;
            }
            Destroy(this);
            return;
        }

        private void Init()
        {
            _token = new CancellationTokenSource();
        }

        //Scene을 넘기는 기능

        //비동기 Loading기능
        public void LoadScene(eSceneType type)
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(type.ToString());
            //SoundManager.instance.ChangeBGM(type.ToString());
        }
        public void ReloadCurrentScene(eSceneType type)
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void LoadAsyncScene(eSceneType type)
        {
            Time.timeScale = 1f;
            LoadingScene(type).Forget();
        }

        private async UniTaskVoid LoadingScene(eSceneType type)
        {
            asyncOp = SceneManager.LoadSceneAsync(type.ToString());
            asyncOp.allowSceneActivation = false;

            //Loading창이 있다면, 여기서 출력
            /*
            GameObject canvas = Instantiate(_loadPannelPrefab, pos, Quaternion.identity);
            Image scrollbar = canvas.GetComponentInChildren<Image>();
            canvas.SetActive(true);
             */

            float timer = 0f;
            while (!asyncOp.isDone)
            {
                if (asyncOp.progress < 0.9f)
                {
                    //스크롤바 움직이기 scrollbar.fillAmount = asyncOp.progress;
                }
                else
                {
                    timer += Time.unscaledDeltaTime;
                    // scrollbar.fillAmount = Mathf.Lerp(0.9f, 1f, timer);
                    /* 스크롤바가 1 이상인 경우
                    if (scrollbar.fillAmount >= 1f)
                    {
                        asyncOp.allowSceneActivation = true;
                        Destroy(canvas);
                        //SoundManager.instance.ChangeBGM(scene.ToString());
                        OnEnterScene.Invoke();
                        return;
                    }
                    */

                }
                await UniTask.Yield(_token.Token);
            }
        }
    }
}

