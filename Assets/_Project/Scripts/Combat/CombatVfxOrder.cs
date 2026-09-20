using UnityEngine;

namespace KingdomIdle.Combat
{
    public static class CombatVfxOrder
    {
        // Background tiles end at -280; player bodies use Default/2, monsters Enemy/0.
        public const int Ground = -40, Field = -30, FootStatus = -10;
        public const int Impact = 20, OverheadStatus = 50;

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
