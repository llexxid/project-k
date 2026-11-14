using UnityEngine;

namespace KingdomIdle.Combat
{
    public static class CombatVfxOrder
    {
        // Default: background, ground effects, summons. Enemy: bodies. CombatVFX: hits/projectiles/status.
        // Disjoint body bands keep guards readable even behind large enemies; feet sort within each side.
        public const int Ground = -40, Field = -30, FootStatus = -10;
        public const int Summon = 0;
        public const int Impact = 20, OverheadStatus = 50;
        private static readonly int BodyLayer = SortingLayer.NameToID("Enemy");

        public static void ApplyBody(SpriteRenderer renderer, float feetY, bool player)
        {
            if (renderer == null) return;
            int order = (player ? 600 : 0) + Mathf.Clamp(Mathf.RoundToInt(-feetY * 50), -250, 250);
            if (renderer.sortingLayerID != BodyLayer) renderer.sortingLayerID = BodyLayer;
            if (renderer.sortingOrder != order) renderer.sortingOrder = order;
        }

        public static bool IsGround(string prefab, int layer) => prefab switch
        {
            "GroundTelegraph" or "IceBloomWarning" or "SanctuaryHeal" or "Sanctuary" or
            "VenomMist" or "VoidRift" or "FireTornado" => true,
            "MeteorCrater" => layer != 1,
            "StarfallPulse" => true,
            _ => false
        };

        public static void Apply(SpriteRenderer renderer, string prefab, int layer)
        {
            bool ground = IsGround(prefab, layer);
            renderer.sortingLayerName = ground ? "Default" : "CombatVFX";
            renderer.sortingOrder = (ground ? prefab == "SanctuaryHeal" ? FootStatus :
                prefab == "MeteorCrater" || prefab == "StarfallPulse" || prefab == "GroundTelegraph" || prefab == "IceBloomWarning" ? Ground : Field : Impact) + layer;
        }
    }
}
