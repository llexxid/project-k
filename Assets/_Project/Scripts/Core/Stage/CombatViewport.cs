using UnityEngine;

/// <summary>World-space combat framing inside the portrait HUD. No changes to unit scale.</summary>
public static class CombatViewport
{
    public static Rect Bounds(bool boss = false)
    {
        var camera = Camera.main;
        if (camera == null) return Rect.MinMaxRect(-2.3f, -2.4f, 2.3f, boss ? 1.5f : 2.4f);
        float depth = Mathf.Abs(camera.transform.position.z);
        Vector3 left = camera.ViewportToWorldPoint(new Vector3(.12f, .32f, depth));
        Vector3 right = camera.ViewportToWorldPoint(new Vector3(.88f, boss ? .60f : .66f, depth));
        return Rect.MinMaxRect(Mathf.Max(left.x, -2.5f), Mathf.Max(left.y, -2.5f),
            Mathf.Min(right.x, 2.5f), Mathf.Min(right.y, boss ? 1.6f : 2.5f));
    }

    public static Vector3 Formation(int index)
    {
        Rect bounds = Bounds();
        float side = Mathf.Min(1.1f, bounds.width * .24f);
        return index == 0 ? new Vector3(0, .45f, 0) : new Vector3(index == 1 ? -side : side, -1.05f, 0);
    }

    public static Vector2 Spawn(Vector2 authoredPosition, bool boss)
    {
        Rect bounds = Bounds(boss);
        if (boss) return new Vector2(0, bounds.yMax);
        Vector2 direction = authoredPosition.sqrMagnitude > .01f ? authoredPosition.normalized : Vector2.up;
        Vector2 result;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            result = new Vector2(direction.x < 0 ? bounds.xMin : bounds.xMax,
                Mathf.Clamp(direction.y * 2f + Random.Range(-.45f, .45f), bounds.yMin, bounds.yMax));
        else result = new Vector2(Mathf.Clamp(direction.x * 2f + Random.Range(-.65f, .65f), bounds.xMin, bounds.xMax),
                direction.y < 0 ? bounds.yMin : bounds.yMax);
        return result;
    }
}
