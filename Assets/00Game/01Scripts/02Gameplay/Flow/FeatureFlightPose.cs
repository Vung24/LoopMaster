using UnityEngine;

internal struct FeatureFlightPose
{
    public Vector2 Center;
    public Vector2 Size;
    public float Angle;

    public FeatureFlightPose(Vector2 center, Vector2 size, float angle = 0f)
    {
        Center = center;
        Size = size;
        Angle = angle;
    }
}
