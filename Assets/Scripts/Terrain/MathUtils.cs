using UnityEngine;

public static class MathUtils
{
    /// <summary>
    /// An implementation of the barycentric algorithm from
    /// https://en.wikipedia.org/wiki/Barycentric_coordinate_system
    /// </summary>
    public static Vector3 Barycentric(Vector2 a, Vector2 b, Vector2 c, Vector2 v)
    {
        // Helper subtractions
        var vc = v - c;
        var ac = a - c;

        // Determinants
        var det = (b.y - c.y)*ac.x + (c.x - b.x)*ac.y;

        // Components
        float wa = ((b.y - c.y)*vc.x + (c.x - b.x)*vc.y) / det;
        float wb = ((c.y - a.y)*vc.x + (a.x - c.x)*vc.y) / det;
        float wc = 1 - wa - wb;

        // Return as Vector3
        return new(wa, wb, wc);
    }

    public static Vector2 XZ(this Vector3 v3)
        => new(v3.x, v3.z);

    public static Vector3 InvXZ(this Vector2 v2)
        => new(v2.x, 0, v2.y);
}
