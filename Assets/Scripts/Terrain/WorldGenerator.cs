using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

using RandInst = Unity.Mathematics.Random;

public class WorldGenerator : MonoBehaviour
{
    [Serializable]
    public struct OctaveInfo
    {
        public Vector2 NoiseScale;
        public Vector2 NoiseOffset;
        public float OutputScale;
    }


    [Header("Noise")]
    public OctaveInfo[] octaves;

    [Header("Mesh Details")]
    public int width;
    public int height;
    public float spacing;
    public float baseNoiseScale;

    [Header("Prefabs")]
    public GameObject mountain;

    [Header("Objects")]
    public GameObject mansion;
    public GameObject ship;
    public MeshFilter ground;

    [Header("Positioning")]
    public float minTravelDistance;
    public float maxTravelDistance;

    GameObject[] placedMountains;
    Mesh groundMesh;
    Vector3[] groundVerts;

    Vector2 Size => new(width, height);

    RandInst random;

    public void GenerateGround()
    {
        groundVerts = new Vector3[width * height];
        var triangles = new List<int>();

        var offX = random.NextInt(0, 10000);
        var offY = random.NextInt(0, 10000);

        for (int i = 0; i < width; i++)
        {
            for(int j = 0; j < height; j++)
            {
                var ceni = i - width / 2;
                var cenj = j - height / 2;

                var basePosition = new Vector3(ceni * spacing, 0, cenj * spacing);

                var totalNoise = 0f;
                for(int k = 0; k < octaves.Length; k++)
                {
                    var info = octaves[k];

                    var noise = Mathf.PerlinNoise(
                        info.NoiseScale.x * i * baseNoiseScale * spacing + info.NoiseOffset.x + offX,
                        info.NoiseScale.y * j * baseNoiseScale * spacing + info.NoiseOffset.y + offY
                    );

                    totalNoise += noise * info.OutputScale - info.OutputScale / 2;
                }

                var idx = i * width + j;
                groundVerts[idx] = basePosition + Vector3.up * totalNoise;

                if (i != width - 1 && j != height - 1)
                {
                    triangles.Add(idx + 0);
                    triangles.Add(idx + 1);
                    triangles.Add(idx + width);
                    triangles.Add(idx + width + 1);
                    triangles.Add(idx + width);
                    triangles.Add(idx + 1);
                }
            }
        }

        groundMesh = new Mesh();
        ground.mesh = groundMesh;

        groundMesh.vertices = groundVerts;
        groundMesh.triangles = triangles.ToArray();

        groundMesh.RecalculateBounds();
    }

    public Vector3 SamplePoint(Vector2 xz)
    {
        // Get the vertex indices for this position
        var norm = (xz) / spacing + Size / 2;

        var minx = (int)norm.x;
        var miny = (int)norm.y;

        var maxx = minx + 1;
        var maxy = miny + 1;

        // Get the triangle this vertex is a part of
        int a, b, c;

        if(norm.x + norm.y % 1 > 0.5f)
        {
            a = minx * width + miny;
            b = maxx * width + miny;
            c = minx * width + maxy;
        }
        else
        {
            a = minx * width + miny;
            b = maxx * width + miny;
            c = maxx * width + maxy;
        }

        // Debugging
        #if false
        Debug.DrawLine(
            xz.InvXZ() - Vector3.up * 1000,
            xz.InvXZ() + Vector3.up * 1000,
            Color.cyan,
            10
        );
        Debug.DrawLine(
            groundVerts[a],
            groundVerts[b],
            Color.red,
            10
        ); Debug.DrawLine(
            groundVerts[b],
            groundVerts[c],
            Color.red,
            10
        ); Debug.DrawLine(
            groundVerts[c],
            groundVerts[a],
            Color.red,
            10
        );
        #endif

        // Use barycentric interpolation to calculate the
        // point's position on the mesh
        var bary = MathUtils.Barycentric(
            groundVerts[a].XZ(),
            groundVerts[b].XZ(),
            groundVerts[c].XZ(),
            xz
        );

        return bary.x * groundVerts[a] 
             + bary.y * groundVerts[b]
             + bary.z * groundVerts[c];
    }

    public void Generate()
    {
        random = new((uint)Environment.TickCount);

        GenerateGround();

        ship.transform.position = SamplePoint(Vector2.zero);
        mansion.transform.position = SamplePoint(
            random.NextFloat2Direction() * random.NextFloat(minTravelDistance, maxTravelDistance)
        );
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WorldGenerator))]
public class MeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Regenerate"))
        {
            (target as WorldGenerator).Generate();
        }
    }
}


#endif