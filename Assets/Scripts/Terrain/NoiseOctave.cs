using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct NoiseOctave
{
    public Vector2 NoiseScale;
    public Vector2 NoiseOffset;
    public float OutputScale;

    public readonly float Sample(float baseNoiseScale, float2 pos, int2 off)
    {
        var unscaled = Mathf.PerlinNoise(
            NoiseScale.x * baseNoiseScale * pos.x + NoiseOffset.x + off.x,
            NoiseScale.y * baseNoiseScale * pos.y + NoiseOffset.y + off.y
        );

        return unscaled * OutputScale - OutputScale / 2;
    }
}