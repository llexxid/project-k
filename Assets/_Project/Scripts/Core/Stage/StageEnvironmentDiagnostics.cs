#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Linq;
using KingdomIdle.Combat;
using Scripts.Core;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

/// <summary>Read-only live background evidence for Editor and opt-in device review.</summary>
public static class StageEnvironmentDiagnostics
{
    [Serializable]
    public sealed class RendererEvidence
    {
        public string name, type, sortingLayer, shader, outerSortingGroup;
        public int layerValue, order, materialRenderQueue, distinctTileAssets;
        public bool depthWritePropertyAvailable;
        public float? materialDepthWrite;
    }

    [Serializable]
    public sealed class Evidence
    {
        // Preserve the existing device snapshot names consumed by environment_revision.py.
        public string CurrentPoolId, CurrentPresetId;
        public string orderingEvidenceScope = "Effective SortingGroup/order, material queues and exposed ZWrite only; actual opaque-pixel coverage is verified separately on the GPU.";
        public int renderers, activeColliders, activeAnimators, liveBodyRenderers, groundEffectRenderers;
        public bool? backgroundBehindBodies, backgroundBehindGroundEffects;
        public float maximumAbsoluteFootX, maximumBodyRadius;
        public RendererEvidence[] rendererEvidence;
    }

    public static Evidence Capture(StageBackgroundController background)
    {
        if (background == null) return null;
        var scenery = background.GetComponentsInChildren<Renderer>()
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var players = CombatMotion.Players.Where(p => p != null && p.isActiveAndEnabled && !p.IsDead).ToArray();
        var monsters = CombatMotion.Monsters.Where(m => m != null && m.isActiveAndEnabled && m.MonAction != eMonsterAction.Dead).ToArray();
        var bodies = players.Select(p => p.GetComponent<SpriteRenderer>())
            .Concat(monsters.Select(m => m.GetComponentInChildren<SpriteRenderer>()))
            .Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var groundEffects = UnityEngine.Object.FindObjectsByType<PooledSpellVfx>(FindObjectsSortMode.None)
            .Where(v => v.isActiveAndEnabled).SelectMany(v => v.GetComponentsInChildren<SpriteRenderer>())
            .Where(r => r.enabled && r.sortingLayerName == "Default" && r.sortingOrder >= CombatVfxOrder.Ground && r.sortingOrder < 0).ToArray();
        var feet = players.Select(p => Mathf.Abs(p.VfxFootPosition.x))
            .Concat(monsters.Select(m => Mathf.Abs(m.FootPosition.x))).ToArray();
        var radii = monsters.Select(m => m.BodyRadius)
            .Concat(players.Select(_ => CombatMotion.PlayerRadius)).ToArray();
        return new Evidence
        {
            CurrentPoolId = background.CurrentPoolId,
            CurrentPresetId = background.CurrentPresetId,
            renderers = scenery.Length,
            activeColliders = background.GetComponentsInChildren<Collider2D>().Count(c => c.enabled && c.gameObject.activeInHierarchy),
            activeAnimators = background.GetComponentsInChildren<Animator>().Count(a => a.enabled && a.gameObject.activeInHierarchy),
            liveBodyRenderers = bodies.Length,
            groundEffectRenderers = groundEffects.Length,
            backgroundBehindBodies = bodies.Length == 0 ? null : scenery.Length > 0 && scenery.All(r => bodies.All(body => Before(r, body))),
            backgroundBehindGroundEffects = groundEffects.Length == 0 ? null : scenery.Length > 0 && scenery.All(r => groundEffects.All(effect => Before(r, effect))),
            maximumAbsoluteFootX = feet.Length == 0 ? 0 : feet.Max(),
            maximumBodyRadius = radii.Length == 0 ? 0 : radii.Max(),
            rendererEvidence = scenery.Select(Describe).ToArray()
        };
    }

    static bool Before(Renderer background, Renderer foreground)
    {
        var backGroup = OuterGroup(background); var frontGroup = OuterGroup(foreground);
        int a = SortingLayer.GetLayerValueFromID(backGroup != null ? backGroup.sortingLayerID : background.sortingLayerID);
        int b = SortingLayer.GetLayerValueFromID(frontGroup != null ? frontGroup.sortingLayerID : foreground.sortingLayerID);
        int backOrder = backGroup != null ? backGroup.sortingOrder : background.sortingOrder;
        int frontOrder = frontGroup != null ? frontGroup.sortingOrder : foreground.sortingOrder;
        var backMaterials = background.sharedMaterials.Where(m => m != null).ToArray();
        var frontMaterials = foreground.sharedMaterials.Where(m => m != null).ToArray();
        return (a < b || a == b && backOrder < frontOrder) && backMaterials.Length > 0 && frontMaterials.Length > 0 &&
            backMaterials.All(m => (!m.HasProperty("_ZWrite") || m.GetFloat("_ZWrite") == 0) && frontMaterials.All(f => m.renderQueue <= f.renderQueue));
    }

    static SortingGroup OuterGroup(Renderer renderer) => renderer.GetComponentsInParent<SortingGroup>().LastOrDefault(g => g.enabled);

    static RendererEvidence Describe(Renderer renderer)
    {
        var material = renderer.sharedMaterial;
        var group = OuterGroup(renderer);
        int layer = group != null ? group.sortingLayerID : renderer.sortingLayerID;
        bool hasDepthWrite = material != null && material.HasProperty("_ZWrite");
        var map = renderer.GetComponent<Tilemap>();
        return new RendererEvidence
        {
            name = renderer.name,
            type = renderer.GetType().Name,
            sortingLayer = SortingLayer.IDToName(layer),
            layerValue = SortingLayer.GetLayerValueFromID(layer),
            order = group != null ? group.sortingOrder : renderer.sortingOrder,
            outerSortingGroup = group != null ? group.name : null,
            shader = material != null && material.shader != null ? material.shader.name : null,
            materialRenderQueue = material != null ? material.renderQueue : -1,
            depthWritePropertyAvailable = hasDepthWrite,
            materialDepthWrite = hasDepthWrite ? material.GetFloat("_ZWrite") : null,
            distinctTileAssets = map != null ? map.GetUsedTilesCount() : 0
        };
    }
}
#endif
