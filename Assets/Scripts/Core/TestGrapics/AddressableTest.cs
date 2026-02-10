using Cysharp.Threading.Tasks;
using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public class AddressableTest : MonoBehaviour
{
    AsyncOperationHandle<GameObject> _handle;
    AsyncOperationHandle<IList<GameObject>> _handles;
    GameObject _prefab;

    private void Awake()
    {
        //_handle = Addressables.LoadAssetAsync<GameObject>("Monster");
        //_handle.Completed += OnHandles;



    }

    private void OnHandles(AsyncOperationHandle<GameObject> obj)
    {
        _prefab = obj.Result;
    }
    private void Start()
    {
    }
    private async void LoadAssets()
    {
        string[] ids = { "Monster", "1", "3", "2", "FireBall" };
        List<string> idList;
        idList = ids.ToList<string>();

        string groupKey = "Monster";
        _handles = Addressables.LoadAssetsAsync<GameObject>(groupKey, (loaded) => { });

        IList<GameObject> objList = await _handles.Task;

        foreach (GameObject obj in objList)
        {
            Debug.Log(obj.name);
        }
    }


    private void Update()
    {

    }

}
