using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Scripts.Core;
using Scripts.Core.SO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Scene-owned background. Holds one selected preset; releases its assets on scene exit.</summary>
public sealed class StageBackgroundController : MonoBehaviour
{
    [SerializeField] private GameObject[] _legacyBackgrounds = Array.Empty<GameObject>();
    private readonly Dictionary<string, string> _previous = new(StringComparer.Ordinal);
    private GameObject _instance;
    private AsyncOperationHandle<GameObject> _handle;
    private int _request;
    public string CurrentPresetId { get; private set; }
    public string CurrentPoolId { get; private set; }

    public async UniTask<bool> ApplyAsync(StageDefinition definition, StageDatabaseSO database)
    {
        int request = ++_request;
        string pool = definition.Encounter.EnvironmentPoolId;
        var choices = new List<StageEnvironmentPreset>();
        foreach (var item in database.EnvironmentPresets) if (item.PoolId == pool && item.Weight > 0) choices.Add(item);
        if (choices.Count == 0) throw new InvalidOperationException("Empty stage environment pool: " + pool);
        _previous.TryGetValue(pool, out var previous);
        if (choices.Count > 1) choices.RemoveAll(x => x.PresetId == previous);
        int total = 0; foreach (var item in choices) total = checked(total + item.Weight);
        int ticket = UnityEngine.Random.Range(0, total); var selected = choices[0];
        foreach (var item in choices) { selected = item; ticket -= item.Weight; if (ticket < 0) break; }
        var pending = Addressables.LoadAssetAsync<GameObject>(selected.PresetId);
        bool adopted = false;
        try
        {
            var prefab = await pending.Task;
            if (this == null || request != _request) return false;
            if (prefab == null) throw new InvalidOperationException("Stage environment load failed: " + selected.PresetId);
            var next = Instantiate(prefab, transform, false);
            next.name = selected.PresetId;
            if (_instance != null) { _instance.SetActive(false); Destroy(_instance); }
            if (_handle.IsValid()) Addressables.Release(_handle);
            _instance = next; _handle = pending; adopted = true;
            foreach (var background in _legacyBackgrounds) if (background != null) background.SetActive(false);
            _previous[pool] = selected.PresetId;
            CurrentPoolId = pool; CurrentPresetId = selected.PresetId;
            return true;
        }
        finally { if (!adopted && pending.IsValid()) Addressables.Release(pending); }
    }
    private void OnDestroy()
    {
        _request++;
        if (_instance != null) Destroy(_instance);
        if (_handle.IsValid()) Addressables.Release(_handle);
    }
}
