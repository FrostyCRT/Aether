using UnityEngine;

public static class MapBoundaryUtils
{
    public const float ZoneHalfSize = 55f;

    public static Vector3 ClampToZone(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, -ZoneHalfSize, ZoneHalfSize);
        position.z = Mathf.Clamp(position.z, -ZoneHalfSize, ZoneHalfSize);
        return position;
    }
}