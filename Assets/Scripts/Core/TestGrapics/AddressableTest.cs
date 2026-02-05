using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public class AddressableTest : MonoBehaviour
{
    AsyncOperationHandle<GameObject> _handle;
    GameObject _prefab;

    private void Awake()
    {
        _handle = Addressables.LoadAssetAsync<GameObject>("Monster");
        _handle.Completed += OnHandles;
    }

    private void OnHandles(AsyncOperationHandle<GameObject> obj)
    {
        _prefab = obj.Result;
    }
    private void Start()
    {

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_prefab != null)
            {
                GameObject.Instantiate(_prefab, Vector3.zero, Quaternion.identity);
            }
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            Addressables.Release(_handle);
            _prefab = null;
        }
    }

}
