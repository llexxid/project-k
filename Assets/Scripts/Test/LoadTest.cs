using Scripts.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GameManager.Instance.LoadAsyncScene(eSceneType.JunGiScene);
        }
    }
}
