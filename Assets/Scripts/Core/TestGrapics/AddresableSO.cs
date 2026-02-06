using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine;

[CreateAssetMenu(fileName = "AddressableSO", menuName = "ScriptableObjects/AddressableSO", order = 1)]
public class AddresableSO : ScriptableObject
{
    int _id;
    AssetReference _assetRef;
}
